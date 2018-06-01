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
using SidekaApi.Tools;

namespace SidekaApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = LogConfigurationHelper.GetConfiguration();
            
            try
            {
                if (args.Length > 0 && args[0] == "--update-sizes")
                {
                    new SizeUpdater().Run();
                }
                else if (args.Length > 0 && args[0] == "--pbdt-import")
                {
                    new PbdtXlsxToSql().Run(@"D:\Documents", "22. SEMARANG", "33.22");
                   
                }
                else if (args.Length > 0 && args[0] == "--pbdt-to-sdcontent")
                {
                    new PbdtToSdContent().run("33.22.20.2009", "2015");
                }
                else if (args.Length > 0 && args[0] == "--fix-duplicates")
                {
                    new DuplicateFixer().Run();
                }
                else
                {
                    var host = BuildWebHost(args);
                    host.Run();
                }
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
                .UseUrls("http://0.0.0.0:5001")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()                                
                .UseStartup<Startup>()
                .UseSerilog()
                .Build();
    }
}
