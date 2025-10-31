using contracts.Adapters;
using domain.Interfaces;
using MediatR;

namespace application.CommsAdapters.Commands;

public class DisconnectAdapter
{
    public class Command : IRequest<bool>
    {
        public class Handler(ICommsAdapterManager comms) : IRequestHandler<Command, bool>
        {
            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                await comms.DisconnectAsync();
                return true;
            }
        }

    }

}