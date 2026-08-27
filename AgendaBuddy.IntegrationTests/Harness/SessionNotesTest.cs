using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-014 AC-1, AC-9, AC-10, AC-12 / threats T-201 and T-202: session notes are provider-private, and the
/// existence of a note is not disclosed either.
/// </summary>
/// <remarks>
/// <para>
/// <b>These notes are the most sensitive data in the product</b> — therapy and coaching notes about named
/// individuals. They are also the newest route family, so they get the strictest posture: `Provider` role,
/// ownership of the appointment, and not-found made indistinguishable from not-yours.
/// </para>
/// <para>
/// <b>Threat T-201 is F-016's defect, one layer along.</b> <c>NoteService.GetByAppointmentAsync</c> takes a
/// <c>providerEmail</c> parameter, so the obvious route passes a client-supplied value through — which would
/// hand any authenticated caller every provider's notes for any appointment identifier they can guess, and
/// customers already receive those identifiers in their own appointment responses. The provider email comes
/// from the caller's <c>sub</c> claim instead, and these tests are what prove it.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class SessionNotesTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string Owner = "notes-owner@example.com";
    private const string OtherProvider = "other-provider@example.com";
    private const string TheCustomer = "a-real-client@example.com";
    private const string OwnedAppointment = "appointment-owned";
    private const string ForeignAppointment = "appointment-foreign";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<ServiceHost> StartWithTwoProvidersAppointmentsAsync()
    {
        var service = host.StartService("Production");
        var appointments = service.Database.GetCollection<AppointmentEntity>("appointments");

        await appointments.InsertManyAsync(
        [
            new AppointmentEntity
            {
                Id = ObjectId.GenerateNewId(), Identifier = OwnedAppointment,
                EmailProvider = Owner, EmailCustomer = TheCustomer,
                Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc)
            },
            new AppointmentEntity
            {
                Id = ObjectId.GenerateNewId(), Identifier = ForeignAppointment,
                EmailProvider = OtherProvider, EmailCustomer = TheCustomer,
                Start = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc)
            }
        ]);

        return service;
    }

    private HttpRequestMessage Authorised(HttpMethod method, string path, string subject, string role,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(subject, role));
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Theory]
    [InlineData("GET", "api/v1/booking/appointments/appointment-owned/notes")]
    [InlineData("POST", "api/v1/booking/appointments/appointment-owned/notes")]
    [InlineData("PUT", "api/v1/booking/notes/000000000000000000000000")]
    [InlineData("DELETE", "api/v1/booking/notes/000000000000000000000000")]
    public async Task AC8_EveryNotesRoute_RefusesAnAnonymousCaller(string method, string path)
    {
        // Not a sample: all four notes routes. A forgotten RequireAuthorization() is invisible in review, and
        // F-016 exists because five routes in this solution served PII to anonymous callers.
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var response = await service.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { content = "x" })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AC1_TheOwningProvider_CanWriteAndReadBackANote()
    {
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var created = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{OwnedAppointment}/notes",
            Owner, TokenFactory.ProviderRole, new { content = "Third session. Shoulder mobility improving." }));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var read = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/booking/appointments/{OwnedAppointment}/notes",
            Owner, TokenFactory.ProviderRole));

        // F-019-T06: the response is now wrapped in DataResponse<T> (ADR-049) -- the notes moved from
        // the root to .data, but the assertions below are unchanged.
        var notesWrapper = await read.Content.ReadFromJsonAsync<DataResponse<List<NoteEntity>>>(HarnessJson.Options);
        var note = Assert.Single(notesWrapper!.Data!);
        Assert.Equal("Third session. Shoulder mobility improving.", note.Content);

        // The provider email came from the TOKEN, not the request — the body carried neither field.
        Assert.Equal(Owner, note.ProviderEmail);
        Assert.Equal(OwnedAppointment, note.AppointmentIdentifier);

        // ⚠️ This is also the first time NoteEntity has ever been serialised to MongoDB: it was written by
        // F-008 and never persisted, because nothing registered a repository for it. The read-back is the
        // proof its BSON mapping works, which no unit test could give.
        Assert.NotEqual(ObjectId.Empty, note.Id);
    }

    [Fact]
    public async Task T201_AProviderCannotReadAnotherProvidersNotes()
    {
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        // The other provider's appointment identifier is not a secret — a customer receives it in their own
        // appointment responses — so this is the attack the guard has to stop.
        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/booking/appointments/{ForeignAppointment}/notes",
            Owner, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task T201_AProviderCannotWriteANoteOnAnotherProvidersAppointment()
    {
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{ForeignAppointment}/notes",
            Owner, TokenFactory.ProviderRole, new { content = "not mine to write" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await service.Database.GetCollection<NoteEntity>("notes")
            .CountDocumentsAsync(Builders<NoteEntity>.Filter.Empty));
    }

    [Fact]
    public async Task AC10_ACustomerCannotTouchNotesAtAll()
    {
        // The customer the notes are ABOUT, on their own appointment. Notes are the provider's clinical record,
        // not a shared document — and this is the only route family in F-014 where the subject of the data is
        // deliberately not a reader.
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var read = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/booking/appointments/{OwnedAppointment}/notes",
            TheCustomer, TokenFactory.CustomerRole));

        var write = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{OwnedAppointment}/notes",
            TheCustomer, TokenFactory.CustomerRole, new { content = "let me in" }));

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task T202_ANoteThatDoesNotExist_AndOneBelongingToSomebodyElse_AnswerIdentically()
    {
        // AC-12. For a therapist, the existence of a note implies the session happened and was noteworthy, so
        // "no such note" and "not your note" must be one answer. Distinguishing them turns the route into an
        // enumeration oracle over ObjectIds that leak into logs, screenshots and shared URLs.
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var foreignNote = new NoteEntity
        {
            Id = ObjectId.GenerateNewId(),
            ProviderEmail = OtherProvider,
            AppointmentIdentifier = ForeignAppointment,
            Content = "somebody else's clinical note"
        };
        await service.Database.GetCollection<NoteEntity>("notes").InsertOneAsync(foreignNote);

        var missing = await service.Client.SendAsync(Authorised(
            HttpMethod.Put, "api/v1/booking/notes/000000000000000000000000",
            Owner, TokenFactory.ProviderRole, new { content = "edit" }));

        var foreign = await service.Client.SendAsync(Authorised(
            HttpMethod.Put, $"api/v1/booking/notes/{foreignNote.Id}",
            Owner, TokenFactory.ProviderRole, new { content = "edit" }));

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(foreign.StatusCode, missing.StatusCode);
        Assert.Equal(
            await Normalised(missing),
            await Normalised(foreign));

        // And the foreign note is untouched.
        var stored = await service.Database.GetCollection<NoteEntity>("notes")
            .Find(Builders<NoteEntity>.Filter.Eq(n => n.Id, foreignNote.Id)).SingleAsync();
        Assert.Equal("somebody else's clinical note", stored.Content);
    }

    [Fact]
    public async Task AnOwnedNote_CanBeEditedAndDeletedByItsProvider()
    {
        // The permissive half. Without it, a guard that refused EVERYTHING would satisfy every test above.
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var created = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{OwnedAppointment}/notes",
            Owner, TokenFactory.ProviderRole, new { content = "first draft" }));
        // F-019-T06: DataResponse<T> envelope -- same note as AC1's read-back above.
        var note = (await created.Content.ReadFromJsonAsync<DataResponse<NoteEntity>>(HarnessJson.Options))!.Data;

        var edited = await service.Client.SendAsync(Authorised(
            HttpMethod.Put, $"api/v1/booking/notes/{note!.Id}",
            Owner, TokenFactory.ProviderRole, new { content = "corrected" }));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("corrected",
            (await edited.Content.ReadFromJsonAsync<DataResponse<NoteEntity>>(HarnessJson.Options))!.Data!.Content);

        var deleted = await service.Client.SendAsync(Authorised(
            HttpMethod.Delete, $"api/v1/booking/notes/{note.Id}", Owner, TokenFactory.ProviderRole));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(0, await service.Database.GetCollection<NoteEntity>("notes")
            .CountDocumentsAsync(Builders<NoteEntity>.Filter.Empty));
    }

    [Fact]
    public async Task AnEmptyNoteIsRejected()
    {
        using var service = await StartWithTwoProvidersAppointmentsAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{OwnedAppointment}/notes",
            Owner, TokenFactory.ProviderRole, new { content = "   " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A response body with the per-request correlation fields removed, so two can be compared.</summary>
    private static async Task<string> Normalised(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        var node = System.Text.Json.Nodes.JsonNode.Parse(body)!.AsObject();
        node.Remove("requestId");
        node.Remove("traceId");
        return node.ToJsonString();
    }
}
