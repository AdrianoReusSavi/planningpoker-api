using Microsoft.AspNetCore.Mvc;

namespace PlanningPoker.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("pong");
}