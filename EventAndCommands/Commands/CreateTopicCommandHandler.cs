using MediatR;
using KafkaFlow.Admin;

namespace EventAndCommands.Commands;

public class CreateTopicCommandHandler :IRequestHandler<CreateTopicCommand>
{
    
    public Task<Unit> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        
        throw new NotImplementedException();
    }
}