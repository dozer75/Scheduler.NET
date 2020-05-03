[![GitHub Workflow Status (branch)](https://img.shields.io/github/workflow/status/dozer75/Scheduler.NET/CI%20-%20master/master?label=Continuous%20integration%20build&style=plastic)](https://github.com/dozer75/Scheduler.NET/actions?query=workflow%3A%22CI+-+master%22)
[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/dozer75/Scheduler.NET/Release%20-%20NuGet?label=NuGet%20build&style=plastic)](https://github.com/dozer75/Scheduler.NET/actions?query=workflow%3A%22Release+-+NuGet%22)

[![NuGet Status](https://img.shields.io/nuget/v/Foralla.Scheduler?label=NuGet%20version&style=plastic)](https://www.nuget.org/packages/Foralla.Scheduler/)

[![GitHub](https://img.shields.io/github/license/dozer75/Scheduler.NET?label=License&style=plastic)](https://github.com/dozer75/Scheduler.NET/blob/master/LICENSE)

# Foralla.Scheduler

Foralla.Scheduler is a small and simple scheduler framework designed around the [hosted services in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services). 

The framework supports:
- Cron expressions for scheduling
- Static defined jobs
- Add/remove jobs dynamically

## Main contents
- Cron expressions
- Requirements
- Adding Foralla.Scheduler support to your application
- Implementing a job
- Defining a system job
- Adding jobs dynamically
- Removing jobs
- Date/Time gotchas
- Future


## Cron expressions

The basic scheduling definition is done using cron expressions. This framework uses the Cronos .NET library to parse cron expressions. 
Please refer to the [Cronos .NET library project](https://github.com/HangfireIO/Cronos) for a description of supported cron expressions.

## Requirements

This project supports both .NET Standard 2.0 or newer.

Since the project is designed around hosted services, any implemtations must use either [Generic Host](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host) or 
[Web Host](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host) provided by ASP.NET Core to be able to run. 

## Adding Foralla.Scheduler support to your application

To enable the Foralla.Scheduler in your project install the `Foralla.Scheduler` NuGet package to your project using your favorite NuGet package tool.

When the NuGet package is installed, add the scheduler to the services using the `AddScheduler()` method.

	public void ConfigureServices(IServiceCollection services)
	{
		// Other services
		services.AddScheduler();
	}

The `AddScheduler` method returns the `IServiceCollection` instance it is executed on for method chaining.

The `Generic Host`/`Web Host` framework will then recognize the scheduler and start it as a background service that lives as long as the process is running.

## Implementing a job

To implement a job inherit the `CronJob` abstract class and add your implementation to it. It is possible to rather implement the `IJob` interface to provide your own custom job 
scheduler, however this example uses the `CronJob`.

	public sealed class HelloJob : CronJob
	{
		public override string Expression { get; } = "*/10 * * * * *";

		// NOTE: Name MUST be unique within the current project!
		public override string Name { get; } = Guid.NewGuid().ToString();

		public override Task ExecuteAsync(CancellationToken stoppingToken)
		{
			Console.WriteLine($"Hello from {Name} at {DateTimeOffset.Now}!");

			return Task.CompletedTask;
		}
	}

The `stoppingToken` is cancelled whenever the host is about to be shut down and should be checked in long running operations to gracefully stop the job.

NOTE: The expression can be configured to be dynamic. However it will first be triggered based on a changed expression after the next execution. On a dynamic job
it is possible to remove the existing job and then add the updated job to make the new expression to be used directly.

## Defining a system job

System jobs is defined by jobs that are added to the scheduler during startup when configuring the services. 

	public void ConfigureServices(IServiceCollection services)
	{
		// Other services
		services.AddSystemJob<HelloJob>();
		// and/or
		services.AddSystemJob(new HelloJob());
	}

The `AddSystemJob` method returns the `IServiceCollection` instance it is executed on for method chaining. All system jobs are added as singletons.

A system job cannot be removed during the lifetime of the process. To be able to control the lifetime of a job add it dynamically using the 
`IScheduleManager.AddJob` as described below.

## Adding jobs dynamically

During the lifetime of an executing instance it is possible to add jobs dynamically. This is done by using the `ISchedulerManager` instance that can be retrieved 
using dependency injection. The implementation of `ISchedulerManager` communicates with the scheduler to manage it runtime.

To add a job dynamically use the `ISchedulerManager.AddJob(IJob job)` method.

	public class MySchedulerManagerCommuicator
	{
		private readonly ISchedulerManager _manager;

		public MySchedulerManagerCommuicator(ISchedulerManager manager)
		{
			_manager = manager;
		}

		public void AddJob()
		{
			_manager.AddJob(new HelloJob());
		}
	}

Unlike a system job a dynamically added job can be removed runtime.

## Removing jobs

To remove a job that is scheduled use the `ISchedulerManager.RemoveJobAsync` method.

	public async Task RemoveJobAsync()
	{
		await _manager.RemoveJobAsync("MyJobName");
	}

Please note:
- A system job CANNOT be removed.
- A job that is already running runs to it's completion before beeing removed.

## Date/Time gotchas

The scheduler works with `DateTimeOffset` for all timing operations. All timings is using local timezone. If a custom implementation of `IJob` is done, 
make sure that the DateTimeOffset returned by the implementation returns a local `DateTimeOffset`.

## Future

This is the 0.1.0 version of the scheduler. For now it doesn't persist the configured jobs or support for clustered executions. Both of these are planned to be supported 
in the future.

### Persisting jobs

The plan is to provide interface based persistance logic that provides a couple of default implementations, but also enables the user of this library
to create its own implementation.

### Clustering

The plan is to have support for clustering or distributed scheduler to be able to run multiple schedulers that uses the same configuration. The plan for this is to use
[Redis Distributed Locks](https://redis.io/topics/distlock) using the [RedLock.net Framework](https://github.com/samcook/RedLock.net). This will also be replaceable since
it will be implemented using an interface based locking logic.

Other relevant locking strategies could be using e.g. 
[SQL Server Application locks](https://docs.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql?redirectedfrom=MSDN&view=sql-server-ver15) 
using the [DistributedLock package](https://github.com/madelson/DistributedLock).
