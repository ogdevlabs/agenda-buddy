namespace Booking.Tests.Queries;

public class GetAppointmentNotesQueryHandlerTest
{
    [Fact]
    public async Task Handle_DelegatesToNoteService_ReturnsOkWithNotes()
    {
        var expectedNotes = new List<NoteEntity>
        {
            new() { ProviderEmail = "provider@example.com", AppointmentIdentifier = "abc123", Content = "Went well." }
        };
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.GetByAppointmentAsync("provider@example.com", "abc123"))
            .ReturnsAsync(expectedNotes);
        var handler = new GetAppointmentNotesQueryHandler(notes.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(
            new GetAppointmentNotesQuery { ProviderEmail = "provider@example.com", Identifier = "abc123" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedNotes, result.Value);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetAppointmentNotesQueryHandler(Mock.Of<INoteService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
