using System.Reflection;
using System.Text.RegularExpressions;
using AgendaBuddy.MobileApp.Models;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// A control property that binds two-way must target a property the view model can actually be written to.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>This is the bug class that shipped twice in one week, both times silently.</b> A two-way binding whose
/// target has no setter fails at runtime with nothing raised: the control shows the new value because that is its
/// own state, while the write back is dropped and the view model never learns. It looks exactly like a save that
/// did not persist.
/// </para>
/// <list type="number">
/// <item>
/// <c>CalendarSettingsPage</c> bound <c>Switch.IsToggled</c> to <c>WorkDayRow.IsOpen</c>, which was
/// <c>=> !IsClosed</c> — getter only. Closing Saturday and Sunday moved the switches and changed nothing, so Save
/// sent every day as open and reopening the screen showed the weekend enabled again.
/// </item>
/// <item>
/// <c>EditProfilePage</c> gated Save on a <c>CanExecute</c> reading two checkbox-bound booleans, which left the
/// button inert — the same "the control moved, the view model did not" shape.
/// </item>
/// </list>
/// <para>
/// XAML is not compiled on this <c>net10.0</c> slice, so the views are read as text and the property names are
/// resolved against the loaded <c>AgendaBuddy.MobileApp</c> assembly by reflection.
/// </para>
/// </remarks>
public class TwoWayBindingTargetTest
{
    /// <summary>
    /// Control properties whose bindings default to two-way, so a binding to one of them is a WRITE.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b><c>Text</c> is deliberately NOT here.</b> It is two-way on <c>Entry</c>/<c>Editor</c>/<c>SearchBar</c>
    /// and display-only on <c>Label</c>/<c>Button</c>/<c>Span</c>, and this test reads XAML as text without
    /// resolving which control an attribute sits on — so including it reported some seventy read-only labels and
    /// buried the two real defects. Every property below writes back on <b>every</b> control that has it, which is
    /// what makes a hit meaningful rather than something to be allowlisted away.
    /// </remarks>
    private static readonly string[] TwoWayProperties =
    [
        "IsToggled", "IsChecked", "SelectedIndex", "SelectedItem", "SelectedDate",
        "Value", "IsRefreshing", "Date", "Time",
    ];

    /// <summary>
    /// <c>Type.Property</c> pairs whose getter-only match is a NAME COLLISION, not a defect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The BindingContext is not declared in XAML, so a bound name is checked against every type that declares it.
    /// Keyed on the declaring type rather than the bare name deliberately: suppressing <c>IsAllDay</c> outright
    /// would also hide a genuine getter-only <c>IsAllDay</c> somewhere that <i>is</i> two-way-bound.
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <c>CalendarBlock.IsAllDay</c> is computed from the block's own start and end. <c>TimeOffPage</c>'s switch is
    /// page-level and binds <c>TimeOffViewModel.IsAllDay</c>, which is a settable <c>[ObservableProperty]</c>;
    /// <c>CalendarBlock</c> is the list-row model and nothing toggles it.
    /// </item>
    /// <item>
    /// The seven <c>*ViewModel.IsProvider</c> properties are computed off the session. <c>RegisterPage</c>'s radio
    /// binds <c>RegisterViewModel.IsProvider</c>, which is settable. ⚠️ That role picker <i>is</i> broken, for
    /// different reasons — ungrouped radios and a hardcoded selected look — tracked as <c>agenda-buddy-1ja</c>.
    /// </item>
    /// </list>
    /// </remarks>
    private static readonly HashSet<string> NameCollisions = new(StringComparer.Ordinal)
    {
        "CalendarBlock.IsAllDay",
        "CalendarSettingsViewModel.IsProvider",
        "CalendarViewModel.IsProvider",
        "DashboardViewModel.IsProvider",
        "PaymentAccountViewModel.IsProvider",
        "ProfessionsViewModel.IsProvider",
        "ProfileViewModel.IsProvider",
        "TimeOffViewModel.IsProvider",
        "UserSessionService.IsProvider",
    };

