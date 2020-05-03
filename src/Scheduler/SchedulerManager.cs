using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Foralla.Scheduler.EventArgs;

namespace Foralla.Scheduler
{
    /// <summary>
    ///     The internal implementation of <see cref="Foralla.Scheduler.ISchedulerManager" />.
    /// </summary>
    internal class SchedulerManager : ISchedulerManager
    {
        public IEnumerable<IJob> Jobs
        {
            get
            {
                var eventArgs = new GetJobsEventArgs(false);

                GetJobs?.Invoke(this, eventArgs);

                return eventArgs.Jobs;
            }
        }

        public IEnumerable<IJob> SystemJobs
        {
            get
            {
                var eventArgs = new GetJobsEventArgs(true);

                GetJobs?.Invoke(this, eventArgs);

                return eventArgs.Jobs;
            }
        }

        public bool AddJob(IJob job)
        {
            var eventArgs = new AddJobEventArgs(job);
            JobAdded?.Invoke(this, eventArgs);

            return eventArgs.Success;
        }

        public event EventHandler<GetJobsEventArgs> GetJobs;

        public event EventHandler<AddJobEventArgs> JobAdded;

        public event EventHandler<RemoveJobEventArgs> JobRemoved;

        public async Task<bool> RemoveJobAsync(string name)
        {
            var eventArgs = new RemoveJobEventArgs(name);
            JobRemoved?.Invoke(this, eventArgs);

            return await (eventArgs.Success ?? Task.FromResult(false)).ConfigureAwait(false);
        }
    }
}
