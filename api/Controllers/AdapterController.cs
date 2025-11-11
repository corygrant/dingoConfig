using api.Models.Adapters;
using api.Enums;
using api.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdapterController(ICommsAdapterManager comms) : ControllerBase
{
    [HttpGet("available")]
    public AdapterAvailableResponse GetAvailable()
    {
        return comms.GetAvailable();
    }

    [HttpGet("status")]
    public ActionResult<AdapterStatusResponse> GetStatus()
    {
        return comms.GetStatus();
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect([FromBody] ConnectAdapterRequest request)
    {
        CanBitRate br = request.Bitrate switch
        {
            "1000K" => CanBitRate.BitRate1000K,
            "500K" => CanBitRate.BitRate500K,
            "250K" => CanBitRate.BitRate250K,
            "125K" => CanBitRate.BitRate125K,
            _ => throw new ArgumentException($"Unknown bitrate type: {request.Bitrate}")
        };

        if (await comms.ConnectAsync(comms.ToAdapter(request.AdapterType), request.Port, br, CancellationToken.None))
            return Ok();
        return Problem();
    }

    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect()
    {
        await comms.DisconnectAsync();
        return Ok();
    }
}