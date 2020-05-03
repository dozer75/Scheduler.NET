using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Foralla.Scheduler.Test
{
    public class SchedulerManagerTest
    {
        [Fact]
        public void TestAddJob()
        {
            var jobMock = new Mock<IJob>();

            var schedulerManager = new SchedulerManager();

            schedulerManager.JobAdded += (sender, args) =>
                                         {
                                             Assert.Equal(schedulerManager, sender);
                                             Assert.Equal(jobMock.Object, args.Job);
                                             args.Success = true;
                                         };

            Assert.True(schedulerManager.AddJob(jobMock.Object));
        }

        [Fact]
        public void TestAddJobNoEventSubscription()
        {
            var jobMock = new Mock<IJob>();

            var schedulerManager = new SchedulerManager();

            Assert.False(schedulerManager.AddJob(jobMock.Object));
        }

        [Fact]
        public void TestGetJobs()
        {
            var jobMock = new Mock<IJob>();
            var schedulerManager = new SchedulerManager();

            schedulerManager.GetJobs += (sender, args) =>
                                        {
                                            Assert.Equal(schedulerManager, sender);
                                            Assert.False(args.SystemJobs);

                                            args.Jobs = new[]
                                                        {
                                                            jobMock.Object
                                                        };
                                        };

            var jobs = schedulerManager.Jobs.ToArray();

            var job = Assert.Single(jobs);

            Assert.Equal(jobMock.Object, job);
        }

        [Fact]
        public void TestGetJobsNoEventSubscription()
        {
            var schedulerManager = new SchedulerManager();

            Assert.Null(schedulerManager.Jobs);
        }

        [Fact]
        public async Task TestRemoveJob()
        {
            var schedulerManager = new SchedulerManager();

            schedulerManager.JobRemoved += (sender, args) =>
                                           {
                                               Assert.Equal(schedulerManager, sender);
                                               Assert.Equal("Job", args.Name);

                                               args.Success = Task.FromResult(true);
                                           };

            Assert.True(await schedulerManager.RemoveJobAsync("Job"));
        }

        [Fact]
        public async Task TestRemoveJobNoEventSubscription()
        {
            var schedulerManager = new SchedulerManager();

            Assert.False(await schedulerManager.RemoveJobAsync("Job"));
        }

        [Fact]
        public void TestSystemGetJobs()
        {
            var jobMock = new Mock<IJob>();
            var schedulerManager = new SchedulerManager();

            schedulerManager.GetJobs += (sender, args) =>
                                        {
                                            Assert.Equal(schedulerManager, sender);
                                            Assert.True(args.SystemJobs);

                                            args.Jobs = new[]
                                                        {
                                                            jobMock.Object
                                                        };
                                        };

            var jobs = schedulerManager.SystemJobs.ToArray();

            var job = Assert.Single(jobs);

            Assert.Equal(jobMock.Object, job);
        }

        [Fact]
        public void TestSystemGetJobsNoEventSubscription()
        {
            var schedulerManager = new SchedulerManager();

            Assert.Null(schedulerManager.SystemJobs);
        }
    }
}
