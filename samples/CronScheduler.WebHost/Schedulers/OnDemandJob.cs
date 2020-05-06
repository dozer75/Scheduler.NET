using System;
using System.Threading;
using System.Threading.Tasks;
using CronScheduler.WebHost.Services;
using Foralla.Scheduler;
using Microsoft.Extensions.Logging;

namespace CronScheduler.WebHost.Schedulers
{
    public class OnDemandJob : CronJob
    {
        private readonly IJobService _jobService;
        private readonly ILogger<OnDemandJob> _logger;
        private string _expression;
        private string _name;
        public override string Expression => _expression;
        public override string Name => _name;

        public OnDemandJob(IJobService jobService, ILogger<OnDemandJob> logger)
        {
            _jobService = jobService;
            _logger = logger;
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var result = await _jobService.StartAsync(Name, stoppingToken);

            if (!string.IsNullOrEmpty(result))
            {
                _logger.LogInformation($"{result} was returned at {DateTimeOffset.Now}");
            }
        }

        public void Initalize(string name, string expression)
        {
            _name = name;
            _expression = expression;
        }
    }
}
