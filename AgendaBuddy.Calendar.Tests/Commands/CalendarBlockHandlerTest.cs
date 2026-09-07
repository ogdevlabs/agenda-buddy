using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgendaBuddy.Calendar.Core.Commands;
using AgendaBuddy.Calendar.Core.Queries;
using AgendaBuddy.Calendar.Domain.Commands;
using AgendaBuddy.Calendar.Domain.Queries;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.Calendar.Tests.Commands;

/// <summary>
/// A provider's time off.
/// </summary>
/// <remarks>
/// The rule worth reading here is the conflict refusal. Blocking over existing bookings silently is the trap: the
/// slot disappears from availability while the customer still holds a session nobody has told them about. So the
/// default answer names what is in the way, and forcing it is a deliberate second act that leaves those sessions
/// standing for the provider to deal with.
/// </remarks>
public class CalendarBlockHandlerTest
{
    private const string Email = "coach@example.com";

    private static readonly DateTime Start = new(2026, 9, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 9, 14, 17, 0, 0, DateTimeKind.Utc);

    private static Mock<ICalendarBlockService> Blocks(
        IEnumerable<AppointmentEntity>? conflicts = null, bool blockSucceeds = true)
    {
        var blocks = new Mock<ICalendarBlockService>();

        blocks.Setup(b => b.GetAffectedAppointmentsAsync(
                  It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
              .ReturnsAsync(conflicts ?? []);

        blocks.Setup(b => b.BlockAsync(
                  It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
              .ReturnsAsync(blockSucceeds
                  ? new CalendarBlockEntity(Email, Start, End, "Holiday")
                  : null);

        blocks.Setup(b => b.RemoveBlockAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        blocks.Setup(b => b.GetBlocksAsync(It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        return blocks;
    }

    private static AppointmentEntity Appointment() => new()
    {
        EmailProvider = Email,
        EmailCustomer = "ada@example.com",
        Start = Start.AddHours(1),
        End = Start.AddHours(2),
        AppointmentStatus = AppointmentStatus.Booked
    };

    private static BlockCalendarCommand Command(bool force = false) => new()
    {
        EmailProvider = Email,
        StartUtc = Start,
        EndUtc = End,
        Reason = "Holiday",
        Force = force
    };

    [Fact]
    public async Task AClearRangeIsBlocked()
    {
        var blocks = Blocks();
        var handler = new BlockCalendarCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        blocks.Verify(b => b.BlockAsync(Email, Start, End, "Holiday"), Times.Once);
    }

    /// <summary>
    /// Refused by default, and the refusal names HOW MANY — a count only the server holds, and the reason the
    /// provider is told the cost rather than discovering it after the fact.
    /// </summary>
    [Fact]
    public async Task ARangeWithLiveSessionsIsRefusedAndCountsThem()
    {
        var blocks = Blocks(conflicts: [Appointment(), Appointment()]);
        var handler = new BlockCalendarCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("2 booked sessions", result.Errors[0].Message);
        blocks.Verify(
            b => b.BlockAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task TheRefusalIsSingularForOneSession()
    {
        var blocks = Blocks(conflicts: [Appointment()]);
        var handler = new BlockCalendarCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Contains("1 booked session fall", result.Errors[0].Message);
    }

    /// <summary>
    /// Forcing skips the check entirely: the provider has already been shown the count and accepted it, and the
    /// sessions are deliberately left standing rather than cancelled on their behalf.
    /// </summary>
    [Fact]
    public async Task ForcingBlocksTheRangeWithoutCheckingConflicts()
    {
        var blocks = Blocks(conflicts: [Appointment(), Appointment()]);
        var handler = new BlockCalendarCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(Command(force: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        blocks.Verify(
            b => b.GetAffectedAppointmentsAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    /// <summary>Refused rather than clamped: widening a provider's time off blocks hours they never chose.</summary>
    [Fact]
    public async Task ARangeThatDoesNotEndAfterItStartsIsRefused()
    {
        var blocks = Blocks();
        var handler = new BlockCalendarCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new BlockCalendarCommand { EmailProvider = Email, StartUtc = End, EndUtc = Start },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        blocks.Verify(
            b => b.BlockAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()),
            Times.Never);
    }

    /// <summary>
    /// The audit records the range and the owner but NEVER the reason — it is the provider's private note.
    /// </summary>
    [Fact]
    public async Task TheAuditDoesNotRecordThePrivateReason()
    {
        var eventStore = new Mock<IEventStore>();
        Event? saved = null;
        eventStore.Setup(e => e.SaveAsync(It.IsAny<Event>()))
                  .Callback<Event>(e => saved = e)
                  .Returns(Task.CompletedTask);

        var handler = new BlockCalendarCommandHandler(Blocks().Object, eventStore.Object);
        await handler.Handle(Command(), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.DoesNotContain("Holiday", saved!.Data);
        Assert.Contains(Email, saved.Data);
    }

    [Fact]
    public async Task RemovingABlockSucceedsAndIsAudited()
    {
        var eventStore = new Mock<IEventStore>();
        var handler = new RemoveCalendarBlockCommandHandler(Blocks().Object, eventStore.Object);

        var result = await handler.Handle(
            new RemoveCalendarBlockCommand { EmailProvider = Email, Identifier = "abc" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        eventStore.Verify(
            e => e.SaveAsync(It.Is<Event>(ev =>
                ev.Status == "Success" && ev.Type == nameof(RemoveCalendarBlockCommand))),
            Times.Once);
    }

    [Fact]
    public async Task RemovingSomethingThatIsNotThereFails()
    {
        var blocks = Blocks();
        blocks.Setup(b => b.RemoveBlockAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        var handler = new RemoveCalendarBlockCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new RemoveCalendarBlockCommand { EmailProvider = Email, Identifier = "nope" },
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    /// <summary>
    /// The owning email goes into the removal itself, so it cannot be raced into deleting somebody else's block.
    /// </summary>
    [Fact]
    public async Task RemovalIsScopedToTheOwner()
    {
        var blocks = Blocks();
        var handler = new RemoveCalendarBlockCommandHandler(blocks.Object, Mock.Of<IEventStore>());

        await handler.Handle(
            new RemoveCalendarBlockCommand { EmailProvider = Email, Identifier = "abc" },
            CancellationToken.None);

        blocks.Verify(b => b.RemoveBlockAsync(Email, "abc"), Times.Once);
    }

    [Fact]
    public async Task ConflictsAreReadForTheRangeAsked()
    {
        var blocks = Blocks(conflicts: [Appointment()]);
        var handler = new GetCalendarBlockConflictsQueryHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new GetCalendarBlockConflictsQuery { EmailProvider = Email, StartUtc = Start, EndUtc = End },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        blocks.Verify(b => b.GetAffectedAppointmentsAsync(Email, Start, End), Times.Once);
    }

    [Fact]
    public async Task AConflictQueryOverAnInvalidRangeFails()
    {
        var handler = new GetCalendarBlockConflictsQueryHandler(Blocks().Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new GetCalendarBlockConflictsQuery { EmailProvider = Email, StartUtc = End, EndUtc = Start },
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    /// <summary>
    /// Bounded to blocks that have not finished: past time off is not actionable, and a provider with years of
    /// history would otherwise pay for all of it.
    /// </summary>
    [Fact]
    public async Task ReadingBlocksAsksOnlyForOnesStillInForce()
    {
        var blocks = Blocks();
        var handler = new GetCalendarBlocksQueryHandler(blocks.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new GetCalendarBlocksQuery { EmailProvider = Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        blocks.Verify(
            b => b.GetBlocksAsync(Email, It.Is<DateTime>(from => from > DateTime.UtcNow.AddMinutes(-1))),
            Times.Once);
    }

    [Fact]
    public async Task ANullRequestThrows()
    {
        var handler = new BlockCalendarCommandHandler(Blocks().Object, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, CancellationToken.None));
    }
}
