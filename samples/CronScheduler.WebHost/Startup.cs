using System;
using System.Diagnostics.CodeAnalysis;
using CronScheduler.WebHost.Schedulers;
using CronScheduler.WebHost.Services;
using Foralla.Scheduler.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CronScheduler.WebHost
{
    [SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Used as a generic type")]
    public class Startup
    {
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();
            ;
            services.AddHttpClient<IJobService, JobService>(client => client.BaseAddress = new Uri("https://localhost:5001/api/"));

            services.AddScheduler()
                    .AddJob<OnDemandJob>()
                    .AddSystemJob<SystemJob>();
        }
    }
}
