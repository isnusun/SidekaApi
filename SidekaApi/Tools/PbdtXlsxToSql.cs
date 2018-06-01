using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OfficeOpenXml;
using SidekaApi.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace SidekaApi.Tools {

    public class PbdtXlsxToSql {

        private SidekaDbContext dbContext;
  
        public PbdtXlsxToSql()
        {
            dbContext = SidekaDbContext.CreateForTools();
        }

        public void Run(string rootDirectory, string fileName, string kabupatenCode)
        {
            dbContext.SidekaRegion.Take(10);

            var idvFile = Path.Combine(rootDirectory, "1. JATENG_IDV", fileName + "_IDV.xlsx");

            if(!File.Exists(idvFile)){
                throw new Exception(idvFile + "Not exists");
            }

            var rtFile = Path.Combine(rootDirectory, "2. JATENG_RT", fileName + "_RT.xlsx");

            if(!File.Exists(rtFile)){
                throw new Exception(rtFile + "Not exists");
            }

            var byDesa = new Dictionary<String, Tuple<List<object[]>, List<object[]>>>();
            var desaTuples = new Dictionary<String, Tuple<String, String>>();

            using(var package = new ExcelPackage(new FileInfo(idvFile)))
            {
                var sheet = package.Workbook.Worksheets.FirstOrDefault(s => s.Name == "Sheet1");
                int emptyRows = 0;
                for (int i = sheet.Cells.Start.Row, endRow = sheet.Cells.End.Row; i < endRow; i++) {
                    Console.WriteLine("idv {0}\t of \t{1}", i, endRow);

                    var objs = new List<object>();
                    for (int j = sheet.Cells.Start.Column, endColumn = Math.Min(sheet.Cells.End.Column, 40); j < endColumn; j++) {
                        objs.Add(sheet.Cells[i, j].Value);
                    }
                    var idDesa = objs[0] as String;

                    if (idDesa == null || idDesa.Trim().ToUpper() == "KODE WILAYAH")
                    {
                        emptyRows++;

                        if (emptyRows > 10)
                        {
                            break;
                        }
                        continue;
                    }
                    emptyRows = 0;

                    if (!byDesa.ContainsKey(idDesa)){
                        byDesa[idDesa] = Tuple.Create(new List<object[]>(), new List<object[]>());
                    }
                    byDesa[idDesa].Item1.Add(objs.ToArray());
                    desaTuples[idDesa] = Tuple.Create((String) objs[3], (String) objs[4] );
                    
                }
            }

            using(var package = new ExcelPackage(new FileInfo(rtFile)))
            {
                var sheet = package.Workbook.Worksheets.FirstOrDefault(s => s.Name == "Sheet1");
                int emptyRows = 0;
                for (int i = sheet.Cells.Start.Row, endRow = sheet.Cells.End.Row; i < endRow; i++)
                {
                    Console.WriteLine("rt {0}\t of \t{1}", i, endRow);
                    var objs = new List<object>();

                    for (int j = sheet.Cells.Start.Column, endColumn = Math.Min(sheet.Cells.End.Column, 80); j < endColumn; j++)
                    {
                        objs.Add(sheet.Cells[i, j].Value);
                    }
                    var idDesa = objs[0] as String;
                    if (idDesa == null || idDesa.Trim().ToUpper() == "KODE WILAYAH")
                    {
                        emptyRows++;

                        if (emptyRows > 10)
                        {
                            break;
                        }
                        continue;
                    }
                    emptyRows = 0;

                    if (!byDesa.ContainsKey(idDesa)){
                        byDesa[idDesa] = Tuple.Create(new List<object[]>(), new List<object[]>());
                    }
                    byDesa[idDesa].Item2.Add(objs.ToArray());
                    desaTuples[idDesa] = Tuple.Create((String) objs[3], (String) objs[4]);
                   
                }
            }

            foreach(var idDesa in byDesa.Keys){
                var desaTuple = desaTuples[idDesa];
                var kecamatan = dbContext.Set<SidekaRegion>()
                    .FirstOrDefault(r => r.RegionName.ToUpper() == desaTuple.Item1.ToUpper() && r.ParentCode == kabupatenCode);
                if(kecamatan == null){
                    Console.WriteLine("Kecamatan "+idDesa+" "+desaTuple.Item1+"not exists");
                    continue;
                }
                var desa = dbContext.Set<SidekaRegion>()
                    .FirstOrDefault(r => r.RegionName.ToUpper() == desaTuple.Item2.ToUpper() && r.ParentCode == kecamatan.RegionCode);

                if (desa == null){
                    Console.WriteLine("Desa "+idDesa+" "+desaTuple+"not exists");
                    continue;
                }

                var existingData = dbContext.Pbdt2015
                        .FirstOrDefault(s => s.RegionCode == desa.RegionCode);

                if (existingData == null || existingData.IsImported == false)
                {
                    Console.WriteLine("Writing new PBDT...");
                    var json = ConvertToJson(byDesa[idDesa].Item1, byDesa[idDesa].Item2);
                    Pbdt2015 data = new Pbdt2015
                    {
                        RegionCode = desa.RegionCode,
                        Content = json,
                        IsImported = true
                    };
                    dbContext.Set<Pbdt2015>().Add(data);
                    Console.WriteLine("Desa " + desaTuple + " finished");
                }
            }

            dbContext.SaveChanges();
        }

        private String ConvertToJson(List<object[]> idvs, List<object[]> rts) {
            var pbdtIdvData = ParsePbdtIdv(idvs);
            var pbdtRtData = ParsePbdtRt(rts);

            var bundle = new Dictionary<string, object>();

            bundle["data"] = new Dictionary<string, object>()
            {
                {"pbdtIdv", pbdtIdvData.ToArray() },
                {"pbdtRt", pbdtRtData.ToArray() }
            };
           
            return JsonConvert.SerializeObject(bundle);
        }

        private List<object> ParsePbdtIdv(List<object[]> idvs)
        {
            var result = new List<object>();
            var columns = Helpers.PbdtColumns.getIdvColumns();

            for (var i = 0; i < idvs.Count(); i++)
            {
                var idv = idvs[i];
                var pbdtIdvData = new List<object>();

                for (var j = 0; j < idv.Count(); j++)
                {
                    if (j == 0)
                    {
                        pbdtIdvData.Add(Helpers.IdGenerator.GuidToBase64(Guid.NewGuid()));
                        continue;
                    }

                    pbdtIdvData.Add(idv[j]);
                }
                result.Add(pbdtIdvData.ToArray());
            }

            return result;
        }
        
        private List<object> ParsePbdtRt(List<object[]> rts)
        {
            var result = new List<object>();

            for (var i = 0; i < rts.Count(); i++)
            {
                var rt = rts[i];
                var pbdtRtData = new List<object>();

                for (var j = 0; j < rt.Count(); j++)
                {
                    if (j == 0)
                    {
                        pbdtRtData.Add(Helpers.IdGenerator.GuidToBase64(Guid.NewGuid()));
                        continue;
                    }

                    pbdtRtData.Add(rt[j]);
                }
             
               result.Add(pbdtRtData.ToArray());
            }

            return result;
        }
    }
}