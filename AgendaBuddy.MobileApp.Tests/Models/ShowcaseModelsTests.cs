using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Models;

/// <summary>
/// Every body here is copied from the API contract, not written in the shape the client would like, so a field
/// name that drifts from the wire fails here instead of rendering an empty screen.
/// </summary>
public class ShowcaseModelsTests
{
    private const string MineJson = """
        { "success": true, "data": {
          "providerRef": "66f1a2b3c4d5e6f708192a3b", "publicCode": "K7Q2X9",
          "tagline": "Fine-line tattoos, Guadalajara", "about": "Ten years of fine-line work.",
          "photoHash": "aa11", "logoHash": null,
          "portfolio": [ { "hash": "bb22", "caption": "Botanical sleeve", "serviceId": "svc1", "width": 1600, "height": 1200, "addedAt": "2026-09-30T18:02:11Z" } ],
          "completeness": { "done": 3, "total": 5, "missing": ["logo", "about"] },
          "funnel": { "scans": 41, "opened": 17, "booked": 4, "windowDays": 7 }
        } }
        """;

    private const string ViewJson = """
        { "success": true, "data": {
          "providerRef": "66f1a2b3c4d5e6f708192a3b", "firstName": "Mariana", "lastName": "Ruiz",
          "professions": ["Tattoo Artist"],
          "tagline": "Fine-line", "about": "About text", "photoHash": "aa11", "logoHash": "cc33", "avatarId": "geo-07",
          "portfolio": [ { "hash": "bb22", "caption": "Leaf", "serviceId": "svc1", "width": 1600, "height": 1200, "isNew": true } ],
          "services": [ { "id": "svc1", "name": "Fine-line piece", "fee": 1800, "feeType": "Fixed", "durationMinutes": 120 } ],
          "relationship": {
            "isSelf": false, "isSubscribed": true,
            "nextAppointment": { "identifier": "appt-1", "scheduledAt": "2026-10-12T16:00:00Z", "serviceName": "Fine-line piece" },
            "hasBookedBefore": true
          }
        } }
        """;

    [Fact]
    public void MyShowcaseReadsEveryContractField()
    {
        var mine = ShowcaseApiService.ReadData<MyShowcase>(MineJson)!;

        Assert.Equal("66f1a2b3c4d5e6f708192a3b", mine.ProviderRef);
        Assert.Equal("K7Q2X9", mine.PublicCode);
        Assert.Equal("Fine-line tattoos, Guadalajara", mine.Tagline);
        Assert.Equal("Ten years of fine-line work.", mine.About);
        Assert.Equal("aa11", mine.PhotoHash);
        Assert.Null(mine.LogoHash);

        var item = Assert.Single(mine.Portfolio);
        Assert.Equal("bb22", item.Hash);
        Assert.Equal("Botanical sleeve", item.Caption);
        Assert.Equal("svc1", item.ServiceId);
        Assert.Equal(1600, item.Width);
        Assert.Equal(1200, item.Height);
        Assert.Equal(new DateTime(2026, 9, 30, 18, 2, 11, DateTimeKind.Utc), item.AddedAt!.Value.ToUniversalTime());

        Assert.Equal(3, mine.Completeness.Done);
        Assert.Equal(5, mine.Completeness.Total);
        Assert.True(mine.Completeness.IsMissing("logo"));
        Assert.True(mine.Completeness.IsMissing("About"));
        Assert.False(mine.Completeness.IsMissing("photo"));

        Assert.Equal(41, mine.Funnel.Scans);
        Assert.Equal(17, mine.Funnel.Opened);
        Assert.Equal(4, mine.Funnel.Booked);
        Assert.Equal(7, mine.Funnel.WindowDays);
    }

    [Fact]
    public void AnUncreatedCodeIsNull()
    {
        var mine = ShowcaseApiService.ReadData<MyShowcase>(
            """{ "success": true, "data": { "providerRef": "66f1", "publicCode": null, "portfolio": [] } }""")!;

        Assert.Null(mine.PublicCode);
        Assert.Empty(mine.Portfolio);
    }

