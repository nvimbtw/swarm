using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swarm.Server.Connections;

namespace Swarm.Server.Controllers;

[ApiController]
[Route("api/pairing")]
[EnableRateLimiting("pairing")]
public sealed class PairingController(PairingService pairing) : ControllerBase
{
    [HttpPost("start")]
    public IActionResult Start(StartPairingRequest request)
    {
        if (!Valid(request.MachineId, 100) || !Valid(request.MachineName, 80))
            return BadRequest(new { error = "invalid_machine" });
        try { return Ok(pairing.Start(request.MachineId, request.MachineName)); }
        catch (InvalidOperationException) { return StatusCode(503, new { error = "pairing_capacity" }); }
    }

    [HttpPost("complete")]
    public IActionResult Complete(CompletePairingRequest request)
    {
        if (!Valid(request.PairingId, 64) || !Valid(request.Secret, 128) || !Valid(request.Code, 16))
            return BadRequest(new { error = "invalid_pairing" });
        var credential = pairing.Complete(request.PairingId, request.Secret, request.Code);
        return credential is null ? Unauthorized(new { error = "pairing_rejected" }) : Ok(credential);
    }

    internal static bool Valid(string? value, int max) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= max && !value.Any(char.IsControl);
}

public sealed record StartPairingRequest(string MachineId, string MachineName);
public sealed record CompletePairingRequest(string PairingId, string Secret, string Code);
