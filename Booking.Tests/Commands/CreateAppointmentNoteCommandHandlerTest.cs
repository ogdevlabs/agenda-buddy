namespace Booking.Tests.Commands;

public class CreateAppointmentNoteCommandHandlerTest
{
    [Fact]
    public async Task Handle_CreatesNoteWithCallerProviderEmailAndPathIdentifier_ReturnsOk()
    {
        var created = new NoteEntity
        {
            ProviderEmail = "provider@example.com",
            AppointmentIdentifier = "abc123",
            Content = "Went well."
        };
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.CreateAsync(It.Is<NoteEntity>(note =>
                note.ProviderEmail == "provider@example.com"
                && note.AppointmentIdentifier == "abc123"
                && note.Content == "Went well.")))
            .ReturnsAsync(created);
        var handler = new CreateAppointmentNoteCommandHandler(notes.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new CreateAppointmentNoteCommand
            {
                ProviderEmail = "provider@example.com",
                Identifier = "abc123",
                Content = "Went well."
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(created, result.Value);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CreateAppointmentNoteCommandHandler(Mock.Of<INoteService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
