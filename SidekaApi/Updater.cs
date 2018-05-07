using System;
using System.Collections.Generic;
using System.Text;
using SidekaApi.Models;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SidekaApi.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SidekaApi
{
    public class Updater
    {
        private SidekaDbContext dbContext;
        private IConfiguration Configuration { get; }

        public Updater()
        {
            var builder = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .AddEnvironmentVariables();

            Configuration = builder.Build();

            var optionsBuilder = new DbContextOptionsBuilder<SidekaDbContext>();
            optionsBuilder.UseMySql(Configuration.GetConnectionString("DefaultConnection"));
            dbContext = new SidekaDbContext(optionsBuilder.Options);
        }

        public void Run()
        {
            Console.WriteLine("========= Updating Data =========");
            Console.WriteLine("Fetching Desa");

            var desas = dbContext.SidekaDesa.ToList();

            foreach (var desa in desas)
            {
                Console.WriteLine("Processing Desa {0}-{1}", desa.BlogId, desa.Desa);

                var contentQuery = dbContext.SidekaContent
                    .Where(sc => sc.DesaId == desa.BlogId)
                    .Where(sc => sc.ApiVersion == "2.0");

                Console.WriteLine("Fetching Contents For Desa {0}-{1}", desa.BlogId, desa.Desa);

                var sidekaContents = contentQuery.OrderByDescending(sc => sc.ChangeId).ToList();
                var counter = 0;

                foreach (var sidekaContent in sidekaContents)
                {
                    counter += 1;

                    Console.WriteLine("Processing Content #{0}", counter);

                    var sidekaContentJObject = JsonConvert.DeserializeObject<JObject>(sidekaContent.Content);

                    if (sidekaContent.ApiVersion == "1.0")
                        sidekaContentJObject["columns"] = JArray.FromObject(new string[] { "nik", "nama_penduduk", "tempat_lahir", "tanggal_lahir", "jenis_kelamin", "pendidikan", "agama", "status_kawin", "pekerjaan", "pekerjaan_ped", "kewarganegaraan", "kompetensi", "no_telepon", "email", "no_kitas", "no_paspor", "golongan_darah", "status_penduduk", "status_tinggal", "kontrasepsi", "difabilitas", "no_kk", "nama_ayah", "nama_ibu", "hubungan_keluarga", "nama_dusun", "rw", "rt", "alamat_jalan" });

                    try
                    {
                        Console.WriteLine("Calculating Sizes....");

                        var content = new SidekaContentViewModel(sidekaContentJObject);
                        var contentSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(content.Data));
                        var diffSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(content.Diffs));

                        Console.WriteLine("Desa {0}-Change Id {1}, Content Size {2}, Diff Size {3}", desa.Desa, sidekaContent.ChangeId, contentSize, diffSize);
                        Console.WriteLine("Saving Size...");

                        sidekaContent.ContentSize = contentSize;
                        sidekaContent.DiffSize = diffSize;

                        dbContext.Update(sidekaContent);
                        dbContext.SaveChanges();

                        Console.WriteLine("Sizes Have Been Saved");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error When Calculating Size Desa {0}-{1}: {2}", desa.BlogId, desa.Desa, ex.Message);
                    }
                }
            }
        }
    }
}
