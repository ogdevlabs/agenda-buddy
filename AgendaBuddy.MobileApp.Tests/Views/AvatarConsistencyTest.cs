using System.Xml.Linq;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// One person, one mark, on every surface that shows them.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because two surfaces already disagreed.</b> Contacts resolved
/// <c>AvatarCatalog.Resolve(assignedId, email)</c> — honouring the mark the server assigned — while Messages
/// called <c>AvatarCatalog.Deterministic(email)</c> unconditionally. Its comment claimed the two agreed, and they
/// do only for an account with no assigned avatar; every account created since the handlers started assigning one
/// is the case where they differ. Meanwhile the Dashboard showed the first letter of a name, so the same person
/// was identified three different ways.
/// </para>
/// <para>
/// Everything now goes through <see cref="AvatarSource"/> for the image and <c>Controls/Avatar.xaml</c> for the
/// visual.
/// </para>
/// </remarks>
public class AvatarConsistencyTest
{
    private const string Email = "ada@example.com";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    /// <summary>Views that show a person and must therefore show their avatar.</summary>
    public static TheoryData<string> ViewsShowingAPerson => new()
    {
        "MessagingPage.xaml",
        "CustomersPage.xaml",
        "DashboardPage.xaml",
        "CalendarPage.xaml",
        "AppointmentDetailPage.xaml"
    };

    /// <summary>
    /// Every one of them uses the shared control, so the visual cannot drift per surface.
    /// </summary>
    [Theory]
    [MemberData(nameof(ViewsShowingAPerson))]
    public void AViewShowingAPersonUsesTheSharedAvatarControl(string viewName)
    {
        var root = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", viewName)).Root;
        Assert.NotNull(root);

        var avatars = root!.Descendants().Count(element => element.Name.LocalName == "Avatar");

        Assert.True(
            avatars > 0,
            $"{viewName} shows a person but no <controls:Avatar>. Every surface that names somebody has to show "
            + "the same mark for them, or the app identifies one person three different ways.");
    }

    /// <summary>
    /// And binds it to a model's own <c>AvatarAsset</c>, which is the property that routes through
    /// <see cref="AvatarSource"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ViewsShowingAPerson))]
    public void TheAvatarIsBoundToAResolvedAsset(string viewName)
    {
        var root = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", viewName)).Root;

        var sources = root!.Descendants()
            .Where(element => element.Name.LocalName == "Avatar")
            .Select(element => element.Attribute("Source")?.Value ?? string.Empty)
            .ToList();

        Assert.NotEmpty(sources);
        Assert.All(sources, source => Assert.Contains("AvatarAsset", source));
    }

    /// <summary>
    /// No surface may hand-roll the circular clip any more — that is how the two copies drifted apart.
    /// </summary>
    [Theory]
    [MemberData(nameof(ViewsShowingAPerson))]
    public void NoViewHandRollsItsOwnAvatarImage(string viewName)
    {
        var root = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", viewName)).Root;

        var handRolled = root!.Descendants()
            .Where(element => element.Name.LocalName == "Image")
            .Select(element => element.Attribute("Source")?.Value ?? string.Empty)
            .Where(source => source.Contains("AvatarAsset"))
            .ToList();

        Assert.Empty(handRolled);
    }

    // ── The resolution itself ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An assigned id wins. This is the case the old messaging path got wrong, and the reason the same person
    /// showed one mark in Messages and another in Contacts.
    /// </summary>
    [Fact]
    public void AnAssignedIdIsHonouredOverTheEmailDerivation()
    {
        var assigned = AvatarSource.For("avatar_03", Email);
        var derived = AvatarSource.FromEmail(Email);

        Assert.Equal("avatar_03.png", assigned);
        Assert.NotEqual(derived, assigned);
    }

    /// <summary>
    /// A row with no assigned mark still gets a stable one, which is what let avatars ship with no migration.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnAbsentIdFallsBackToAStableDerivation(string? assignedId)
    {
        var first = AvatarSource.For(assignedId, Email);
        var second = AvatarSource.For(assignedId, Email);

        Assert.Equal(first, second);
        Assert.EndsWith(".png", first);
        Assert.NotEqual(".png", first);
    }

    /// <summary>
    /// An id from a future build with a larger catalogue is treated as absent rather than honoured — an unknown
    /// asset name renders as an empty circle, not an error.
    /// </summary>
    [Fact]
    public void AnUnknownIdFallsBackRatherThanRenderingNothing()
    {
        var unknown = AvatarSource.For("avatar_999", Email);

        Assert.Equal(AvatarSource.FromEmail(Email), unknown);
    }

    /// <summary>
    /// The three models that carry a person all agree, given the same inputs. They are read by different
    /// surfaces, so a difference here is a difference the user sees.
    /// </summary>
    [Fact]
    public void EveryModelResolvesTheSamePersonToTheSameMark()
    {
        var contact = new CustomerSummary { Email = Email, AvatarId = "avatar_05" };
        var thread = new MessageThreadStub
        {
            OtherPartyEmail = Email,
            OtherPartyAvatarId = "avatar_05"
        };
        var appointment = new AppointmentDetail { ContactEmail = Email, ContactAvatarId = "avatar_05" };
        var summary = new AppointmentSummary { ContactEmail = Email, ContactAvatarId = "avatar_05" };

        Assert.Equal("avatar_05.png", contact.AvatarAsset);
        Assert.Equal(contact.AvatarAsset, thread.AvatarAsset);
        Assert.Equal(contact.AvatarAsset, appointment.AvatarAsset);
        Assert.Equal(contact.AvatarAsset, summary.AvatarAsset);
    }

    /// <summary>
    /// And they still agree when no assigned id is available — which is the situation messaging is normally in,
    /// since the message wire carries participant emails and nothing else.
    /// </summary>
    [Fact]
    public void EveryModelAgreesOnThePersonWithNoAssignedMark()
    {
        var contact = new CustomerSummary { Email = Email };
        var thread = new MessageThreadStub { OtherPartyEmail = Email };
        var appointment = new AppointmentDetail { ContactEmail = Email };
        var summary = new AppointmentSummary { ContactEmail = Email };

        Assert.Equal(contact.AvatarAsset, thread.AvatarAsset);
        Assert.Equal(contact.AvatarAsset, appointment.AvatarAsset);
        Assert.Equal(contact.AvatarAsset, summary.AvatarAsset);
    }

    /// <summary>
    /// Different people get different marks often enough for the catalogue to be doing its job — a resolver that
    /// collapsed everyone onto one avatar would satisfy every assertion above.
    /// </summary>
    [Fact]
    public void DifferentPeopleGetDifferentMarks()
    {
        var marks = Enumerable.Range(0, 40)
            .Select(index => AvatarSource.FromEmail($"person{index}@example.com"))
            .Distinct()
            .Count();

        Assert.True(marks > 1, "every address resolved to the same avatar");
    }
}
