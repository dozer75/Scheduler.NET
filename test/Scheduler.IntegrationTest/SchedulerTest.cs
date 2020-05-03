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
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.False(schedulerManager.AddJob(jobMock.Object));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Warning, $"{jobMock.Name} already exist. You have to remove existing before adding new.", Times.Once);
        }

        [Fact]
        public async Task TestExecutingJobIsCancelledOnHostStop()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();
            var cancellationJob = new CancellationJob();

            await using (var services = new ServiceCollection()
                                       .AddSingleton(loggerMock.Object)
                                       .AddScheduler()
                                       .AddSystemJob(cancellationJob)
                                       .BuildServiceProvider(true))
            {
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

            var cancellationJob = new CancellationJob
                                  {
                                      CancellationToken = manualCancellationTokenSource.Token
                                  };

            await using (var services = new ServiceCollection()
                                       .AddSingleton(loggerMock.Object)
                                       .AddScheduler()
                                       .AddSystemJob(cancellationJob)
                                       .BuildServiceProvider(true))
            {
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

            var executionTime = DateTimeOffset.Now.AddMilliseconds(200);
            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => executionTime);

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            Task stopTask = null;

            jobMock.Setup(job => job.ExecuteAsync(It.IsAny<CancellationToken>())).Callback<CancellationToken>(_ => stopTask = scheduler.StopAsync(CancellationToken.None));

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(300);

            await stopTask.ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, $"{jobMock.Name} has not been rescheduled since the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestJobIsStoppedWhenThereIsNoNextScheduledTime()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            DateTimeOffset? executionTime = DateTimeOffset.Now.AddMilliseconds(200);
            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => executionTime);
            jobMock.Setup(job => job.ExecuteAsync(It.IsAny<CancellationToken>())).Callback<CancellationToken>(_ => executionTime = null);

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(400);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, $"{jobMock.Name} has been removed from the scheduler since there was no more scheduled execution times.", Times.Once);
        }

        [Fact]
        public async Task TestJobWithoutScheduledTimeIsntScheduled()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => null);

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Warning, $"{jobMock.Name} does not have any scheduled time, the job is not started.", Times.Once);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            jobMock.Verify(job => job.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TestRemoveDynamicallyAddedJob()
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
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob(jobMock.Object));

            await Task.Delay(100);

            Assert.True(await schedulerManager.RemoveJobAsync(jobMock.Name));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, $"{jobMock.Name} has not been rescheduled since the host is shutting down.", Times.Once);
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

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.False(await schedulerManager.RemoveJobAsync(jobMock.Name));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Error, $"{jobMock.Name} is a system job and cannot be removed.", Times.Once);
        }

        [Fact]
        public async Task TestRetrievingJobsShouldOnlyReturnDynamicJobs()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            var jobMock2 = new Mock<IJob>();
            jobMock2.SetupGet(job => job.Name).Returns(jobMock2.Name);
            jobMock2.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob(jobMock2.Object));

            var jobs = schedulerManager.Jobs.ToArray();

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.DoesNotContain(jobMock.Object, jobs);
            Assert.Contains(jobMock2.Object, jobs);
        }

        [Fact]
        public async Task TestRetrievingSystemJobsShouldOnlyReturnSystemJobs()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            var jobMock2 = new Mock<IJob>();
            jobMock2.SetupGet(job => job.Name).Returns(jobMock2.Name);
            jobMock2.SetupGet(job => job.NextScheduledTime).Returns(() => DateTimeOffset.Now.AddSeconds(1));

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(100);

            var schedulerManager = services.GetRequiredService<SchedulerManager>();

            Assert.True(schedulerManager.AddJob(jobMock2.Object));

            var jobs = schedulerManager.SystemJobs.ToArray();

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Contains(jobMock.Object, jobs);
            Assert.DoesNotContain(jobMock2.Object, jobs);
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

            jobMock.Verify(job => job.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
            loggerMock.Verify(LogLevel.Trace, $"{jobMock.Name} has not been rescheduled since the host is shutting down.", Times.Once);
        }

        [Fact]
        public async Task TestSystemJobsInitializedIsExecuted()
        {
            using var cancellationTokenSource1 = new CancellationTokenSource();
            using var cancellationTokenSource2 = new CancellationTokenSource();
            using var linkedCancellationsTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource1.Token, cancellationTokenSource2.Token);

            var jobMock1 = new Mock<IJob>();

            var job1Executed = false;

            jobMock1.SetupGet(job => job.Name).Returns(jobMock1.Name);
            jobMock1.SetupGet(job => job.NextScheduledTime).Returns(() => !job1Executed ? (DateTimeOffset?) DateTimeOffset.Now.AddMilliseconds(100) : null);

            jobMock1.Setup(job => job.ExecuteAsync(It.IsAny<CancellationToken>()))
                    .Callback((CancellationToken ct) =>
                              {
                                  job1Executed = true;
                                  cancellationTokenSource1.Cancel();
                              });

            var jobMock2 = new Mock<IJob>();
            var job2Executed = false;

            jobMock2.SetupGet(job => job.Name).Returns(jobMock2.Name);
            jobMock2.SetupGet(job => job.NextScheduledTime).Returns(() => !job2Executed ? (DateTimeOffset?) DateTimeOffset.Now.AddMilliseconds(100) : null);

            jobMock2.Setup(job => job.ExecuteAsync(It.IsAny<CancellationToken>()))
                    .Callback((CancellationToken ct) =>
                              {
                                  job2Executed = true;
                                  cancellationTokenSource2.Cancel();
                              });

            var loggerMock = new Mock<ILogger<Scheduler>>();

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock1.Object)
                                      .AddSystemJob(jobMock2.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            linkedCancellationsTokenSource.Token.Register(state =>
                                                          {
                                                              var tcs = (TaskCompletionSource<object>) state;
                                                              tcs.TrySetResult(null);
                                                          }, waitForStop);

            var result = await Task.WhenAny(waitForStop.Task, Task.Delay(10000, CancellationToken.None));

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            // If this assertion fails, something has hanged during execution so that the jobs isn't executed.
            Assert.Equal(waitForStop.Task, result);
        }

        [Fact]
        public async Task TestWaitingJobIsCancelledOnHostStop()
        {
            var loggerMock = new Mock<ILogger<Scheduler>>();

            var executionTime = DateTimeOffset.Now.AddMinutes(1);

            var jobMock = new Mock<IJob>();
            jobMock.SetupGet(job => job.Name).Returns(jobMock.Name);
            jobMock.SetupGet(job => job.NextScheduledTime).Returns(() => executionTime);

            await using var services = new ServiceCollection()
                                      .AddSingleton(loggerMock.Object)
                                      .AddScheduler()
                                      .AddSystemJob(jobMock.Object)
                                      .BuildServiceProvider(true);

            var scheduler = services.GetRequiredService<IHostedService>();

            await scheduler.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(1);

            await scheduler.StopAsync(CancellationToken.None).ConfigureAwait(false);

            loggerMock.Verify(LogLevel.Trace, $"{jobMock.Name} that should have started {executionTime} has been cancelled because the host is shutting down.", Times.Once);
            jobMock.Verify(job => job.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
