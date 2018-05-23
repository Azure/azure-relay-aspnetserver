using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Relay.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SelfHostServer
{
    public class Startup
    {
        static string connectionString = Environment.GetEnvironmentVariable("SB_HC_CONNECTIONSTRING");

        public static void Main(string[] args)
        {
            if ( args.Length > 0)
            {
                connectionString = args[0];
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine($"dotnet {Path.GetFileName(typeof(Startup).Assembly.Location)} [connection string]");
                return;
            }

            RunAsync(connectionString).GetAwaiter().GetResult();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Server options can be configured here instead of in Main.
            services.Configure<AzureRelayOptions>(options =>
            {
                
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Hello world from " + context.Request.Host + " at " + DateTime.Now);
            });
        }
        
        private static Task RunAsync(string connectionString)
        {
            var host = new WebHostBuilder()
                .ConfigureLogging(factory => factory.AddConsole())
                .UseStartup<Startup>()
                .UseAzureRelay(options =>
                {
                    options.UrlPrefixes.Add(connectionString);
                })
                .Build();

            host.Run();

            return Task.CompletedTask;
        }
    }
}
