namespace AgendaBuddy.EventAndCommands.Commands.Calendar;

public class BookCalendarCommandHandler : IRequestHandler<BookCalendarCommand, bool>
{
    public Task<bool> Handle(BookCalendarCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
