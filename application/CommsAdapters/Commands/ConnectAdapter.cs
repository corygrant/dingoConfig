using contracts.Adapters;
using domain.Enums;
using domain.Interfaces;
using MediatR;

namespace application.CommsAdapters.Commands;

public class ConnectAdapter
{
    public class Command : IRequest<bool>
    {
        public required ConnectAdapterRequest Settings { get; set; }

        public class Handler(ICommsAdapterManager comms) : IRequestHandler<Command, bool>
        {
            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                CanBitRate br = request.Settings.Bitrate switch
                {
                    "1000K" => CanBitRate.BitRate1000K,
                    "500K" => CanBitRate.BitRate500K,
                    "250K" => CanBitRate.BitRate250K,
                    "125K" => CanBitRate.BitRate125K,
                    _ => throw new ArgumentException($"Unknown bitrate type: {request.Settings.Bitrate}")
                };

                return await comms.ConnectAsync( comms.ToAdapter(request.Settings.AdapterType), request.Settings.Port, br, cancellationToken);
            }
        }
    }
}