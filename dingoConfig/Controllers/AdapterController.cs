using dingoConfig.Models.Adapters;
using dingoConfig.Enums;
using dingoConfig.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace dingoConfig.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdapterController(ICommsAdapterManager comms) : ControllerBase
{
    /// <summary>
    /// Get available adapters and ports
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(AdapterAvailableResponse), StatusCodes.Status200OK)]
    public ActionResult<AdapterAvailableResponse> GetAvailable()
    {
        var result = comms.GetAvailable();
        return Ok(result);
    }

    /// <summary>
    /// Get current adapter connection status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(AdapterStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<AdapterStatusResponse> GetStatus()
    {
        var status = comms.GetStatus();
        return Ok(status);
    }

    /// <summary>
    /// Connect to an adapter
    /// </summary>
    [HttpPost("connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Connect([FromBody] ConnectAdapterRequest request)
    {
        // Model validation handled automatically by [ApiController]
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Parse bitrate
        CanBitRate bitrate = request.Bitrate switch
        {
            "1000K" => CanBitRate.BitRate1000K,
            "500K" => CanBitRate.BitRate500K,
            "250K" => CanBitRate.BitRate250K,
            "125K" => CanBitRate.BitRate125K,
            _ => CanBitRate.BitRate500K // Default fallback (validation regex should prevent this)
        };

        // Attempt connection
        try
        {
            var adapter = comms.ToAdapter(request.AdapterType);
            var success = await comms.ConnectAsync(adapter, request.Port, bitrate, CancellationToken.None);

            if (!success)
            {
                return Problem(
                    title: "Connection Failed",
                    detail: $"Unable to connect to adapter '{request.AdapterType}' on port '{request.Port}'",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Ok(new { message = "Connected successfully", adapterType = request.AdapterType, port = request.Port });
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Connection Error",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Disconnect from current adapter
    /// </summary>
    [HttpPost("disconnect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Disconnect()
    {
        if (!comms.IsConnected)
        {
            return Problem(
                title: "Disconnection Error",
                detail: "Adapter is not connected",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        
        try
        {
            await comms.DisconnectAsync();
            return Ok(new { message = "Disconnected successfully" });
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Disconnection Error",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}