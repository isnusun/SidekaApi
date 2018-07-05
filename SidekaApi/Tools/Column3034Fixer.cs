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

namespace SidekaApi.Tools
{
    public class Column3034Fixer
    {
        public void Run()
        {
            using (var dbContext = SidekaDbContext.CreateForTools())
            {
                Console.WriteLine("========= Updating Data =========");
                Console.WriteLine("Fetching Desa");

                var desas = dbContext.SidekaDesa.OrderBy(d => d.BlogId).ToList();
                var contentTypes = new string[] { "penduduk" };

                foreach (var desa in desas)
                {
                    foreach(var contentType in contentTypes)
                    {
                        Console.WriteLine("Processing Desa {0}-{1} {2}", desa.BlogId, desa.Desa, contentType);

                        var contentQuery = dbContext.SidekaContent
                            .Where(sc => sc.DesaId == desa.BlogId)
                            .Where(sc => sc.ApiVersion == "2.0")
                            .Where(sc => sc.Type == contentType)
                            .OrderByDescending(sc => sc.Id);

                        try
                        {
                            var sidekaContent = contentQuery.AsNoTracking().FirstOrDefault();
                            if (sidekaContent == null)
                                continue;

                            Console.WriteLine("Content Change ID {0}", sidekaContent.ChangeId);

                            var jObject = JsonConvert.DeserializeObject<JObject>(sidekaContent.Content);
                            var viewModel = new SidekaContentViewModel(jObject);

                            bool hasChanges = false;

                            if(!viewModel.Columns.ContainsKey("penduduk") 
                                || !viewModel.Data.ContainsKey("penduduk") 
                                || viewModel.Columns["penduduk"].Columns.Length != 34
                                || viewModel.Data["penduduk"].Length == 0)
                                continue;

                            bool all30Columns = true;
                            var data = viewModel.Data["penduduk"];
                            foreach(var row in data){
                                if(((object[]) row).Length != 30)
                                {
                                    all30Columns = false;
                                    break;
                                }
                            }

                            if(all30Columns){
                                Console.WriteLine("All 30 columns");
                            }


                            if (hasChanges)
                            {
                                var newContent = JsonConvert.SerializeObject(viewModel);
                                var contentSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(viewModel.Data));
                                var diffSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(viewModel.Diffs));
                                var updatedContent = new SidekaContent
                                {
                                    Id = sidekaContent.Id,
                                    ContentSize = contentSize,
                                    Content = newContent,
                                    DiffSize = diffSize
                                };
                                dbContext.Attach(updatedContent);
                                dbContext.Entry(updatedContent).Property(c => c.ContentSize).IsModified = true;
                                dbContext.Entry(updatedContent).Property(c => c.DiffSize).IsModified = true;
                                dbContext.Entry(updatedContent).Property(c => c.Content).IsModified = true;
                                dbContext.SaveChanges();

                                Console.WriteLine("Content updated");
                            }
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine(e);
                            Console.WriteLine(e.StackTrace);
                        }
                    }
                }
            }
        }

        private static Func<Object, String> GetIdGetter(SidekaColumnConfig config){
            if(config.IsDict)
                return o => (string)((Dictionary<string, object>) o)["id"];
            else{
                var index = Array.IndexOf(config.Columns, ("id"));
                return o => (String) ((object[])o)[index];
            }
        }
    }
}