    private static readonly Regex BindingTarget = new(
        @"(?<prop>[A-Za-z]+)\s*=\s*""\{\s*Binding\s+(?:Path\s*=\s*)?(?<target>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    [Fact]
    public void EveryTwoWayBindingTargetsAWritableProperty()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in XamlFiles())
        {
            foreach (var match in BindingTarget.Matches(File.ReadAllText(file)).Cast<Match>())
            {
                var controlProperty = match.Groups["prop"].Value;
                var target = match.Groups["target"].Value;

                if (!TwoWayProperties.Contains(controlProperty, StringComparer.Ordinal)) continue;

                foreach (var declaring in TypesDeclaring(target))
                {
                    if (NameCollisions.Contains($"{declaring.Name}.{target}")) continue;

                    var property = declaring.GetProperty(
                        target, BindingFlags.Public | BindingFlags.Instance);

                    if (property is null || property.CanWrite) continue;

                    offenders.Add(
                        $"{Path.GetFileName(file)}: {controlProperty}=\"{{Binding {target}}}\" -> "
                        + $"{declaring.Name}.{target} has no setter");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "These bindings write to a property with no setter. The control will move and the view model will "
            + "never learn, with nothing raised — it looks like a save that did not persist. Give the property a "
            + "setter, or bind a writable one: "
            + string.Join("; ", offenders));
    }

    /// <summary>
    /// The allowlist names only real, still-getter-only properties — so an entry cannot quietly outlive the
    /// collision it documents and start hiding a genuine defect.
    /// </summary>
    [Fact]
    public void TheNameCollisionAllowlistIsNotStale()
    {
        var stale = new List<string>();

        foreach (var entry in NameCollisions)
        {
            var (typeName, propertyName) = (entry.Split('.')[0], entry.Split('.')[1]);

            var declaring = typeof(WorkDayRow).Assembly.GetTypes()
                .FirstOrDefault(type => type.Name == typeName);

            var property = declaring?.GetProperty(
                propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (property is null || property.CanWrite) stale.Add(entry);
        }

        Assert.True(stale.Count == 0,
            "These allowlist entries no longer name a getter-only property, so they suppress nothing and could "
            + $"hide a real defect. Remove them: {string.Join(", ", stale)}");
    }

    /// <summary>The specific regression: the day toggle has to be able to close a day.</summary>
    [Fact]
    public void TheWorkDayOpenToggleCanActuallyCloseADay()
    {
        var row = new WorkDayRow(DayOfWeek.Saturday, 9, 17, isClosed: false);

        row.IsOpen = false;

        Assert.True(row.IsClosed);
        Assert.False(row.IsOpen);
    }

    [Fact]
    public void TheWorkDayOpenToggleCanReopenADayAndKeepsItsHours()
    {
        var row = new WorkDayRow(DayOfWeek.Saturday, 9, 17, isClosed: true);

        row.IsOpen = true;

        Assert.False(row.IsClosed);
        // Re-opening must not mean re-entering the hours — that is why IsClosed is separate from them.
        Assert.Equal(9, row.StartHour);
        Assert.Equal(17, row.EndHour);
    }

    /// <summary>
    /// Types the binding could plausibly resolve against. The BindingContext is not declared in the XAML, so every
    /// declaring type is checked — a getter-only property of that name anywhere is worth reporting.
    /// </summary>
    private static IEnumerable<Type> TypesDeclaring(string propertyName) =>
        typeof(WorkDayRow).Assembly
            .GetTypes()
            .Where(type => type.IsClass && type.IsPublic)
            .Where(type => type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                               ?.DeclaringType == type);

    private static IEnumerable<string> XamlFiles()
    {
        var root = MobileAppRoot();
        return Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.xaml")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "Controls"), "*.xaml"));
    }

    private static string MobileAppRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "AgendaBuddy.MobileApp");
    }
}
