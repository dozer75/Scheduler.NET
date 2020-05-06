using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CronScheduler.WebHost.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobPingController : ControllerBase
    {
        private readonly ILogger<JobPingController> _logger;

        public JobPingController(ILogger<JobPingController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{from}")]
        public IActionResult Get(string from)
        {
            _logger.LogInformation($"Pinged from {from} at {DateTimeOffset.Now}");

            return Ok($"Ping is returned for {from}");
        }
    }
}
