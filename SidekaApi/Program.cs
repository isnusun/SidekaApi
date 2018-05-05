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
                var importer = new PbdtXlsxToSql();
                importer.Run(@"D:\Documents", "22. SEMARANG", "33.22");

                //var pbdtToSdContent = new PbdtToSdContent();
                //pbdtToSdContent.run("33.22.20.2009", "2015");

                if (args.Length > 0 && args[0] == "--update-sizes")
                {
                    UpdateSizes();
                }
                else if (args.Length > 0 && args[0] == "--pbdt-import")
                {
                   
                }
                else if (args.Length > 0 && args[0] == "--pbdt-to-sdcontent")
                {

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

        public static void UpdateSizes()
        {
            var updater = new Updater();
            updater.Run("penduduk", null);
            updater.Run("pemetaan", null);
            updater.Run("keuangan", null);
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
