using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foralla.Scheduler.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Foralla.Scheduler.IntegrationTest
{
    public class SchedulerTest
    {
        /// <summary>
        ///     Helper class for dependency injected tests
        /// </summary>
        private class Job : IJob
        {
            private Func<CancellationToken, Task> _executeAsync;
            private Func<DateTimeOffset?> _nextScheduledTimeFunc;

            public virtual string Name { get; } = "Job name";

            public DateTimeOffset? NextScheduledTime => _nextScheduledTimeFunc();

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                return _executeAsync.Invoke(stoppingToken);
            }

            public void Initialize(Func<DateTimeOffset?> nextScheduledTime, Func<CancellationToken, Task> executeAsyncFunc)
            {
                _nextScheduledTimeFunc = nextScheduledTime;
                _executeAsync = executeAsyncFunc;
            }
        }

        private class Job2 : Job
        {
            public override string Name { get; } = "Job 2 name";
        }

        /// <summary>
        ///     Helper class for job cancellation tests since Moq isn't that good on async operations.
        /// </summary>
        private class CancellationJob : IJob
        {
            public CancellationToken? CancellationToken { get; set; }

            public string Name => nameof(CancellationJob);
            public DateTimeOffset? NextScheduledTime { get; } = DateTimeOffset.Now.AddMilliseconds(100);

            public async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                if (CancellationToken != null)
                {
                    await Task.Delay(10000, CancellationToken.Value);
                }
                else
                {
                    await Task.Delay(10000, stoppingToken);
                }
            }
        }

        [Fact]
        public async Task TestAddExistingJob()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob(jobMock.Object));

            await Task.Delay(100);

            Assert.False(schedulerManager.AddJob(jobMock.Object));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Warning, $"{jobMock.Name} already exist. You have to remove existing before adding new.", Times.Once);
        }

        [Fact]
        public async Task TestExecutingJobIsCancelledOnHostStop()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            CancellationJob cancellationJob;

            await using (var services = new ServiceCollection()
                                       .AddSingleton(loggerMock.Object)
                                       .AddScheduler()
                                       .AddSystemJob<CancellationJob>()
                                       .BuildServiceProvider(true))
            {
                cancellationJob = (CancellationJob)services.GetRequiredService<SystemJob<CancellationJob>>().Job;

                var scheduler = services.GetRequiredService<IHostedService>();

                await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

                await Task.Delay(200);

                await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            loggerMock.Verify(LogLevel.Trace, $"{cancellationJob.Name} that started at {cancellationJob.NextScheduledTime} has been cancelled during execution because the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestExecutingJobIsHasBeenExternallyCancelled()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            using var manualCancellationTokenSource = new CancellationTokenSource();

            CancellationJob cancellationJob;

            await using (var services = new ServiceCollection()
                                       .AddSingleton(loggerMock.Object)
                                       .AddScheduler()
                                       .AddSystemJob<CancellationJob>()
                                       .BuildServiceProvider(true))
            {
                cancellationJob = (CancellationJob)services.GetRequiredService<SystemJob<CancellationJob>>().Job;
                cancellationJob.CancellationToken = manualCancellationTokenSource.Token;

                var scheduler = services.GetRequiredService<IHostedService>();

                await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

                manualCancellationTokenSource.Cancel();

                await Task.Delay(200, CancellationToken.None);

                await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            loggerMock.Verify(LogLevel.Trace, $"{cancellationJob.Name} that started at {cancellationJob.NextScheduledTime} has been cancelled by the job itself.", Times.Once);
        }

        [Fact]
        public async Task TestJobIsStoppedWhenHostIsStoppedBetweenExecutionAndRescheduling()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();
            Task stopTask = null;

            services.GetRequiredService<Job>().Initialize(() => DateTimeOffset.Now.AddMilliseconds(200),
                                                          _ =>
                                                          {
                                                              stopTask = scheduler.StopAsync(CancellationToken.None);

                                                              return Task.CompletedTask;
                                                          });

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(300);

            await stopTask.ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, "Job name has not been rescheduled since the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestJobIsStoppedWhenThereIsNoNextScheduledTime()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            DateTimeOffset? executionTime = DateTimeOffset.Now.AddMilliseconds(200);

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            services.GetRequiredService<Job>().Initialize(() => executionTime,
                                                          _ =>
                                                          {
                                                              executionTime = null;

                                                              return Task.CompletedTask;
                                                          });

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(400);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, "Job name has been removed from the scheduler since there was no more scheduled execution times.", Times.Once);
        }

        [Fact]
        public async Task TestJobWithoutScheduledTimeIsntScheduled()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            services.GetRequiredService<Job>().Initialize(() => null,
                                                          _ =>
                                                          {
                                                              Assert.True(false, "Unexpected call to ExecuteAsync.");

                                                              return Task.CompletedTask;
                                                          });

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Warning, "Job name does not have any scheduled time, the job is not started.", Times.Once);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestRemoveDynamicallyAddedJob()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            services.GetRequiredService<Job>().Initialize(() => DateTimeOffset.Now.AddSeconds(1),
                                                          _ => Task.CompletedTask);

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob<Job>());

            await Task.Delay(100);

            Assert.True(await schedulerManager.RemoveJobAsync("Job name"));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, "Job name has not been rescheduled since the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestRemoveNonExistingJob()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.False(await schedulerManager.RemoveJobAsync("NoJob"));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Warning, "Could not find the job NoJob.", Times.Once);
        }

        [Fact]
        public async Task TestRemoveSystemJob()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            services.GetRequiredService<Job>().Initialize(() => DateTimeOffset.Now.AddSeconds(1),
                                                          _ => Task.CompletedTask);

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.False(await schedulerManager.RemoveJobAsync("Job name"));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Error, "Job name is a system job and cannot be removed.", Times.Once);
        }

        [Fact]
        public async Task TestRetrievingJobsShouldOnlyReturnDynamicJobs()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob(jobMock.Object));

            var jobs = schedulerManager.Jobs.ToArray();

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.DoesNotContain(services.GetService<Job>(), jobs);
            Assert.Contains(jobMock.Object, jobs);
        }

        [Fact]
        public async Task TestRetrievingSystemJobsShouldOnlyReturnSystemJobs()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(j => j.Name).Returns(jobMock.Name);
            jobMock.SetupGet(j => j.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSingleton<Job>()
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            var job = services.GetRequiredService<Job>();

            job.Initialize(() => DateTimeOffset.Now.AddSeconds(1), null);

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob(jobMock.Object));

            var jobs = schedulerManager.SystemJobs.ToArray();

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Contains(job, jobs);
            Assert.DoesNotContain(jobMock.Object, jobs);
        }

        [Fact]
        public async Task TestRunDynamicallyAddedJob()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddMilliseconds(200));

            Assert.True(services.GetRequiredService<SchedulerManager>().AddJob(jobMock.Object));

            await Task.Delay(300);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            jobMock.Verify(job => job.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
            loggerMock.Verify(LogLevel.Trace, $"{jobMock.Name} has not been rescheduled since the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestSystemJobsInitializedIsExecuted()
        {
            using var cancellationTokenSource1 = new CancellationTokenSource();
            using var cancellationTokenSource2 = new CancellationTokenSource();

            var waitForStop1 = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waitForStop2 = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            cancellationTokenSource1.Token.Register(state =>
                                                    {
                                                        var tcs = (TaskCompletionSource<object>)state;
                                                        tcs.TrySetResult(null);
                                                    }, waitForStop1);

            cancellationTokenSource2.Token.Register(state =>
                                                    {
                                                        var tcs = (TaskCompletionSource<object>)state;
                                                        tcs.TrySetResult(null);
                                                    }, waitForStop2);

            var job1Executed = false;
            var job2Executed = false;

            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddTransient(p =>
                                                    {
                                                        var job = new Job();

                                                        job.Initialize(() => !job1Executed ? (DateTimeOffset?)DateTimeOffset.Now.AddMilliseconds(100) : null,
                                                                       ct =>
                                                                       {
                                                                           job1Executed = true;
                                                                           cancellationTokenSource1.Cancel();

                                                                           return Task.CompletedTask;
                                                                       });

                                                        return job;
                                                    })
                                      .AddTransient(p =>
                                                    {
                                                        var job = new Job2();

                                                        job.Initialize(() => !job2Executed ? (DateTimeOffset?)DateTimeOffset.Now.AddMilliseconds(100) : null,
                                                                       ct =>
                                                                       {
                                                                           job2Executed = true;
                                                                           cancellationTokenSource2.Cancel();

                                                                           return Task.CompletedTask;
                                                                       });

                                                        return job;
                                                    })
                                      .AddSystemJob<Job>()
                                      .AddSystemJob<Job2>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            var allTask = Task.WhenAll(waitForStop1.Task, waitForStop2.Task);

            var result = await Task.WhenAny(allTask, Task.Delay(10000, CancellationToken.None));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            // If this assertion fails, something has hanged during execution so that the jobs isn't executed.
            Assert.Equal(allTask, result);
        }

        [Fact]
        public async Task TestWaitingJobIsCancelledOnHostStop()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var executionTime = DateTimeOffset.Now.AddMinutes(1);

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddTransient(p =>
                                                    {
                                                        var job = new Job();

                                                        job.Initialize(() => executionTime,
                                                                       ct =>
                                                                       {
                                                                           Assert.False(true, "The callback should never have been called.");

                                                                           return Task.CompletedTask;
                                                                       });

                                                        return job;
                                                    })
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(300);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, $"Job name that should have started {executionTime} has been cancelled because the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestIssue2LogErrorOnUnhandledExceptionsInJob()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddTransient(p =>
                                                    {
                                                        var job = new Job();

                                                        job.Initialize(() => DateTimeOffset.Now.AddMilliseconds(100),
                                                                       ct => throw new InvalidOperationException("This exception should be logged as AggregatedException"));

                                                        return job;
                                                    })
                                      .AddSystemJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(200);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Error, "Job name failed unexpectedly. See exception for more information.", new AggregateException("One or more errors occurred. (This exception should be logged as AggregatedException)"), Times.Once);
        }

        [Fact]
        public async Task TestIssue2AddJobShouldFailIfJobInitalizationFails()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddTransient(p =>
                                                    {
                                                        var job = new Job();

                                                        job.Initialize(() => throw new InvalidOperationException("This exception should be logged as AggregatedException"),
                                                                       ct => Task.CompletedTask);

                                                        return job;
                                                    })
                                      .AddJob<Job>()
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.False(services.GetRequiredService<ISchedulerManager>().AddJob<Job>());

            await Task.Delay(200);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Error, "Job name failed unexpectedly. See exception for more information.", new AggregateException("One or more errors occurred. (This exception should be logged as AggregatedException)"), Times.Once);
        }
    }
}
