using System.Reflection;
using System.Threading.Tasks;
using Foralla.Scheduler.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Foralla.Scheduler.Cron.Demo
{
    internal static class Program
    {
        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                       .ConfigureAppConfiguration((context, builder) =>
                                                  {
                                                      builder.AddCommandLine(args);
                                                      builder.AddJsonFile("appsettings.json", true, true);
                                                      builder.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", true, true);

                                                      if (context.HostingEnvironment.IsDevelopment())
                                                      {
                                                          builder.AddUserSecrets(Assembly.GetEntryAssembly(), true);
                                                      }
                                                  })
                       .ConfigureServices((context, services) =>
                                          {
                                              services.AddSingleton(context.Configuration);

                                              services.AddScheduler()
                                                      .AddSystemJob<JobStarter>();
                                          });
        }

        private static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync().ConfigureAwait(false);
        }
    }
}
