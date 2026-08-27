namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// F-018-T13 / AC-15. A PERMANENT structural guard: fails if a future change removes the
/// <c>eventStore.SaveAsync(</c> call from any command or query handler's path, without needing an edit
/// every time a new handler is added.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why structural rather than another HTTP round trip.</b> <see cref="BookingAuditTest"/> and its five
/// siblings each pin ONE reachable route's audit behaviour, which is real coverage but not a general net —
/// several handlers in this kernel have no reachable HTTP route at all (<c>DeactivateProviderCommandHandler
/// </c>'s own failure branch is unreachable even though its success branch has a route;
/// <c>UpdateAppointmentCommandHandler</c> and <c>CancelAppointmentCommandHandler</c> were never exercised by
/// an AC-7 test in this task). A handler-by-handler HTTP test for every one of them would multiply this
/// task's test count without changing what it proves: CONSTITUTION §3's mandate is about the SOURCE, not
/// about any one route. Scanning every handler file directly is what makes the guard "fail when the write
/// is removed" rather than "fail when a specific route stops auditing" — and it is what the task's own
/// framing (a reflection/convention-based check over an explicit HTTP test per handler) points at.
/// </para>
/// <para>
/// <b>Convention, not a maintained list.</b> Every <c>*CommandHandler.cs</c>/<c>*QueryHandler.cs</c> under
/// <see cref="ScanRoots"/> (<c>AgendaBuddy.EventAndCommands/</c>, and — as of F-019-T03 — <c>AgendaBuddy.Booking.Core/</c>, the
/// first handler location outside <c>AgendaBuddy.EventAndCommands</c>) is discovered by directory walk
/// (<c>SearchOption.AllDirectories</c>), so a new handler is covered automatically — no edit here is
/// needed when a task adds one under an already-listed root. **F-019-T07 confirmed this directly**: all
/// 10 of AgendaBuddy.Booking.Core's handlers (T03's 3 moved originals, T04's rename, T05's 6 freshly-authored F-014
/// handlers, none from T06 — it only rewires routes, adds no handler files) are found with zero further
/// edits here, because the recursive walk needs no per-handler awareness. A genuinely new root (e.g.
/// F-020 moving another service's handlers into its own Core project) still needs adding to
/// <see cref="ScanRoots"/> when that happens — out of scope for F-019, which only touches Booking. Only
/// <see cref="ExcludedHandlerFiles"/> is a maintained list, and it holds exactly one documented exception.
/// </para>
/// <para>
/// <b>The one exclusion, retired by F-020-T08.</b> <c>BookCalendarCommandHandler.cs</c> took no
/// <c>IEventStore</c> at all and its <c>Handle</c> unconditionally threw <c>NotImplementedException</c> —
/// there was no result, success or failure, for CONSTITUTION §3 to apply to, and it had no HTTP route
/// either (<c>15-cqrs-and-messaging.md</c>'s command inventory), so nothing could ever reach it to find
/// out. F-020-T08 deleted the file outright rather than moving dead code into
/// <c>AgendaBuddy.Calendar.Core</c> alongside Calendar's two real, moved query handlers — see that task's
/// own report for the disclosure. <see cref="ExcludedHandlerFiles"/> is now empty; kept as a named,
/// documented list rather than removed, so a future genuine exclusion has an obvious place to go.
/// </para>
/// <para>
/// <b>Mutation-tested once, by hand, as AC-15 requires.</b> Recorded in this task's final report rather than
/// left in the repository: <c>eventStore.SaveAsync</c> was temporarily commented out of
/// <c>BookingAppointmentCommandHandler</c>'s success branch, this test went red naming that exact file, and
/// it went green again once the line was restored.
/// </para>
/// </remarks>
public class EventStoreWriteGuardTest
{
    /// <summary>
    /// Deliberately empty as of F-020-T08 — see this class's remarks on <c>BookCalendarCommandHandler.cs</c>,
    /// the one former entry, which was deleted rather than moved.
    /// </summary>
    private static readonly string[] ExcludedHandlerFiles = [];

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
        }

        return current.FullName;
    }

    // F-019-T03. AgendaBuddy.Booking.Core is the first handler location outside AgendaBuddy.EventAndCommands — its 3
    // moved handlers (Book/Update/Cancel) would otherwise silently drop out of this guard's
    // coverage. F-019-T07 confirmed this single root already covers every handler T04/T05 added
    // afterward (10 total in AgendaBuddy.Booking.Core as of F-019) with no further edits -- the recursive
    // directory walk needs no per-handler awareness.
    //
    // F-020-T08: AgendaBuddy.Calendar.Core is the second such root, added for the same reason -- its 2 moved
    // query handlers (CheckCalendarAvailability/CheckCalendarAppointments) would otherwise silently drop
    // out of coverage. A new root is only needed when another service's handlers move to their own Core
    // project.
    private static readonly string[] ScanRoots =
        ["AgendaBuddy.EventAndCommands", "AgendaBuddy.Booking.Core", "AgendaBuddy.Calendar.Core"];

    private static List<string> HandlerFiles()
    {
        var root = RepoRoot();

        return ScanRoots
            .Select(scanRoot => Path.Combine(root, scanRoot))
            .Where(Directory.Exists)
            .SelectMany(scanRoot => Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
            // Excludes bin/obj and any dotfolder — a parallel worktree's build output must never leak into
            // this scan (a recurring class of defect in this repo's structural tests).
            .Where(path => Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar)
                .All(segment => segment is not ("bin" or "obj") && !segment.StartsWith('.')))
            .Where(path => Path.GetFileName(path).EndsWith("CommandHandler.cs", StringComparison.Ordinal)
                        || Path.GetFileName(path).EndsWith("QueryHandler.cs", StringComparison.Ordinal))
            .Where(path => !ExcludedHandlerFiles.Contains(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void TheHandlerScanItselfFindsHandlers()
    {
        // Guards the guard: if this glob ever matches nothing (a directory rename, a naming convention
        // change), every assertion below would vacuously pass and CONSTITUTION §3 would have no test at
        // all watching it. Twenty as of F-020-T08: BookCalendarCommandHandler.cs (previously excluded,
        // never counted here) was deleted rather than moved, so the count is unchanged by Calendar's move.
        Assert.True(HandlerFiles().Count >= 20, $"expected at least 20 handler files, found {HandlerFiles().Count}");
    }

    [Theory]
    [MemberData(nameof(HandlerFileNames))]
    public void AC15_EveryCommandOrQueryHandler_CallsEventStoreSaveAsync(string handlerFilePath)
    {
        var content = File.ReadAllText(handlerFilePath);

        Assert.Contains(
            "eventStore.SaveAsync(",
            content,
            StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> HandlerFileNames() =>
        HandlerFiles().Select(path => new object[] { path });
}
