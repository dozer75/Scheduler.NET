using System;
using System.Threading;
using System.Threading.Tasks;

namespace Foralla.Scheduler
{
    /// <summary>
    ///     Interface for a scheduled job
    /// </summary>
    public interface IJob
    {
        /// <summary>
        ///     Gets the name of the job.
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Gets the next scheduled execution time.
        /// </summary>
        /// <remarks>Must return the next scheduled time as local time. Returning null will stop and remove the job.</remarks>
        DateTimeOffset? NextScheduledTime { get; }

        /// <summary>
        ///     The operation to execute when <see cref="NextScheduledTime" /> occurs.
        /// </summary>
        /// <param name="stoppingToken">
        ///     A <see cref="CancellationToken" /> that is triggered when the host that runs the scheduler
        ///     is about to stop.
        /// </param>
        /// <returns>A <see cref="Task" /> that represents the long running operation.</returns>
        Task ExecuteAsync(CancellationToken stoppingToken);
    }
}
