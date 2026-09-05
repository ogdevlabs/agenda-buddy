using System.Threading.Tasks;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// Push is disabled rather than broken when no provider is configured — the same shape as a non-charging
/// payment gateway (ADR-038) and an API-key-less <see cref="ResendEmailSender"/>.
/// </summary>
public class UnconfiguredPushSenderTest
{
    private static UnconfiguredPushSender Create(PushOptions? options = null) =>
        new(Options.Create(options ?? new PushOptions()));

    /// <summary>
    /// <c>false</c>, not <c>true</c>. A sender that claims success for a message it never sent turns a missing
    /// credential into a silent delivery hole no log or metric can find.
    /// </summary>
    [Fact]
    public async Task WithNothingConfigured_ReportsThatItDeliveredNothing()
    {
        Assert.False(await Create().SendAsync("device-token", "Subject", "Body"));
    }

    // Registration and sending must not throw: a local AppHost run has no push provider, and every caller
    // treats delivery as best-effort.
    [Fact]
    public async Task WithNothingConfigured_DoesNotThrow()
    {
        await Create().SendAsync("device-token", "Subject", "Body");
    }

    [Fact]
    public void IsConfigured_IsFalseUntilBothProjectIdAndCredentialArePresent()
    {
        Assert.False(Create().IsConfigured);
        Assert.False(Create(new PushOptions { FirebaseProjectId = "agenda-me" }).IsConfigured);
        Assert.False(Create(new PushOptions { ServiceAccountJson = "{}" }).IsConfigured);

        // Both, because FCM HTTP v1 needs a bearer token minted from the service account -- a project id on
        // its own cannot authenticate anything.
        Assert.True(Create(new PushOptions
        {
            FirebaseProjectId = "agenda-me",
            ServiceAccountJson = "{}"
        }).IsConfigured);
    }

    /// <summary>
    /// This implementation sends nothing even when handed credentials — it is the not-configured branch, and
    /// <c>AddPushDelivery</c> is what makes sure it is only reached when there are none. A test asserting
    /// otherwise would be asserting that the wrong sender got registered.
    /// </summary>
    [Fact]
    public async Task ThisImplementationSendsNothingRegardlessOfWhatItIsHanded()
    {
        var sender = Create(new PushOptions
        {
            FirebaseProjectId = "agenda-me",
            ServiceAccountJson = "{}"
        });

        Assert.False(await sender.SendAsync("device-token", "Subject", "Body"));
    }
}
