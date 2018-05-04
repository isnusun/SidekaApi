using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OfficeOpenXml;
using SidekaApi.Models;

namespace SidekaApi {
    public class PbdtXlsxToSql {

        SidekaDbContext dbContext;

        public void Run(string rootDirectory, string fileName, string kabupatenCode)
        {
            var idvFile = Path.Combine(rootDirectory, "1. JATENG_IDV", fileName);
            if(!File.Exists(idvFile)){
                throw new Exception(idvFile + "Not exists");
            }
            var rtFile = Path.Combine(rootDirectory, "2. JATENG_RT", fileName);
            if(!File.Exists(rtFile)){
                throw new Exception(rtFile + "Not exists");
            }

            var byDesa = new Dictionary<String, Tuple<List<object[]>, List<object[]>>>();
            var desaTuples = new Dictionary<String, Tuple<String, String>>();

            using(var package = new ExcelPackage(new FileInfo(idvFile)))
            {
                var sheet = package.Workbook.Worksheets.First();
                for (int i = sheet.Cells.Start.Row; i < sheet.Cells.End.Row; i++){
                    var objs = new List<object>();
                    for(int j = sheet.Cells.Start.Column; j < sheet.Cells.End.Column; j++){
                        objs.Add(sheet.Cells[i, j].Value);
                    }
                    var idDesa = objs[0] as String;
                    if(!byDesa.ContainsKey(idDesa)){
                        byDesa[idDesa] = Tuple.Create(new List<object[]>(), new List<object[]>());
                    }
                    byDesa[idDesa].Item1.Add(objs.ToArray());
                    desaTuples[idDesa] = Tuple.Create((String) objs[1], (String) objs[2] );
                }
            }

            using(var package = new ExcelPackage(new FileInfo(rtFile)))
            {
                var sheet = package.Workbook.Worksheets.First();
                for (int i = sheet.Cells.Start.Row; i < sheet.Cells.End.Row; i++){
                    var objs = new List<object>();
                    for(int j = sheet.Cells.Start.Column; j < sheet.Cells.End.Column; j++){
                        objs.Add(sheet.Cells[i, j].Value);
                    }
                    var idDesa = objs[0] as String;
                    if(!byDesa.ContainsKey(idDesa)){
                        byDesa[idDesa] = Tuple.Create(new List<object[]>(), new List<object[]>());
                    }
                    byDesa[idDesa].Item2.Add(objs.ToArray());
                    desaTuples[idDesa] = Tuple.Create((String) objs[1], (String) objs[2]);
                }
            }

            foreach(var idDesa in byDesa.Keys){
                var desaTuple = desaTuples[idDesa];
                var kecamatan = dbContext.Set<SidekaRegion>()
                    .FirstOrDefault(r => r.Name.ToUpper() == desaTuple.Item1.ToUpper() && r.ParentCode == kabupatenCode);
                if(kecamatan == null){
                    Console.WriteLine("Kecamatan "+idDesa+" "+desaTuple.Item1+"not exists");
                    continue;
                }
                var desa = dbContext.Set<SidekaRegion>()
                    .FirstOrDefault(r => r.Name.ToUpper() == desaTuple.Item2.ToUpper() && r.ParentCode == kecamatan.Code);
                if(desa == null){
                    Console.WriteLine("Desa "+idDesa+" "+desaTuple+"not exists");
                    continue;
                }

                var json = ConvertToJson(byDesa[idDesa].Item1, byDesa[idDesa].Item2);
                Pbdt2015 data = new Pbdt2015 
                {
                    RegionCode = desa.Code,
                    SidekaContentJson = json,
                };
                dbContext.Set<Pbdt2015>().Add(data);
                dbContext.SaveChanges();
                Console.WriteLine("Desa "+desaTuple+" finished");
            }
        }

        private String ConvertToJson(List<object[]> idvs, List<object[]> rows){
            return null;
        }
    }
}