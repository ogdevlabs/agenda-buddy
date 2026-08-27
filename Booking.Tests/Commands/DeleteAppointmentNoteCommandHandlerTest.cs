namespace Booking.Tests.Commands;

public class DeleteAppointmentNoteCommandHandlerTest
{
    [Fact]
    public async Task Handle_DelegatesToNoteService_ReturnsOk()
    {
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.DeleteAsync("note1", "provider@example.com")).Returns(Task.CompletedTask);
        var handler = new DeleteAppointmentNoteCommandHandler(notes.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new DeleteAppointmentNoteCommand { Id = "note1", ProviderEmail = "provider@example.com" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        notes.Verify(n => n.DeleteAsync("note1", "provider@example.com"), Times.Once);
    }

    [Fact]
    public async Task Handle_NoteBelongsToAnotherProvider_PropagatesUnauthorizedAccessException()
    {
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var handler = new DeleteAppointmentNoteCommandHandler(notes.Object, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new DeleteAppointmentNoteCommand { Id = "note1", ProviderEmail = "provider@example.com" },
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new DeleteAppointmentNoteCommandHandler(Mock.Of<INoteService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
