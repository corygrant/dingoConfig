using contracts.Adapters;
using domain.Interfaces;
using MediatR;

namespace application.CommsAdapters.Queries;

public class GetAdapterStatus
{
    public class Query() : IRequest<AdapterStatusResponse>;

    public class Handler(ICommsAdapterManager comms) : IRequestHandler<Query, AdapterStatusResponse>
    {
        public Task<AdapterStatusResponse> Handle(Query request, CancellationToken cancellationToken)
        {
            var status = comms.GetStatus();

            return Task.FromResult(status);
        }
    }
}