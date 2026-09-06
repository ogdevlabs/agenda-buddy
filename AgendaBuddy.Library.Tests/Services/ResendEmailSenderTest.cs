using System.Net;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class ResendEmailSenderTest
{
    private const string Token = "a-token-that-must-not-leak";

    private static ResendEmailSender Sut(EmailOptions options, HttpMessageHandler? handler = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler ?? new ThrowingHandler()));

        return new ResendEmailSender(factory.Object, Options.Create(options));
    }

    /// <summary>
    /// No key configured is a supported state, not an error: a local run has no mail provider, and
    /// registration must not fail because of it.
    /// </summary>
    [Fact]
    public async Task WithNoApiKey_ReturnsFalseWithoutSendingAnything()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        var sent = await Sut(new EmailOptions { ApiKey = null }, handler)
            .SendAsync("someone@example.com", "Confirm your email address", Token);

        Assert.False(sent);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task WithAnApiKey_PostsToResendAndReportsSuccess()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        var sent = await Sut(new EmailOptions { ApiKey = "re_test", FromAddress = "no-reply@example.com" }, handler)
            .SendAsync("someone@example.com", "Confirm your email address", Token);

        Assert.True(sent);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("https://api.resend.com/emails", handler.LastUri?.ToString());
        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Contains(Token, handler.LastBody);
    }

    /// <summary>
    /// The default sender is the product's own verified domain, not Resend's sandbox address.
    /// </summary>
    /// <remarks>
    /// Worth pinning because nothing else does, and getting it wrong is silent in both directions.
    /// <c>onboarding@resend.dev</c> — the previous default — is accepted by Resend with no verified domain but
    /// delivers <b>only</b> to the Resend account owner, so a real customer's password reset went nowhere and
    /// the send still reported success. A sending domain that is not verified is rejected outright, and
    /// <see cref="ResendEmailSender"/> absorbs that by contract, so that failure is equally quiet.
    /// </remarks>
    [Fact]
    public async Task TheDefaultSenderIsTheProductsOwnDomain()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        // No FromAddress/FromName set: this is what a deployment with only an API key configured sends as.
        await Sut(new EmailOptions { ApiKey = "re_test" }, handler)
            .SendAsync("someone@example.com", "Confirm your email address", Token);

        // The address only: the angle brackets around it are JSON-escaped in the body, so matching the whole
        // "Name <address>" header here would be asserting the encoder's behaviour rather than the sender's.
        Assert.Contains("AgendaMe@fererelabs.com", handler.LastBody);
        Assert.DoesNotContain("resend.dev", handler.LastBody);
    }

    // A provider outage must not fail the operation that triggered the send. On the reset path in
    // particular, a 500 would confirm to an attacker that the address has an account.
    [Fact]
    public async Task WhenTheProviderRejectsTheSend_ReturnsFalseRatherThanThrowing()
    {
        var sent = await Sut(new EmailOptions { ApiKey = "re_test" }, new RecordingHandler(HttpStatusCode.Forbidden))
            .SendAsync("someone@example.com", "Reset your password", Token);

        Assert.False(sent);
    }

    [Fact]
    public async Task WhenTheProviderIsUnreachable_ReturnsFalseRatherThanThrowing()
    {
        var sent = await Sut(new EmailOptions { ApiKey = "re_test" }, new ThrowingHandler())
            .SendAsync("someone@example.com", "Reset your password", Token);

        Assert.False(sent);
    }

    [Fact]
    public async Task WithNoRecipient_SendsNothing()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        var sent = await Sut(new EmailOptions { ApiKey = "re_test" }, handler).SendAsync("", "Subject", Token);

        Assert.False(sent);
        Assert.Equal(0, handler.Calls);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }
        public string? LastAuthScheme { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no network");
    }
}
