using Microsoft.AspNetCore.Mvc;
using Swarm.Server.Diagnostics;

namespace Swarm.Server.Controllers;

[ApiController]
[Route("diagnostics")]
public class DiagnosticsController(ConnectionDiagnostics diagnostics) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<DiagnosticsSnapshot> Get()
    {
        return diagnostics.GetSnapshot();
    }
}
