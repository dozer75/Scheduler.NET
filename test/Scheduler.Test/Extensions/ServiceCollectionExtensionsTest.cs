using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foralla.Scheduler.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Foralla.Scheduler.Test.Extensions
{
    public class ServiceCollectionExtensionsTest
    {
        private class TestJob : IJob
        {
            public string Name => nameof(TestJob);
            public DateTimeOffset? NextScheduledTime => DateTimeOffset.Now.AddSeconds(1);

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void TestAddScheduler()
        {
            var serviceCollection = new ServiceCollection()
                                   .AddLogging()
                                   .AddScheduler();

            var serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(IHostedService));
            Assert.Equal(typeof(Scheduler), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(ISchedulerManager));
            Assert.NotNull(serviceDescriptor.ImplementationFactory);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(SchedulerManager));
            Assert.Equal(typeof(SchedulerManager), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            var services = serviceCollection
               .BuildServiceProvider(true);

            var hostOptions = services.GetRequiredService<IOptions<HostOptions>>();

            Assert.NotNull(hostOptions.Value);
            Assert.Equal(TimeSpan.FromMinutes(10), hostOptions.Value.ShutdownTimeout);

            Assert.IsType<Scheduler>(services.GetRequiredService<IHostedService>());

            var interfacedSchedulerManager = Assert.IsType<SchedulerManager>(services.GetRequiredService<ISchedulerManager>());

            Assert.Equal(interfacedSchedulerManager, services.GetRequiredService<SchedulerManager>());
        }

        [Fact]
        public void TestAddSystemJobInstance()
        {
            var testJob = new TestJob();

            var serviceCollection = new ServiceCollection()
               .AddSystemJob(testJob);

            var serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(IJob));
            Assert.Equal(typeof(IJob), serviceDescriptor.ServiceType);
            Assert.Equal(testJob, serviceDescriptor.ImplementationInstance);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            var services = serviceCollection
               .BuildServiceProvider(true);

            Assert.Equal(testJob, services.GetRequiredService<IJob>());
        }

        [Fact]
        public void TestAddSystemJobType()
        {
            var serviceCollection = new ServiceCollection()
               .AddSystemJob<TestJob>();

            var serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(IJob));
            Assert.Equal(typeof(IJob), serviceDescriptor.ServiceType);
            Assert.Equal(typeof(TestJob), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            var services = serviceCollection
               .BuildServiceProvider(true);

            Assert.IsType<TestJob>(services.GetRequiredService<IJob>());
        }
    }
}
