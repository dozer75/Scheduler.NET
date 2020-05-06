using System;
using System.Threading;
using System.Threading.Tasks;
using Foralla.Scheduler;
using Microsoft.Extensions.Logging;

namespace CronScheduler.WebHost.Schedulers
{
    public class SystemJob : CronJob
    {
        private readonly ILogger<SystemJob> _logger;
        public override string Expression => "*/30 * * * * *";
        public override string Name => nameof(SystemJob);

        public SystemJob(ILogger<SystemJob> logger)
        {
            _logger = logger;
        }

        public override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(SystemJob)} executed at {DateTimeOffset.Now}");

            return Task.CompletedTask;
        }
    }
}