    [Fact]
    public void ShowcaseViewReadsEveryContractField()
    {
        var view = ShowcaseApiService.ReadData<ShowcaseView>(ViewJson)!;

        Assert.Equal("Mariana", view.FirstName);
        Assert.Equal("Ruiz", view.LastName);
        Assert.Equal("Mariana Ruiz", view.FullName);
        Assert.Equal(["Tattoo Artist"], view.Professions);
        Assert.Equal("cc33", view.LogoHash);
        Assert.Equal("geo-07", view.AvatarId);
        Assert.True(Assert.Single(view.Portfolio).IsNew);

        var service = Assert.Single(view.Services);
        Assert.Equal("svc1", service.Id);
        Assert.Equal("Fine-line piece", service.Name);
        Assert.Equal(1800m, service.Fee);
        Assert.Equal("Fixed", service.FeeType);
        Assert.Equal(120, service.DurationMinutes);

        Assert.False(view.Relationship.IsSelf);
        Assert.True(view.Relationship.IsSubscribed);
        Assert.True(view.Relationship.HasBookedBefore);
        var next = view.Relationship.NextAppointment!;
        Assert.Equal("appt-1", next.Identifier);
        Assert.Equal(new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc), next.ScheduledAt.ToUniversalTime());
        Assert.Equal("Fine-line piece", next.ServiceName);
    }

    [Fact]
    public void AnEmptyShowcaseIsTheDesignedEmptyStateNotAnError()
    {
        var view = ShowcaseApiService.ReadData<ShowcaseView>(
            """{ "success": true, "data": { "providerRef": "66f1", "firstName": "Ana", "lastName": "", "portfolio": [], "services": [], "relationship": { "isSelf": true } } }""")!;

        Assert.Equal("Ana", view.FullName);
        Assert.Empty(view.Portfolio);
        Assert.True(view.Relationship.IsSelf);
        Assert.Null(view.Relationship.NextAppointment);
    }

    [Fact]
    public void PublicCodeUploadLookupAndHiddenReadTheirContractShapes()
    {
        var code = ShowcaseApiService.ReadData<ShowcasePublicCode>(
            """{ "success": true, "data": { "code": "K7Q2X9", "url": "https://go.example/api/v1/go/K7Q2X9" } }""")!;
        Assert.Equal("K7Q2X9", code.Code);
        Assert.Equal("https://go.example/api/v1/go/K7Q2X9", code.Url);

        var upload = ShowcaseApiService.ReadData<MediaUpload>(
            """{ "success": true, "data": { "hash": "9f2ce1", "width": 1600, "height": 1067, "deduplicated": true } }""")!;
        Assert.Equal("9f2ce1", upload.Hash);
        Assert.Equal(1067, upload.Height);
        Assert.True(upload.Deduplicated);

        var lookup = ShowcaseApiService.ReadData<List<ShowcaseLookupEntry>>(
            """[{ "email": "m@x.dev", "providerRef": "66f1", "photoHash": null, "portfolioChangedAt": "2026-09-29T10:00:00Z" }]""")!;
        var entry = Assert.Single(lookup);
        Assert.Equal("m@x.dev", entry.Email);
        Assert.Null(entry.PhotoHash);
        Assert.NotNull(entry.PortfolioChangedAt);

        var hidden = ShowcaseApiService.ReadData<List<HiddenProvider>>(
            """{ "success": true, "data": [{ "providerRef": "66f1", "firstName": "Mariana", "lastName": "Ruiz" }] }""")!;
        Assert.Equal("Mariana Ruiz", Assert.Single(hidden).FullName);
    }

    [Theory]
    [InlineData(ShowcaseReportReason.Inappropriate, "inappropriate")]
    [InlineData(ShowcaseReportReason.NotTheirWork, "not_their_work")]
    [InlineData(ShowcaseReportReason.Spam, "spam")]
    [InlineData(ShowcaseReportReason.Other, "other")]
    public void ReportReasonsUseTheContractWireValues(ShowcaseReportReason reason, string wire) =>
        Assert.Equal(wire, ShowcaseReportReasons.WireValue(reason));

    [Fact]
    public void EveryReportReasonHasAWireValue() =>
        Assert.Equal(4, Enum.GetValues<ShowcaseReportReason>().Select(ShowcaseReportReasons.WireValue).Distinct().Count());
}
