using System.Threading;
using System.Threading.Tasks;
using CronScheduler.WebHost.DTO;

namespace CronScheduler.WebHost.Services
{
    public interface IJobService
    {
        Task<bool> AddAsync(Job job, CancellationToken cancellationToken);

        Task<string> StartAsync(string jobName, CancellationToken cancellationToken);
    }
}
