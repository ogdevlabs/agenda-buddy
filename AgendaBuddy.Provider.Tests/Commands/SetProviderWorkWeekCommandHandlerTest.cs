using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using AgendaBuddy.Provider.Core.Commands;
using AgendaBuddy.Provider.Domain.Commands;
using Moq;
using Xunit;

namespace AgendaBuddy.Provider.Tests.Commands;

/// <summary>
/// Per-weekday working hours.
/// </summary>
/// <remarks>
/// The rules re-checked here are already checked at the API boundary; they live in the handler too so a caller
/// reaching it directly gets the same answer. An unusable window is REFUSED, never clamped: silently correcting it
/// leaves the provider looking at hours they did not choose, and silently accepting it makes them unbookable that
/// day with nothing to explain why.
/// </remarks>
public class SetProviderWorkWeekCommandHandlerTest
{
    private const string Email = "coach@example.com";

    private static SetProviderWorkWeekCommandHandler Handler(
        Mock<IProviderService> providers, Mock<IEventStore>? eventStore = null) =>
        new(providers.Object, (eventStore ?? new Mock<IEventStore>()).Object);

    private static Mock<IProviderService> Providers(ProviderEntity? updated = null)
    {
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.SetWorkWeekAsync(It.IsAny<string>(), It.IsAny<List<WorkDayHours>>()))
                 .ReturnsAsync(updated ?? new ProviderEntity
                 {
                     FirstName = "Demo",
                     LastName = "Coach",
                     Email = Email
                 });
        return providers;
    }

    private static SetProviderWorkWeekCommand Command(params WorkDayHours[] days) =>
        new() { Email = Email, Days = [.. days] };

    [Fact]
    public async Task AUsableWeekIsStored()
    {
        var providers = Providers();

        var result = await Handler(providers).Handle(
            Command(
                new WorkDayHours(DayOfWeek.Monday, 9, 17),
                new WorkDayHours(DayOfWeek.Sunday, null, null, isClosed: true)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        providers.Verify(
            p => p.SetWorkWeekAsync(Email, It.Is<List<WorkDayHours>>(days => days.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task AnEmptyWeekIsRefused()
    {
        var providers = Providers();

        var result = await Handler(providers).Handle(
            new SetProviderWorkWeekCommand { Email = Email, Days = [] }, CancellationToken.None);

        Assert.True(result.IsFailed);
        providers.Verify(
            p => p.SetWorkWeekAsync(It.IsAny<string>(), It.IsAny<List<WorkDayHours>>()), Times.Never);
    }

    /// <summary>
    /// The refusal NAMES the day, because "some day is wrong" is not something a provider can act on.
    /// </summary>
    [Theory]
    [InlineData(17, 9)]
    [InlineData(9, 9)]
    [InlineData(null, 17)]
    [InlineData(9, null)]
    public async Task AnUnusableOpenDayIsRefusedByName(int? startHour, int? endHour)
    {
        var providers = Providers();

        var result = await Handler(providers).Handle(
            Command(new WorkDayHours(DayOfWeek.Wednesday, startHour, endHour)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("Wednesday", result.Errors[0].Message);
        providers.Verify(
            p => p.SetWorkWeekAsync(It.IsAny<string>(), It.IsAny<List<WorkDayHours>>()), Times.Never);
    }

    /// <summary>A closed day has no window to validate, so hours are irrelevant to it.</summary>
    [Fact]
    public async Task AClosedDayNeedsNoUsableWindow()
    {
        var providers = Providers();

        var result = await Handler(providers).Handle(
            Command(new WorkDayHours(DayOfWeek.Sunday, null, null, isClosed: true)), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// Hours stored alongside a closed day are kept, so re-opening it does not mean re-entering them — and they
    /// must not make the day fail validation while it is closed.
    /// </summary>
    [Fact]
    public async Task AClosedDayMayKeepEvenAnUnusablePair()
    {
        var providers = Providers();

        var result = await Handler(providers).Handle(
            Command(new WorkDayHours(DayOfWeek.Sunday, 17, 9, isClosed: true)), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// A duplicate weekday is a client bug, and picking one silently would store hours nobody chose.
    /// </summary>
    [Fact]
    public async Task ADuplicateWeekdayIsRefusedByName()
    {
        var providers = Providers();

        var result = await Handler(providers).Handle(
            Command(
                new WorkDayHours(DayOfWeek.Monday, 9, 11),
                new WorkDayHours(DayOfWeek.Monday, 15, 18)),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("Monday", result.Errors[0].Message);
        providers.Verify(
            p => p.SetWorkWeekAsync(It.IsAny<string>(), It.IsAny<List<WorkDayHours>>()), Times.Never);
    }

    [Fact]
    public async Task AMissingProviderFails()
    {
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.SetWorkWeekAsync(It.IsAny<string>(), It.IsAny<List<WorkDayHours>>()))
                 .ReturnsAsync((ProviderEntity?)null);

        var result = await Handler(providers).Handle(
            Command(new WorkDayHours(DayOfWeek.Monday, 9, 17)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("No provider found", result.Errors[0].Message);
    }

    [Fact]
    public async Task EverySuccessAndFailureIsAudited()
    {
        var eventStore = new Mock<IEventStore>();

        await Handler(Providers(), eventStore).Handle(
            Command(new WorkDayHours(DayOfWeek.Monday, 9, 17)), CancellationToken.None);
        await Handler(Providers(), eventStore).Handle(
            Command(new WorkDayHours(DayOfWeek.Monday, 17, 9)), CancellationToken.None);

        eventStore.Verify(
            e => e.SaveAsync(It.Is<Event>(ev =>
                ev.Status == "Success" && ev.Type == nameof(SetProviderWorkWeekCommand))),
            Times.Once);
        eventStore.Verify(
            e => e.SaveAsync(It.Is<Event>(ev =>
                ev.Status == "Failed" && ev.Type == nameof(SetProviderWorkWeekCommand))),
            Times.Once);
    }

    [Fact]
    public async Task ANullRequestThrows() =>
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Handler(Providers()).Handle(null!, CancellationToken.None));
}
