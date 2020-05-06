using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Foralla.Scheduler.Cron.Demo
{
    internal class ChildJob : CronJob
    {
        private readonly ILogger<ChildJob> _logger;
        public string ChildName { get; set; }

        public override DateTimeOffset? DontStartAfter { get; } = DateTimeOffset.Now.AddSeconds(30);
        public override string Expression { get; } = "*/10 * * * * *";
        public override string Name => ChildName;

        public ChildJob(ILogger<ChildJob> logger)
        {
            _logger = logger;
        }

        public override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(ChildJob)} - {Name} started at {DateTimeOffset.Now}");

            return Task.CompletedTask;
        }
    }
}
