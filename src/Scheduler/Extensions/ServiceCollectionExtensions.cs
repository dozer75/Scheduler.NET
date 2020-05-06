using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Foralla.Scheduler.Extensions
{
    /// <summary>
    ///     Different extension methods that aids the configuration of the Scheduler.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds the <typeparamref name="TJob" /> to <paramref name="services" /> to be available as a service for jobs.
        /// </summary>
        /// <typeparam name="TJob">The type of the job instance to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection" /> to add the <typeparamref name="TJob" />.</param>
        /// <returns>The updated <paramref name="services" /> instance.</returns>
        public static IServiceCollection AddJob<TJob>(this IServiceCollection services)
            where TJob : class, IJob
        {
            services.TryAddTransient<TJob>();
            services.AddTransient<IJob, TJob>(p => p.GetRequiredService<TJob>());

            return services;
        }

        /// <summary>
        ///     Adds a scheduler worker to <paramref name="services" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add the scheduler worker.</param>
        /// <returns>The updated <paramref name="services" /> instance.</returns>
        public static IServiceCollection AddScheduler(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.Configure<HostOptions>(options => options.ShutdownTimeout = new TimeSpan(0, 10, 0));

            services.AddHostedService<Scheduler>();
            services.AddSingleton<SchedulerManager>();
            services.TryAddSingleton<ISchedulerManager>(p => p.GetRequiredService<SchedulerManager>());

            return services;
        }

        /// <summary>
        ///     Adds the <typeparamref name="TJob" /> to <paramref name="services" /> so that it is initialized as a static job by
        ///     the scheduler worker.
        /// </summary>
        /// <typeparam name="TJob">The type of the job instance to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection" /> to add the <typeparamref name="TJob" />.</param>
        /// <returns>The updated <paramref name="services" /> instance.</returns>
        /// <remarks>
        ///     The jobs added using this method is not possible to remove during the lifetime of the instance.
        /// </remarks>
        public static IServiceCollection AddSystemJob<TJob>(this IServiceCollection services)
            where TJob : class, IJob
        {
            services.TryAddSingleton<TJob>();
            services.AddSingleton<SystemJob<TJob>>();
            services.AddSingleton<ISystemJob>(provider => provider.GetRequiredService<SystemJob<TJob>>());

            return services;
        }
    }
}
