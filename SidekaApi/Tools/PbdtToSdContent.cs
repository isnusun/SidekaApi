using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SidekaApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SidekaApi.Tools
{
    public class PbdtToSdContent
    {
        private SidekaDbContext dbContext;
        private IConfiguration configuration;

        public PbdtToSdContent()
        {
            configuration = SidekaDbContext.CreateConfigurationForTools();
            dbContext = SidekaDbContext.CreateForTools(configuration);
        }

        public void run(string regionCode, string subtype)
        {
            var desa = dbContext.SidekaDesa.FirstOrDefault(s => s.Kode == regionCode);
            var pbdt = dbContext.Pbdt2015.FirstOrDefault(s => s.RegionCode == regionCode);

            var content = new SidekaContent()
            {
                DesaId = desa.BlogId,
                ApiVersion = configuration.GetValue<string>("ApiVersion"),
                ChangeId = 1,
                Type = "Kemiskinan",
                Content = pbdt.Content,
                DiffSize = 0,
                Subtype = subtype,
                ContentSize = ASCIIEncoding.Unicode.GetByteCount(pbdt.Content)
            };

            dbContext.Set<SidekaContent>().Add(content);
            dbContext.SaveChanges();
        }
    }
}
