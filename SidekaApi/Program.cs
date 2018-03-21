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
                if (args.Length > 0 && args[0] == "--update-sizes-penduduk")
                {
                    UpdatePendudukSizesData();
                }
                else if (args.Length > 0 && args[0] == "--update-sizes-pemetaan")
                {
                    UpdatePemetaanSizesData();
                }
                else if (args.Length > 0 && args[0] == "--update-sizes-keuangan")
                {
                    UpdateKeuanganSizesData();
                }
                else if (args.Length > 0 && args[0] == "--update-sizes")
                {
                    Console.WriteLine("Please specify the command -penduduk or -pemetaan or -keuangan");
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
            
        }

        public static void UpdatePendudukSizesData()
        {
            var updater = new Updater();
            updater.Run("penduduk", null);
        }

        public static void UpdatePemetaanSizesData()
        {
            var updater = new Updater();
            updater.Run("pemetaan", null);
        }

        public static void UpdateKeuanganSizesData()
        {
            var updater = new Updater();
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
