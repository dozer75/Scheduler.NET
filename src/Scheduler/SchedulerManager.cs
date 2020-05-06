using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Foralla.Scheduler.EventArgs;
using Microsoft.Extensions.DependencyInjection;

namespace Foralla.Scheduler
{
    /// <summary>
    ///     The internal implementation of <see cref="Foralla.Scheduler.ISchedulerManager" />.
    /// </summary>
    internal class SchedulerManager : ISchedulerManager
    {
        private readonly IServiceProvider _provider;

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

        public SchedulerManager(IServiceProvider provider)
        {
            _provider = provider;
        }

        public bool AddJob<TJob>(Action<TJob> setup = null)
            where TJob : class, IJob
        {
            var job = _provider.GetService<TJob>();

            if (job == null)
            {
                throw new InvalidOperationException($"{typeof(TJob).Name} is not a registered service.");
            }

            setup?.Invoke(job);

            return AddJob(job);
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
