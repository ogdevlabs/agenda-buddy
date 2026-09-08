using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Accounts;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Deleting an account, over real HTTP against a real database — profile removed, dependent records scrubbed, and
/// nobody else's account touched.
/// </summary>
/// <remarks>
/// <para>
/// Closes <c>agenda-buddy-1hk.11</c>: App Review Guideline 5.1.1(v) requires an app that supports account creation
/// to let the user delete the account from inside it. The credential half lives in a different database and is
/// covered by <c>IdentityAccountDeletionTest</c>; this covers the domain half and the scrub.
/// </para>
/// <para>
/// ⚠️ <b>Asserted on the stored documents, not on the response.</b> The route answers 204 either way, and a
/// deletion that answered 204 while leaving the address on twelve appointments is exactly the defect a
/// status-code-only test would pass.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class AccountDeletionTest : IClassFixture<ServiceHostFixture<CustomerAnchor>>, IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Customer = "leaving@example.com";
    private const string Bystander = "staying@example.com";
    private const string Provider = "coach@example.com";

    private readonly ServiceHostFixture<CustomerAnchor> _customerHost;
    private readonly ServiceHostFixture<ProviderAnchor> _providerHost;
    private readonly TokenFactory _tokens;

    public AccountDeletionTest(
        ServiceHostFixture<CustomerAnchor> customerHost,
        ServiceHostFixture<ProviderAnchor> providerHost,
        CryptoSessionFixture crypto)
    {
        _customerHost = customerHost;
        _providerHost = providerHost;
        _tokens = new TokenFactory(crypto);
    }

    private HttpRequestMessage Delete(string route, string caller, string role) =>
        Authorized(new HttpRequestMessage(HttpMethod.Delete, route), caller, role);

    private HttpRequestMessage Authorized(HttpRequestMessage request, string caller, string role)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(caller, role));
        return request;
    }

    /// <summary>
    /// Two customers, one provider holding both as subscribers with an appointment each, plus messages,
    /// notifications and a payment on both sides. The bystander is the control: every assertion about what was
    /// removed is worthless without one about what was not.
    /// </summary>
    private static async Task SeedAsync(IMongoDatabase database)
    {
        await database.GetCollection<CustomerEntity>("customers").InsertManyAsync(
        [
            new CustomerEntity
            {
                Id = ObjectId.GenerateNewId(), FirstName = "Ada", LastName = "Leaving", Email = Customer,
                SubscribedProviderCollection = [Provider]
            },
            new CustomerEntity
            {
                Id = ObjectId.GenerateNewId(), FirstName = "Grace", LastName = "Staying", Email = Bystander,
                SubscribedProviderCollection = [Provider]
            }
        ]);

        await database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Pat",
            LastName = "Coach",
            Email = Provider,
            SubscribedCustomerCollection = [Customer, Bystander],
            AppointmentEntities =
            [
                Appointment("leaver-1", Customer),
                Appointment("leaver-2", Customer),
                Appointment("bystander-1", Bystander)
            ]
        });

        await database.GetCollection<AppointmentEntity>("appointments").InsertManyAsync(
        [
            Appointment("leaver-1", Customer),
            Appointment("leaver-2", Customer),
            Appointment("bystander-1", Bystander)
        ]);

        await database.GetCollection<MessageEntity>("messages").InsertManyAsync(
        [
            new MessageEntity { Id = ObjectId.GenerateNewId(), SenderEmail = Customer, RecipientEmail = Provider, Body = "See you then", ThreadId = "t1" },
            new MessageEntity { Id = ObjectId.GenerateNewId(), SenderEmail = Provider, RecipientEmail = Customer, Body = "Confirmed", ThreadId = "t1" },
            new MessageEntity { Id = ObjectId.GenerateNewId(), SenderEmail = Bystander, RecipientEmail = Provider, Body = "Hello", ThreadId = "t2" }
        ]);

        await database.GetCollection<NotificationEntity>("notifications").InsertManyAsync(
        [
            new NotificationEntity { Id = ObjectId.GenerateNewId(), RecipientEmail = Customer, Subject = "Booked", Body = "Your session is booked" },
            new NotificationEntity { Id = ObjectId.GenerateNewId(), RecipientEmail = Bystander, Subject = "Booked", Body = "Your session is booked" }
        ]);

        await database.GetCollection<PaymentEntity>("payments").InsertOneAsync(new PaymentEntity
        {
            Id = ObjectId.GenerateNewId(),
            AppointmentIdentifier = "leaver-1",
            ProviderEmail = Provider,
            CustomerEmail = Customer,
            Amount = 5000,
            Currency = "usd"
        });

        await database.GetCollection<NoteEntity>("notes").InsertOneAsync(new NoteEntity
        {
            Id = ObjectId.GenerateNewId(),
            ProviderEmail = Provider,
            AppointmentIdentifier = "leaver-1",
            Content = "Worked on form"
        });
    }

    private static AppointmentEntity Appointment(string identifier, string customerEmail) => new()
    {
        Id = ObjectId.GenerateNewId(),
        Identifier = identifier,
        EmailProvider = Provider,
        EmailCustomer = customerEmail,
        Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
        End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc)
    };

    // ── the customer path ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ACustomerDeletesTheirOwnAccount_AndTheProfileIsGone()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        var response = await service.Client.SendAsync(
            Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).FirstOrDefaultAsync());
    }

    /// <summary>
    /// ⚠️ <b>The acceptance criterion in full: no dependent record still exposes the address.</b> This is the
    /// assertion that a status-code-only test cannot make, and the one the whole feature is for.
    /// </summary>
    [Fact]
    public async Task AfterDeletion_NoRecordAnywhereStillCarriesTheAddress()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        await service.Client.SendAsync(Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));

        Assert.Empty(await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.EmailCustomer, Customer)).ToListAsync());
        Assert.Empty(await service.Database.GetCollection<MessageEntity>("messages")
            .Find(Builders<MessageEntity>.Filter.Or(
                Builders<MessageEntity>.Filter.Eq(m => m.SenderEmail, Customer),
                Builders<MessageEntity>.Filter.Eq(m => m.RecipientEmail, Customer))).ToListAsync());
        Assert.Empty(await service.Database.GetCollection<NotificationEntity>("notifications")
            .Find(Builders<NotificationEntity>.Filter.Eq(n => n.RecipientEmail, Customer)).ToListAsync());
        Assert.Empty(await service.Database.GetCollection<PaymentEntity>("payments")
            .Find(Builders<PaymentEntity>.Filter.Eq(p => p.CustomerEmail, Customer)).ToListAsync());

        var provider = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();
        Assert.DoesNotContain(Customer, provider.SubscribedCustomerCollection);
        Assert.DoesNotContain(Customer, provider.AppointmentEntities.Select(a => a.EmailCustomer));
    }

    /// <summary>
    /// ⚠️ <b>Every embedded copy, not just the first.</b> MongoDB's positional <c>$</c> rewrites one array element
    /// per update, so a single pass filtered on the address would leave the second appointment carrying it — and
    /// the provider's embedded list is what the calendar actually reads.
    /// </summary>
    [Fact]
    public async Task AfterDeletion_EveryEmbeddedAppointmentIsScrubbed_NotJustTheFirst()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        await service.Client.SendAsync(Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));

        var provider = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();

        var scrubbed = provider.AppointmentEntities
            .Where(a => a.Identifier is "leaver-1" or "leaver-2")
            .ToList();

        Assert.Equal(2, scrubbed.Count);
        Assert.All(scrubbed, a => Assert.True(AccountErasure.IsTombstone(a.EmailCustomer)));
    }

    /// <summary>
    /// The counterparty's history survives. Deleting the appointment outright would delete the <b>provider's</b>
    /// record of a session they delivered and were paid for.
    /// </summary>
    [Fact]
    public async Task AfterDeletion_TheProvidersRecordOfTheSessionsSurvivesUnderATombstone()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        await service.Client.SendAsync(Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));

        var appointments = await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Empty).ToListAsync();

        // All three still exist: two scrubbed, one untouched.
        Assert.Equal(3, appointments.Count);
        Assert.Equal(2, appointments.Count(a => AccountErasure.IsTombstone(a.EmailCustomer)));

        // One tombstone for the whole deletion, so the provider still sees two sessions with one person.
        var tombstones = appointments
            .Where(a => AccountErasure.IsTombstone(a.EmailCustomer))
            .Select(a => a.EmailCustomer)
            .Distinct();
        Assert.Single(tombstones);
    }

    /// <summary>
    /// The control. Every assertion above about what was removed is worthless without one about what was not.
    /// </summary>
    [Fact]
    public async Task AfterDeletion_NobodyElsesRecordsAreTouched()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        await service.Client.SendAsync(Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));

        Assert.NotNull(await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Bystander)).FirstOrDefaultAsync());
        Assert.Single(await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.EmailCustomer, Bystander)).ToListAsync());
        Assert.Single(await service.Database.GetCollection<NotificationEntity>("notifications")
            .Find(Builders<NotificationEntity>.Filter.Eq(n => n.RecipientEmail, Bystander)).ToListAsync());

        var provider = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();
        Assert.Contains(Bystander, provider.SubscribedCustomerCollection);
        Assert.Contains(Bystander, provider.AppointmentEntities.Select(a => a.EmailCustomer));
    }

    /// <summary>
    /// ⚠️ The IDOR that would make this the worst route in the product. <c>{email}</c> must be the caller's own
    /// claim.
    /// </summary>
    [Fact]
    public async Task ACustomerCannotDeleteSomebodyElsesAccount()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        var response = await service.Client.SendAsync(
            Delete($"api/v1/customers/{Customer}", Bystander, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).FirstOrDefaultAsync());
    }

    [Fact]
    public async Task DeletingACustomerAccountAnonymouslyIsRefused()
    {
        using var service = _customerHost.StartService("Production");

        var response = await service.Client.DeleteAsync($"api/v1/customers/{Customer}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// ⚠️ <b>An account with no profile still deletes.</b> Registration writes a credential and then a profile; if
    /// the second call fails the account can sign in and has no profile — and that is exactly the account that must
    /// stay deletable.
    /// </summary>
    [Fact]
    public async Task AnAccountWithNoProfileStillDeletesSuccessfully()
    {
        using var service = _customerHost.StartService("Production");

        var response = await service.Client.SendAsync(
            Delete("api/v1/customers/never-had-a-profile@example.com",
                "never-had-a-profile@example.com", TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>Deleting twice is not an error, so a client that retries a timed-out delete is not stuck.</summary>
    [Fact]
    public async Task DeletingTwiceIsIdempotent()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        var first = await service.Client.SendAsync(Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));
        var second = await service.Client.SendAsync(Delete($"api/v1/customers/{Customer}", Customer, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        // A second run must not scrub already-scrubbed rows under a fresh token, which would split one person's
        // history into two identities in the provider's book.
        var appointments = await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Empty).ToListAsync();
        Assert.Single(appointments
            .Where(a => AccountErasure.IsTombstone(a.EmailCustomer))
            .Select(a => a.EmailCustomer)
            .Distinct());
    }

    // ── the provider path ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AProviderDeletesTheirOwnAccount_AndTheProfileAndNotesAreGone()
    {
        using var service = _providerHost.StartService("Production");
        await SeedAsync(service.Database);

        var response = await service.Client.SendAsync(
            Delete($"api/v1/providers/{Provider}", Provider, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).FirstOrDefaultAsync());

        // A provider's session notes are private to them and are about their clients, so they go outright.
        Assert.Empty(await service.Database.GetCollection<NoteEntity>("notes")
            .Find(Builders<NoteEntity>.Filter.Eq(n => n.ProviderEmail, Provider)).ToListAsync());
    }

    /// <summary>
    /// A customer's own history of sessions they attended is theirs, so it survives with the provider's address
    /// replaced rather than being deleted with the provider.
    /// </summary>
    [Fact]
    public async Task AfterAProviderIsDeleted_TheirCustomersHistorySurvivesUnderATombstone()
    {
        using var service = _providerHost.StartService("Production");
        await SeedAsync(service.Database);

        await service.Client.SendAsync(Delete($"api/v1/providers/{Provider}", Provider, TokenFactory.ProviderRole));

        var appointments = await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Empty).ToListAsync();

        Assert.Equal(3, appointments.Count);
        Assert.All(appointments, a => Assert.True(AccountErasure.IsTombstone(a.EmailProvider)));

        // Both customers stop showing a subscription to somebody who no longer exists.
        var customers = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Empty).ToListAsync();
        Assert.All(customers, c => Assert.DoesNotContain(Provider, c.SubscribedProviderCollection ?? []));
    }

    /// <summary>
    /// ⚠️ <b>Deleting is not deactivating.</b> The pre-existing <c>POST /{email}/deactivate</c> sets
    /// <c>IsActive=false</c> and keeps everything; if this route ever devolved into that, every name, address and
    /// phone number would stay on file while the user was told they were gone.
    /// </summary>
    [Fact]
    public async Task DeletingAProviderIsNotTheSameAsDeactivatingOne()
    {
        using var service = _providerHost.StartService("Production");
        await SeedAsync(service.Database);

        await service.Client.SendAsync(
            Authorized(new HttpRequestMessage(HttpMethod.Post, $"api/v1/providers/{Provider}/deactivate"),
                Provider, TokenFactory.ProviderRole));

        // Deactivation keeps the document.
        var deactivated = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();
        Assert.False(deactivated.IsActive);

        await service.Client.SendAsync(Delete($"api/v1/providers/{Provider}", Provider, TokenFactory.ProviderRole));

        // Deletion does not.
        Assert.Null(await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).FirstOrDefaultAsync());
    }

    [Fact]
    public async Task AProviderCannotDeleteADifferentProvidersAccount()
    {
        using var service = _providerHost.StartService("Production");
        await SeedAsync(service.Database);

        var response = await service.Client.SendAsync(
            Delete($"api/v1/providers/{Provider}", "other-coach@example.com", TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).FirstOrDefaultAsync());
    }

    // ── avatar and consent ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ACustomerSetsTheirAvatarThroughTheDedicatedRoute()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);
        var chosen = AgendaBuddy.Library.Avatars.AvatarCatalog.Ids[6];

        var request = Authorized(
            new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Customer}/avatar")
            {
                Content = JsonContent.Create(new { avatarId = chosen })
            }, Customer, TokenFactory.CustomerRole);
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).SingleAsync();
        Assert.Equal(chosen, stored.AvatarId);
    }

    /// <summary>
    /// ⚠️ An unknown id is 400, not 404 and not a silent success. Storing it throws nowhere —
    /// <c>AvatarCatalog.Resolve</c> treats it as absent — so the account would keep its old mark behind a 200.
    /// </summary>
    [Fact]
    public async Task AnUnknownAvatarIdIsRefusedWithABadRequest()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        var request = Authorized(
            new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Customer}/avatar")
            {
                Content = JsonContent.Create(new { avatarId = "avatar_99" })
            }, Customer, TokenFactory.CustomerRole);
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).SingleAsync();
        Assert.Equal(string.Empty, stored.AvatarId);
    }

    [Fact]
    public async Task ACustomerCannotSetSomebodyElsesAvatar()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        var request = Authorized(
            new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Customer}/avatar")
            {
                Content = JsonContent.Create(new { avatarId = AgendaBuddy.Library.Avatars.AvatarCatalog.Ids[0] })
            }, Bystander, TokenFactory.CustomerRole);
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Consent is stamped by the <b>server</b>. The request carries booleans and no dates, so a device with a wrong
    /// clock cannot file the acceptance in the wrong year.
    /// </summary>
    [Fact]
    public async Task AcceptingTheAgreementsStampsTheServersOwnClock()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);
        var before = DateTime.UtcNow.AddSeconds(-5);

        var request = Authorized(
            new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Customer}/consent")
            {
                Content = JsonContent.Create(new { acceptedTerms = true, acceptedPrivacy = true })
            }, Customer, TokenFactory.CustomerRole);
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).SingleAsync();
        Assert.NotNull(stored.TermsAcceptedAt);
        Assert.NotNull(stored.PrivacyAcceptedAt);
        Assert.True(stored.TermsAcceptedAt >= before);
    }

    /// <summary>
    /// ⚠️ <b>Withdrawing consent clears the timestamp.</b> Written as an explicit null rather than an omitted
    /// field, or an unticked box would leave the previous acceptance standing and read back as still accepted.
    /// </summary>
    [Fact]
    public async Task WithdrawingConsentClearsThePreviousAcceptance()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        async Task SetAsync(bool terms, bool privacy)
        {
            var request = Authorized(
                new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Customer}/consent")
                {
                    Content = JsonContent.Create(new { acceptedTerms = terms, acceptedPrivacy = privacy })
                }, Customer, TokenFactory.CustomerRole);
            var response = await service.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await SetAsync(true, true);
        await SetAsync(false, false);

        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).SingleAsync();
        Assert.Null(stored.TermsAcceptedAt);
        Assert.Null(stored.PrivacyAcceptedAt);
    }

    /// <summary>
    /// A targeted <c>$set</c>: the name, the phone number and the subscription list must all survive a consent
    /// write, which a whole-document replace would discard.
    /// </summary>
    [Fact]
    public async Task RecordingConsentLeavesEveryOtherFieldAlone()
    {
        using var service = _customerHost.StartService("Production");
        await SeedAsync(service.Database);

        var request = Authorized(
            new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Customer}/consent")
            {
                Content = JsonContent.Create(new { acceptedTerms = true, acceptedPrivacy = true })
            }, Customer, TokenFactory.CustomerRole);
        await service.Client.SendAsync(request);

        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Customer)).SingleAsync();
        Assert.Equal("Ada", stored.FirstName);
        Assert.Equal("Leaving", stored.LastName);
        Assert.Contains(Provider, stored.SubscribedProviderCollection ?? []);
    }

    [Fact]
    public async Task AProviderSetsTheirAvatarWithoutDisturbingTheirServices()
    {
        using var service = _providerHost.StartService("Production");

        var serviceId = ObjectId.GenerateNewId();
        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Pat",
            LastName = "Coach",
            Email = Provider,
            ServiceEntities = [new ServiceEntity { Id = serviceId, Name = "Session", Fee = 50 }]
        });

        var chosen = AgendaBuddy.Library.Avatars.AvatarCatalog.Ids[3];
        var request = Authorized(
            new HttpRequestMessage(HttpMethod.Put, $"api/v1/providers/{Provider}/avatar")
            {
                Content = JsonContent.Create(new { avatarId = chosen })
            }, Provider, TokenFactory.ProviderRole);
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();
        Assert.Equal(chosen, stored.AvatarId);
        // ⚠️ The reason this is a dedicated route: PUT /{email} replaces the whole document and resets every
        // nested service id to ObjectId.Empty (agenda-buddy-2wf).
        Assert.Equal(serviceId, Assert.Single(stored.ServiceEntities).Id);
    }
}
