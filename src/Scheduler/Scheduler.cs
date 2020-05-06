using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Foralla.Scheduler
{
    /// <summary>
    ///     Monitors and handles all scheduled jobs.
    /// </summary>
    internal class Scheduler : BackgroundService, IAsyncDisposable
    {
        private readonly ILogger<Scheduler> _logger;
        private readonly ConcurrentDictionary<string, JobScheduleInformation> _runningScheduledJobs = new ConcurrentDictionary<string, JobScheduleInformation>();
        private readonly IEnumerable<IJob> _systemJobs;

        private bool _disposed;
        private CancellationToken _stoppingToken;

        public Scheduler(IEnumerable<ISystemJob> systemJobs, ILogger<Scheduler> logger, SchedulerManager schedulerHandler)
        {
            _systemJobs = systemJobs.Select(sj => sj.Job).OfType<IJob>().ToArray();

            _logger = logger;

            schedulerHandler.GetJobs += (sender, e) => e.Jobs = e.SystemJobs ?
                                                                    _systemJobs :
                                                                    _runningScheduledJobs.Where(job => !_systemJobs.Contains(job.Value.Job)).Select(job => job.Value.Job);

            schedulerHandler.JobAdded += (sender, e) => e.Success = AddJob(e.Job);

            schedulerHandler.JobRemoved += (sender, e) => e.Success = RemoveJobAsync(e.Name);
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_disposed)
            {
                _disposed = true;
            }

            _disposed = true;

            Task.WaitAll(_runningScheduledJobs.Select(st => st.Value.JobTask).ToArray());

            foreach (var scheduledJob in _runningScheduledJobs)
            {
                scheduledJob.Value.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await Task.WhenAll(_runningScheduledJobs.Select(st => st.Value.JobTask)).ConfigureAwait(false);

            foreach (var scheduledJob in _runningScheduledJobs)
            {
                scheduledJob.Value.Dispose();
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            foreach (var job in _systemJobs)
            {
                AddJob(job);
            }

            return Task.CompletedTask;
        }

        private bool AddJob(IJob job)
        {
            var jobScheduleInformation = new JobScheduleInformation();

            if (!_runningScheduledJobs.TryAdd(job.Name, jobScheduleInformation))
            {
                _logger.LogWarning($"{job.Name} already exist. You have to remove existing before adding new.");

                return false;
            }

            jobScheduleInformation.Job = job;
            jobScheduleInformation.CancellationTokenSource = new CancellationTokenSource();
            jobScheduleInformation.JobTask = JobRunnerAsync(job, jobScheduleInformation.CancellationTokenSource.Token);

            jobScheduleInformation.JobTask.ContinueWith(t =>
                                                        {
                                                            // Remove jobs that are stopped because there is no more
                                                            // scheduled executions of the job to free resources
                                                            // runtime.
                                                            if (!_stoppingToken.IsCancellationRequested &&
                                                                _runningScheduledJobs.TryRemove(job.Name, out var removed))
                                                            {
                                                                removed.Dispose();
                                                            }
                                                        }, CancellationToken.None);

            return true;
        }

        private async Task JobRunnerAsync(IJob job, CancellationToken jobCancellationToken)
        {
            if (job.NextScheduledTime == null)
            {
                _logger.LogWarning($"{job.Name} does not have any scheduled time, the job is not started.");

                return;
            }

            using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, jobCancellationToken))
            {
                var linkedToken = linkedTokenSource.Token;

                _logger.LogInformation($"Starting the the scheduler for {job.Name}.");

                while (!linkedToken.IsCancellationRequested && job.NextScheduledTime != null)
                {
                    var nextScheduledTime = job.NextScheduledTime.Value;

                    _logger.LogTrace($"{job.Name} is scheduled to start {nextScheduledTime}");

                    await Task.WhenAny(Task.Delay(nextScheduledTime - DateTimeOffset.Now, linkedToken)).ConfigureAwait(false);

                    if (linkedToken.IsCancellationRequested)
                    {
                        _logger.LogTrace(_stoppingToken.IsCancellationRequested ?
                                             $"{job.Name} that should have started {nextScheduledTime} has been cancelled because the host is shutting down." :
                                             $"{job.Name} that should have started {nextScheduledTime} has been cancelled because the job is removed.");

                        break;
                    }

                    try
                    {
                        var stopwatch = new Stopwatch();

                        stopwatch.Start();

                        _logger.LogTrace($"{job.Name} scheduled at {nextScheduledTime} is starting at {DateTimeOffset.Now}.");

                        var jobTask = job.ExecuteAsync(_stoppingToken);

                        _logger.LogTrace($"{job.Name} has started.");

                        await jobTask.ConfigureAwait(false);

                        stopwatch.Stop();

                        _logger.LogTrace($"{job.Name} has stopped at {DateTimeOffset.Now}, time elapsed: {stopwatch.Elapsed}.");
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (OperationCanceledException)
                    {
                        _logger.LogTrace(_stoppingToken.IsCancellationRequested ?
                                             $"{job.Name} that started at {nextScheduledTime} has been cancelled during execution because the host is shutting down." :
                                             $"{job.Name} that started at {nextScheduledTime} has been cancelled by the job itself.");

                        break;
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                }

                _logger.LogTrace(job.NextScheduledTime == null ?
                                     $"{job.Name} has been removed from the scheduler since there was no more scheduled execution times." :
                                     $"{job.Name} has not been rescheduled since the host is shutting down.");
            }
        }

        private async Task<bool> RemoveJobAsync(string name)
        {
            if (_systemJobs.Any(systemJob => systemJob.Name == name))
            {
                _logger.LogError($"{name} is a system job and cannot be removed.");

                return false;
            }

            if (!_runningScheduledJobs.TryRemove(name, out var job))
            {
                _logger.LogWarning($"Could not find the job {name}.");

                return false;
            }

            job.CancellationTokenSource.Cancel();
            await job.JobTask.ConfigureAwait(false);

            job.Dispose();

            _logger.LogInformation($"The job {name} is removed from the scheduler.");

            return true;
        }

        private class JobScheduleInformation : IDisposable
        {
            public CancellationTokenSource CancellationTokenSource { get; set; }

            public IJob Job { get; set; }

            public Task JobTask { get; set; }

            public void Dispose()
            {
                JobTask?.Dispose();
                CancellationTokenSource?.Dispose();
            }
        }
    }
}
