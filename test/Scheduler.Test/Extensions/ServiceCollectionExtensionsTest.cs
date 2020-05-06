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
        public void TestAddJobType()
        {
            var serviceCollection = new ServiceCollection()
               .AddJob<TestJob>();

            var services = serviceCollection
               .BuildServiceProvider(true);

            var serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(TestJob));
            Assert.Equal(typeof(TestJob), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);

            serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(IJob));
            Assert.NotNull(serviceDescriptor.ImplementationFactory);
            Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);

            var jobInstance = Assert.IsType<TestJob>(services.GetRequiredService<TestJob>());
            var iJobInstance = Assert.IsType<TestJob>(services.GetRequiredService<IJob>());
            Assert.NotEqual(jobInstance, iJobInstance);
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
        public void TestAddSystemJobType()
        {
            var serviceCollection = new ServiceCollection()
               .AddSystemJob<TestJob>();

            var services = serviceCollection
               .BuildServiceProvider(true);

            var serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(TestJob));
            Assert.Equal(typeof(TestJob), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(IJob));
            Assert.NotNull(serviceDescriptor.ImplementationFactory);
            Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);

            serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(SystemJob<TestJob>));
            Assert.Equal(typeof(SystemJob<TestJob>), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(ISystemJob));
            Assert.NotNull(serviceDescriptor.ImplementationFactory);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

            var jobInstance = Assert.IsType<TestJob>(services.GetRequiredService<TestJob>());
            var iJobInstance = Assert.IsType<TestJob>(services.GetRequiredService<IJob>());
            var systemJob = Assert.IsType<SystemJob<TestJob>>(services.GetRequiredService<SystemJob<TestJob>>());
            var iSystemJob = Assert.IsType<SystemJob<TestJob>>(services.GetRequiredService<ISystemJob>());
            Assert.NotEqual(jobInstance, iJobInstance);
            Assert.Equal(systemJob, iSystemJob);
            Assert.Equal(systemJob.Job, systemJob.Job);
            Assert.NotEqual(jobInstance, systemJob.Job);
        }
    }
}
