using application.CommsAdapters.Commands;
using application.CommsAdapters.Queries;
using contracts.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class AdapterController()
    : BaseApiController
{

    [HttpGet("available")]
    public async Task<AdapterAvailableResponse> GetAvailable()
    {
        return await Mediator.Send(new GetAdapterAvailable.Query());
    }
    
    [HttpGet("status")]
    public async Task<ActionResult<AdapterStatusResponse>> GetStatus()
    {
        return await Mediator.Send(new GetAdapterStatus.Query());
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect([FromBody] ConnectAdapterRequest request)
    {
        if (await Mediator.Send(new ConnectAdapter.Command { Settings = request }))
            return Ok();
        return Problem();
    }
    
    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect()
    {
        await Mediator.Send(new DisconnectAdapter.Command());
        return Ok();
    }
}