namespace Booking.Tests.Commands;

public class UpdateAppointmentNoteCommandHandlerTest
{
    [Fact]
    public async Task Handle_DelegatesToNoteService_ReturnsOkWithUpdatedNote()
    {
        var updated = new NoteEntity
        {
            ProviderEmail = "provider@example.com",
            AppointmentIdentifier = "abc123",
            Content = "Updated."
        };
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.UpdateAsync("note1", "provider@example.com", "Updated.")).ReturnsAsync(updated);
        var handler = new UpdateAppointmentNoteCommandHandler(notes.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new UpdateAppointmentNoteCommand { Id = "note1", ProviderEmail = "provider@example.com", Content = "Updated." },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(updated, result.Value);
    }

    // Threat T-202: propagated, not caught here -- Booking.Api maps both to 403 indistinguishably.
    [Fact]
    public async Task Handle_NoteBelongsToAnotherProvider_PropagatesUnauthorizedAccessException()
    {
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var handler = new UpdateAppointmentNoteCommandHandler(notes.Object, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new UpdateAppointmentNoteCommand { Id = "note1", ProviderEmail = "provider@example.com", Content = "x" },
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoSuchNote_PropagatesKeyNotFoundException()
    {
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new KeyNotFoundException());
        var handler = new UpdateAppointmentNoteCommandHandler(notes.Object, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new UpdateAppointmentNoteCommand { Id = "note1", ProviderEmail = "provider@example.com", Content = "x" },
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new UpdateAppointmentNoteCommandHandler(Mock.Of<INoteService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
