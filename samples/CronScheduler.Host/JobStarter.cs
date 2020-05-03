using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Foralla.Scheduler.Cron.Demo
{
    internal class JobStarter : CronJob
    {
        private readonly ILogger<ChildJob> _childLogger;

        private readonly ILogger<JobStarter> _logger;
        private readonly ISchedulerManager _scheduler;
        private int _childCounter;

        public override string Expression { get; } = "*/30 * * * * *";

        public override string Name { get; } = Guid.NewGuid().ToString();

        public JobStarter(ILogger<JobStarter> logger, ILogger<ChildJob> childLogger, ISchedulerManager scheduler)
        {
            _logger = logger;
            _childLogger = childLogger;
            _scheduler = scheduler;
        }

        public override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(JobStarter)} started at {DateTimeOffset.Now}.");

            if (_scheduler.Jobs.Any())
            {
                _scheduler.RemoveJobAsync(_scheduler.Jobs.Select(job => job.Name).First());
            }
            else
            {
                _scheduler.AddJob(new ChildJob(_childLogger)
                                  {
                                      ChildName = $"Child-{Interlocked.Increment(ref _childCounter)}"
                                  });
            }

            return Task.CompletedTask;
        }
    }
}
