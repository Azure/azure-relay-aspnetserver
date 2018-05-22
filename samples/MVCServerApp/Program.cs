using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace MVCServerApp
{
    public class Program
    {
        static string connectionString = Environment.GetEnvironmentVariable("SB_HC_CONNECTIONSTRING");

        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                connectionString = args[0];
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine($"dotnet {Path.GetFileName(typeof(Startup).Assembly.Location)} [connection string]");
                return;
            }

            BuildWebHost(connectionString).Run();
        }

        public static IWebHost BuildWebHost(string connectionString) =>
            new WebHostBuilder()
                .ConfigureLogging(factory => { factory.AddConsole(); factory.AddDebug(); })
                .UseStartup<Startup>()
                .UseAzureRelay(options =>
                {
                    options.UrlPrefixes.Add(connectionString);
                })
                .UseContentRoot(Path.GetFullPath(@"."))
                .UseWebRoot(Path.GetFullPath(@".\wwwroot"))
                .Build();
    }
}
