[![GitHub Workflow Status (branch)](https://img.shields.io/github/workflow/status/dozer75/Scheduler.NET/CI%20-%20master/master?label=Continuous%20integration%20build&style=plastic)](https://github.com/dozer75/Scheduler.NET/actions?query=workflow%3A%22CI+-+master%22)
[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/dozer75/Scheduler.NET/Release%20-%20NuGet?label=NuGet%20build&style=plastic)](https://github.com/dozer75/Scheduler.NET/actions?query=workflow%3A%22Release+-+NuGet%22)

[![NuGet Status](https://img.shields.io/nuget/v/Foralla.Scheduler?label=NuGet%20version&style=plastic)](https://www.nuget.org/packages/Foralla.Scheduler/)

[![GitHub](https://img.shields.io/github/license/dozer75/Scheduler.NET?label=License&style=plastic)](https://github.com/dozer75/Scheduler.NET/blob/master/LICENSE)

# Foralla.Scheduler

Foralla.Scheduler is a small and simple scheduler framework designed around the [hosted services in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services). 
It is built with .NET Standard 2.0 as a base and should work with all .NET platforms that supports this.

The framework supports:
- Cron expressions for scheduling
- Static defined jobs
- Add/remove jobs dynamically

## Main contents
- Cron expressions
- Requirements
- About jobs
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

## About jobs

In the Foralla.Scheduler, there are two different kind of jobs. A system job and a dynamic job.

The system job is a job that is started when the host starts. It cannot be removed during the lifetime of the host. 

The dynamic job is a job that can also be started at any time during the lifetime of the host. It can be removed at any time.

All jobs originates from isntances that implements the `IJob` interface. This interface contains the basic information that the scheduler needs to run a job.

- `Name`
  - A globally unique name for this scheduled job. If there are several jobs with identically name, only the first one is scheduled, other jobs are ignored with a warning.
- `NextScheduledTime`
  - Identifies the next execution of the scheduled job. It is defined as a `DateTimeOffset`. See the section [Date/Time gotchas](#date-time-gotchas) for more information.
- `ExecuteAsync`
  - Is called by the scheduler when the `NextScheduledTime` is reached and must contain the job that is about to be executed. A `CancellationToken` is supplied to the 
  method from the scheduler that should be monitored since it indicates if the scheduler host is about to be stopped. If the token is triggered the implementation should honor 
  this and cancel any running operation as soon as possible.

It is possible for to implement own custom `IJob` implementation, but the recommendation is to use the `CronJob` base class for to take advantage of Cron expressions to define
the recurring scheduling of the job. It also supports start- and endtime for the job.

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

To implement a job inherit the `CronJob` abstract class and add your implementation to it. As mentioned, it is possible to rather implement the `IJob` interface to provide your 
own custom job scheduler, however this example uses the `CronJob`.

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
	}

The `AddSystemJob` method returns the `IServiceCollection` instance it is executed on for method chaining. All system jobs are added as singletons.

A system job cannot be removed during the lifetime of the process. To be able to control the lifetime of a job add it dynamically using the 
`IScheduleManager.AddJob` as described below.

### About system job injections

By default a system job is added as a singleton. This can however be overridden by the implementor by simply adding the configure the job prior to the `AddSystemJob`.

	public void ConfigureServices(IServiceCollection services)
	{
		// Other services
		services.AddTransient<HelloJob>() // Changes the HelloJob to be transient instead of singleton.
                    .AddSystemJob<HelloJob>();
	}

## Adding jobs dynamically

During the lifetime of an executing instance it is possible to add jobs dynamically. This is done by using the `ISchedulerManager` instance that can be retrieved 
using dependency injection. The implementation of `ISchedulerManager` communicates with the scheduler to manage it runtime.

There are two different methods to do this; `AddJob<IJob>(Action<IJob> setup)` and `AddJob(IJob job)`. 

### `AddJob<IJob>(Action<IJob> setup)`
`AddJob<IJob>(Action<IJob> setup)` is the recommended method. By using this the `IJob` is retrieved from the host service provider and the caller can use
the `jobSetup` to customize the `IJob` instance. To be able to use the `AddJob<IJob>(Action<IJob> setup)` method, the job to be added **MUST**
be configured as a service during startup.

To add a job dynamically use the `ISchedulerManager.AddJob(IJob job)` method.

	public void ConfigureServices(IServiceCollection services)
	{
		// Other services
		services.AddJob<HelloJob>();
	}

It's then available for addition to the scheduler.

	public class MySchedulerManagerCommuicator
	{
		private readonly ISchedulerManager _manager;

		public MySchedulerManagerCommuicator(ISchedulerManager manager)
		{
			_manager = manager;
		}

		public void AddJob()
		{
			_manager.AddJob<HelloJob>(setup => // Do any custom configuration);
		}
	}


### `AddJob(IJob job)`

`AddJob(IJob job)` is an alternative set to initialize a job. It does not use the service provider and the implementor is responsible for handling the job.

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

As the work with this scheduler progress this version of the method **MAY** be obsolete, so it is recommended to make use of the first version.

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

## Examples

Some simple examples are provided in the `samples` folder. These are mainly used to proof of concept the scheduler, but can be used as an example of the possibilities
of the scheduler.

## Build the project

It's simple, clone the code and compile it in your favorite .NET Core development environment.

## Issues/bugs

Can be reported using the issues handling. Questions can also be asked using issues for now.

If you have a issue/bug, please describe it with either an examplecode to test or a detailed steps to reproduce.

## Contributions

TO BE DETERMINED.

## Future

This is the 0.2.0 version of the scheduler. For now it doesn't persist the configured jobs or support for clustered executions. Both of these are planned to be supported 
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
