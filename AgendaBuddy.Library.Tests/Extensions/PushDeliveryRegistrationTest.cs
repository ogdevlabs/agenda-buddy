using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgendaBuddy.Library.Extensions;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgendaBuddy.Library.Tests.Extensions;

/// <summary>
/// Which <see cref="IPushSender"/> <c>AddPushDelivery</c> registers, and — the point of this file — that an
/// unreadable credential can never be the reason something other than push fails.
/// </summary>
/// <remarks>
/// <see cref="FcmPushSender"/> is a singleton whose constructor throws on a malformed credential, and it is
/// resolved LAZILY. So a credential that was corrupted in transit did not fail at startup: it threw on the
/// first request that needed a notification. <c>INotificationDispatcher</c> is injected into the Booking
/// handlers as well as <c>MessageModule</c>, so appointment booking, cancellation, status changes and
/// messaging all answered a gateway 502 over a value only push reads.
/// <para>
/// Push is best-effort by contract — <c>DispatchAsync</c> never throws and every channel is independent — so a
/// channel that cannot be CONSTRUCTED has to degrade exactly like one that cannot deliver.
/// </para>
/// </remarks>
public class PushDeliveryRegistrationTest
{
    private const string ProjectId = "agenda-me-test";

    private static string ServiceAccountJson()
    {
        using var rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = "service_account",
            ["project_id"] = ProjectId,
            ["client_email"] = "push@agenda-me-test.iam.gserviceaccount.com",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem()
        });
    }

    private static IPushSender Resolve(string? projectId, string? serviceAccountJson)
    {
        var settings = new Dictionary<string, string?>();
        if (projectId is not null) settings["Push:FirebaseProjectId"] = projectId;
        if (serviceAccountJson is not null) settings["Push:ServiceAccountJson"] = serviceAccountJson;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddPushDelivery(configuration);

        return services.BuildServiceProvider().GetRequiredService<IPushSender>();
    }

    [Fact]
    public void NothingConfigured_ResolvesTheUnconfiguredSender()
    {
        Assert.IsType<UnconfiguredPushSender>(Resolve(null, null));
    }

    [Fact]
    public void BothValuesPresentAndReadable_ResolvesTheRealSender()
    {
        Assert.IsType<FcmPushSender>(Resolve(ProjectId, ServiceAccountJson()));
    }

    [Fact]
    public void ABase64Credential_ResolvesTheRealSender()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(ServiceAccountJson()));

        Assert.IsType<FcmPushSender>(Resolve(ProjectId, encoded));
    }

    // THE regression. This exact shape — the private_key's \n escapes turned into raw newlines by Bicep's own
    // un-escaping — took appointment booking and messaging down in a deployed environment.
    [Fact]
    public void ACredentialCorruptedInTransit_DegradesToUnconfiguredRatherThanThrowing()
    {
        var corrupted = ServiceAccountJson().Replace("\\n", "\n");

        // Resolving must not throw: whatever needs a notification is not push's to break.
        var sender = Resolve(ProjectId, corrupted);

        Assert.IsType<UnconfiguredPushSender>(sender);
    }

    [Fact]
    public void ACredentialThatIsNotACredentialAtAll_DegradesToUnconfigured()
    {
        Assert.IsType<UnconfiguredPushSender>(Resolve(ProjectId, "not a credential"));
    }

    // A service-account key missing its required fields is a misconfiguration, not a corrupt transport — but it
    // must degrade the same way, for the same reason.
    [Fact]
    public void ACredentialMissingItsKeyFields_DegradesToUnconfigured()
    {
        Assert.IsType<UnconfiguredPushSender>(Resolve(ProjectId, """{"client_email":"a@b.test"}"""));
    }

    // A project id with no credential, and a credential with no project id: both are "not configured".
    [Theory]
    [InlineData(ProjectId, null)]
    [InlineData(null, "{}")]
    public void OnlyOneHalfSupplied_ResolvesTheUnconfiguredSender(string? projectId, string? json)
    {
        Assert.IsType<UnconfiguredPushSender>(Resolve(projectId, json));
    }
}
