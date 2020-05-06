using System.Threading.Tasks;
using CronScheduler.WebHost.DTO;
using CronScheduler.WebHost.Services;
using Microsoft.AspNetCore.Mvc;

namespace CronScheduler.WebHost.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobController : ControllerBase
    {
        private readonly IJobService _service;

        public JobController(IJobService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Job job)
        {
            return await _service.AddAsync(job, HttpContext.RequestAborted) ?
                       (IActionResult)Ok(job) :
                       NotFound();
        }
    }
}
