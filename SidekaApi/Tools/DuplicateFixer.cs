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
    public class DuplicateFixer
    {
        public void Run()
        {
            using (var dbContext = SidekaDbContext.CreateForTools())
            {
                Console.WriteLine("========= Updating Data =========");
                Console.WriteLine("Fetching Desa");

                var desas = dbContext.SidekaDesa.OrderBy(d => d.BlogId).ToList();
                var contentTypes = new string[] { "penduduk", "pemetaan" };

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

                            foreach (var tab in viewModel.Data.Keys.ToArray())
                            {
                                var data = viewModel.Data[tab];
                                var columns = viewModel.Columns[tab];
                                var idGetter = GetIdGetter(columns);

                                var ids = new HashSet<string>();
                                var duplicates = new List<int>();
                                for(var i = 0; i < data.Length; i++)
                                {
                                    var id = idGetter(data[i]);
                                    if(ids.Contains(id))
                                    {
                                        duplicates.Add(i);
                                    }
                                    ids.Add(id);
                                }
                                if(duplicates.Count > 0)
                                {
                                    duplicates.Reverse();
                                    Console.WriteLine("Tab {0} has duplicates: {1}", tab, string.Join(", ", duplicates));
                                    var list = data.ToList();
                                    foreach(var i in duplicates)
                                    {
                                        list.RemoveAt(i);
                                    }
                                    data = viewModel.Data[tab] = list.ToArray();
                                    hasChanges = true;
                                }
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
