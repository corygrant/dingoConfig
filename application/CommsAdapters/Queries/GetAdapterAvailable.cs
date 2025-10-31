using contracts.Adapters;
using domain.Interfaces;
using MediatR;

namespace application.CommsAdapters.Queries;

public class GetAdapterAvailable
{
    public class Query() : IRequest<AdapterAvailableResponse>;

    public class Handler(ICommsAdapterManager comms) : IRequestHandler<Query, AdapterAvailableResponse>
    {
        public Task<AdapterAvailableResponse> Handle(Query request, CancellationToken cancellationToken)
        {
            return Task.FromResult(comms.GetAvailable());
        }
    }
}