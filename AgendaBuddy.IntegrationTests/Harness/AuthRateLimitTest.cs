using System.Net;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The per-IP limiter answers <c>429</c> with
/// <c>Retry-After</c> against a <b>running Identity service</b>, and is absent when its flag is off.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a unit test.</b> A unit test on a policy object passes while the middleware is
/// unregistered, or registered in the wrong place, or attached to no endpoint. That is not a hypothetical
/// failure mode — <c>AssertRole</c> has existed in the codebase and gone uncalled by anything before.
/// The PRD therefore makes the integration suite required for this
/// criterion even though CONSTITUTION §7 does not require it in general.
/// </para>
/// <para>
/// <b>Credentials are seeded straight into MongoDB</b> rather than through <c>POST /register</c>, because
/// registering mints a token pair and that needs <c>JWT_PRIVATE_KEY</c> — a private key
/// <see cref="CryptoSessionFixture"/> deliberately never materialises as a string in a
/// public repository. Wrong-password logins reach every part of the path this test is about without
/// signing anything.
/// </para>
/// <para>
/// <b>All requests share one partition.</b> <c>TestServer</c> leaves <c>RemoteIpAddress</c> null, so the
/// limiter files them under <c>RateLimitingExtensions.UnattributedPartition</c> — which is what makes
/// "many requests from one IP" expressible here at all, and is the same code path a deployment behind a
/// proxy that does not forward the client address would take.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class AuthRateLimitTest(ServiceHostFixture<IdentityAnchor> host)
    : IClassFixture<ServiceHostFixture<IdentityAnchor>>
{
    private const string Email = "throttle@example.com";
    private const string CorrectPassword = "correct horse battery staple";
    private const string WrongPassword = "not the password";
    private const int Permitted = 3;

    /// <summary>Enables the limiter with a small allowance, so the test spends 3 verifies and not 10.</summary>
    private static readonly Dictionary<string, string> LimiterOn = new()
    {
        ["Security:RateLimiting:Enabled"] = "true",
        ["Security:RateLimiting:PermitPerMinute"] = Permitted.ToString()
    };

    private static HttpContent Login(string password) =>
        JsonContent.Create(new { email = Email, password });

    private static async Task SeedCredentialAsync(ServiceHost service)
    {
        await service.Database.GetCollection<CredentialEntity>("credentials").InsertOneAsync(
            new CredentialEntity
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, workFactor: 12),
                Role = "Provider"
            });
    }

    private static async Task<CredentialEntity> StoredAsync(ServiceHost service) =>
        await service.Database.GetCollection<CredentialEntity>("credentials")
            .Find(Builders<CredentialEntity>.Filter.Eq(credential => credential.Email, Email))
            .SingleAsync();

    [Fact]
    public async Task T101_RequestsBeyondTheAllowance_Get429WithRetryAfter()
    {
        using var service = host.StartService(settings: LimiterOn);
        await SeedCredentialAsync(service);

        var accepted = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < Permitted; attempt++)
        {
            var response = await service.Client.PostAsync("api/v1/auth/login", Login(WrongPassword));
            accepted.Add(response.StatusCode);
        }

        var throttled = await service.Client.PostAsync("api/v1/auth/login", Login(WrongPassword));

        Assert.All(accepted, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

        // Required by PRD requirement 12: an honest client has to be able to back off correctly rather
        // than guess, and a 429 with no Retry-After invites a tight retry loop — which is the attack.
        Assert.NotNull(throttled.Headers.RetryAfter);
    }

    [Fact]
    public async Task T102_AThrottledRequest_CostsNoBcryptAndTakesNoWrite()
    {
        // The failed-attempt counter is an unauthenticated write path on a collection with
        // no backups, so the limiter has to be evaluated BEFORE it (PRD requirement 11). The counter is
        // the observable proof: it advances once per verified-and-rejected attempt, and a throttled
        // request must not move it at all.
        using var service = host.StartService(settings: LimiterOn);
        await SeedCredentialAsync(service);

        for (var attempt = 0; attempt < Permitted; attempt++)
        {
            await service.Client.PostAsync("api/v1/auth/login", Login(WrongPassword));
        }

        var counterBeforeThrottling = (await StoredAsync(service)).FailedAttempts;
        var throttled = await service.Client.PostAsync("api/v1/auth/login", Login(WrongPassword));

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal(Permitted, counterBeforeThrottling);
        Assert.Equal(counterBeforeThrottling, (await StoredAsync(service)).FailedAttempts);
    }

    [Fact]
    public async Task T101_TheLimiterAlsoCoversRegister_WhichHashesAtTheSameCost()
    {
        // Design decision D-4. RegisterAsync hashes at work factor 12 exactly as login verifies at it, so
        // limiting login alone would leave an equal-cost amplification vector open. The bodies here are
        // deliberately invalid: the limiter runs before validation, so a throttled caller gets 429 rather
        // than 400 — which is correct (rejecting cheaply is the point) and worth pinning.
        using var service = host.StartService(settings: LimiterOn);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < Permitted + 1; attempt++)
        {
            var response = await service.Client.PostAsync(
                "api/v1/auth/register", JsonContent.Create(new { email = "nope", password = "short" }));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(Permitted), status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Refresh_IsNotRateLimited()
    {
        // Also D-4, and the other half of it: refresh spends no BCrypt, so throttling it would buy
        // nothing and would risk breaking the hourly rotation a legitimate mobile client performs.
        using var service = host.StartService(settings: LimiterOn);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < Permitted + 3; attempt++)
        {
            var response = await service.Client.PostAsync(
                "api/v1/auth/refresh", JsonContent.Create(new { refreshToken = "not-a-real-token" }));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
    }

    [Fact]
    public async Task AC14_WithTheDefaultConfiguration_NothingIsThrottled()
    {
        // The AppHost leaves both flags off, so a developer exercising the Bruno collection or
        // scripts/run-ios.sh must see exactly today's behaviour. This is the assertion that keeps the
        // hardening from becoming a local-development tax.
        using var service = host.StartService();
        await SeedCredentialAsync(service);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < Permitted + 2; attempt++)
        {
            var response = await service.Client.PostAsync("api/v1/auth/login", Login(WrongPassword));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task ALockedAccount_IsRefusedIndistinguishablyFromAWrongPassword()
    {
        // AC-7 over real HTTP: both answer a bare 401, so nothing tells an attacker which addresses exist
        // or which they have managed to lock. Threshold set to the number of attempts this test makes.
        using var service = host.StartService(settings: new Dictionary<string, string>
        {
            ["Security:Lockout:MaxFailedAttempts"] = "2",
            ["Security:Lockout:WindowMinutes"] = "15"
        });
        await SeedCredentialAsync(service);

        HttpResponseMessage wrongPassword = null!;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            wrongPassword = await service.Client.PostAsync("api/v1/auth/login", Login(WrongPassword));
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        }

        // The CORRECT password now, so the only thing that can refuse it is the lock.
        var lockedOut = await service.Client.PostAsync("api/v1/auth/login", Login(CorrectPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, lockedOut.StatusCode);
        Assert.NotNull((await StoredAsync(service)).LockUntil);

        // ⚠️ The 401 body is NOT empty: UseStatusCodePages turns a bodyless 401 into ProblemDetails —
        // a bodyless 403 hits the same surprise. So indistinguishability has to be asserted as
        // "identical", not as "absent" — which is the stronger claim anyway.
        Assert.Equal(
            await ComparableBodyAsync(wrongPassword),
            await ComparableBodyAsync(lockedOut));
    }

    /// <summary>
    /// A response body with the per-request correlation fields removed, so two responses can be compared
    /// for the only thing AC-7 cares about: whether they tell a caller anything different.
    /// </summary>
    private static async Task<string> ComparableBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        var node = System.Text.Json.Nodes.JsonNode.Parse(body)!.AsObject();

        // Identity's CustomizeProblemDetails stamps requestId from the current Activity, so it differs
        // per request by design. Comparing raw bodies would fail on that alone and prove nothing.
        node.Remove("requestId");
        node.Remove("traceId");

        return node.ToJsonString();
    }
}
