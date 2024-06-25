namespace EventAndCommands.Commands.Calendar;

[RegisterService(ServiceLifetime.Scoped)]
public class BookCalendarCommandHandler : IRequestHandler<BookCalendarCommand, bool>
{
    public Task<bool> Handle(BookCalendarCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}