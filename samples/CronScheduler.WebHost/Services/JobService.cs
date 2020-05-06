using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CronScheduler.WebHost.DTO;
using CronScheduler.WebHost.Schedulers;
using Foralla.Scheduler;

namespace CronScheduler.WebHost.Services
{
    public class JobService : IJobService
    {
        private readonly HttpClient _httpClient;
        private readonly ISchedulerManager _manager;

        public JobService(HttpClient httpClient, ISchedulerManager manager)
        {
            _httpClient = httpClient;
            _manager = manager;
        }

        public Task<bool> AddAsync(Job job, CancellationToken cancellationToken)
        {
            return Task.FromResult(_manager.AddJob<OnDemandJob>(demandJob => demandJob.Initalize(job.Name, job.Expression)));
        }

        public async Task<string> StartAsync(string jobName, CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetAsync($"jobping/{jobName}", cancellationToken);

            return result.IsSuccessStatusCode ?
                       await result.Content.ReadAsStringAsync() :
                       null;
        }
    }
}
