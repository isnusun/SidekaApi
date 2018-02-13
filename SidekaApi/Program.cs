using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using Serilog;
using SidekaApi.Helpers;

namespace SidekaApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = LogConfigurationHelper.GetConfiguration();

            try
            {
                var host = BuildWebHost(args);
                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseKestrel()
                .UseUrls("http://0.0.0.0:59999")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()                                
                .UseStartup<Startup>()
                .UseSerilog()
                .Build();
    }
}
