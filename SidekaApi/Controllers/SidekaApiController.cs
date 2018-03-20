using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SidekaApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Extensions.Configuration;
using SidekaApi.Helpers;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SidekaApi.ViewModels;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace SidekaApi.Controllers
{
    [Produces("application/json")]
    public class SidekaApiController : ControllerBase
    {
        private SidekaDbContext dbContext;
        private IConfiguration configuration;

        public SidekaApiController(SidekaDbContext dbContext, IConfiguration configuration)
        {
            this.dbContext = dbContext;
            this.configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Dictionary<string, string> login)
        {
            var user = await dbContext.WordpressUser
                .Where(wpu => wpu.UserLogin == login["user"] || wpu.UserEmail == login["user"])
                .FirstOrDefaultAsync();
            if (user == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>());

            var hash = PhpassHelper.Crypt(login["password"], user.UserPass);
            var success = hash == user.UserPass;
            if (!success)
                return StatusCode(
                    (int)HttpStatusCode.Unauthorized,
                    new Dictionary<string, object> {
                        { "success", false },
                        { "message", "Password is not found" }
                    });

            var primaryBlog = await dbContext.WordpressUserMeta
                .Where(wpum => wpum.UserId == user.ID && wpum.MetaKey == "primary_blog")
                .FirstOrDefaultAsync();
            if (primaryBlog == null)
                return StatusCode(
                    (int)HttpStatusCode.Forbidden,
                    new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "Primary blog is not found" }
                    });

            var desaId = int.Parse(primaryBlog.MetaValue);

            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var sidekaToken = new SidekaToken
            {
                UserId = user.ID,
                Token = token,
                DesaId = desaId,
                Info = string.Empty,
                DateCreated = DateTime.Now
            };

            dbContext.SidekaToken.Add(sidekaToken);
            await dbContext.SaveChangesAsync();

            var result = new Dictionary<string, object>()
            {
                { "success", success },
                { "desa_id", desaId },
                { "token", token },
                { "user_id", user.ID },
                { "user_nicename", user.UserNicename },
                // TODO: Change apiVersion -> api_version. Potential bug?
                { "api_version", configuration.GetValue<string>("ApiVersion") }
            };

            await FillAuth(result);
            return Ok(result);
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            var token = GetValueFromHeaders("X-Auth-Token");
            if (!string.IsNullOrEmpty(token))
            {
                var sidekaToken = await dbContext.SidekaToken
                    .Where(t => t.Token == token)
                    .FirstOrDefaultAsync();
                if (sidekaToken != null)
                {
                    var sidekaTokens = await dbContext.SidekaToken.Where(t => t.UserId == sidekaToken.UserId && t.DesaId == sidekaToken.DesaId).ToListAsync();
                    if (sidekaTokens.Count > 0)
                        dbContext.SidekaToken.RemoveRange(sidekaTokens);
                    await dbContext.SaveChangesAsync();
                }

            }

            var result = new Dictionary<string, object>(){
                { "success", true }
            };

            return Ok(result);
        }

        [HttpGet("check_auth/{desaId}")]
        public async Task<IActionResult> CheckAuth(int desaId)
        {
            var auth = await GetAuth(desaId);
            if (auth == null)
                return Ok(new Dictionary<string, string>());
            await FillAuth(auth);
            return Ok(auth);
        }

        [HttpGet("content/{desaId}/{contentType}/subtypes")]
        public async Task<IActionResult> GetContentSubtype(int desaId, string contentType)
        {
            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>());

            var subTypes = await dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId && sc.Type == contentType)
                .OrderByDescending(sc => sc.Timestamp)
                .Select(sc => sc.Subtype)
                .Distinct()
                .ToListAsync();

            return Ok(subTypes);
        }

        [HttpGet("content/{desaId}/{contentType}")]
        [HttpGet("content/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> GetContent(int desaId, string contentType, string contentSubtype = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>());

            var timestamp = QueryStringHelper.GetQueryString<int>(Request.Query, "timestamp", 0);

            var contentQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Timestamp > timestamp)
                .Where(sc => sc.Type == contentType)
                .Where(sc => sc.ApiVersion == "1.0");

            if (!string.IsNullOrWhiteSpace(contentSubtype))
                contentQuery = contentQuery.Where(sc => sc.Subtype == contentSubtype);

            var content = await contentQuery
                .OrderByDescending(sc => sc.Timestamp)
                .Select(sc => sc.Content)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(content))
                return StatusCode((int)HttpStatusCode.NotFound, new Dictionary<string, string>());

            sw.Stop();
            await Logs((int)auth["user_id"], desaId, "", "get_content", contentType, contentSubtype, sw.Elapsed.Milliseconds);
            return Ok(content);
        }

        [HttpPost("content/{desaId}/{contentType}")]
        [HttpPost("content/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> PostContent([FromBody]Dictionary<string, object> data, int desaId,
            string contentType, string contentSubtype = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, object>() { { "success", false } });

            if (contentType == "subtypes")
                return StatusCode((int)HttpStatusCode.InternalServerError, new Dictionary<string, object>() { { "success", false } });

            var apiVersion = configuration.GetValue<string>("ApiVersion");
            var totalData = await dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType)
                .Where(sc => sc.ApiVersion == apiVersion)
                .CountAsync();

            if (totalData > 0)
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new Dictionary<string, object>() { { "error", "Sideka desktop needs to be updated" } });

            var maxChangeIdQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType);

            if (!string.IsNullOrWhiteSpace(contentSubtype))
                maxChangeIdQuery = maxChangeIdQuery.Where(sc => sc.Subtype == contentSubtype);

            var maxChangeId = await maxChangeIdQuery.Select(sc => sc.ChangeId).DefaultIfEmpty(0).MaxAsync();
            var newChangeId = maxChangeId + 1;
            var timestamp = (long)data.GetValueOrDefault("timestamp", 0);
            var serverTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            if (timestamp > serverTimestamp || timestamp <= 0)
                timestamp = serverTimestamp;

            var sidekaContent = new SidekaContent
            {
                DesaId = desaId,
                Type = contentType,
                Subtype = contentSubtype,
                Content = JsonConvert.SerializeObject(data),
                Timestamp = timestamp,
                DateCreated = DateTime.Now,
                CreatedBy = (int)auth["user_id"],
                ChangeId = newChangeId,
                ApiVersion = "1.0"
            };
                        
            dbContext.Add(sidekaContent);
            await dbContext.SaveChangesAsync();

            sw.Stop();
            await Logs((int)auth["user_id"], desaId, "", "save_content", contentType, contentSubtype, sw.Elapsed.Milliseconds);

            return Ok(new Dictionary<string, object>() { { "success", true } });
        }

        [HttpGet("content/v2/{desaId}/{contentType}")]
        [HttpGet("content/v2/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> GetContentV2(int desaId, string contentType, string contentSubtype = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>() { { "message", "Invalid or no token" } });

            var clientChangeId = 0;

            var changeId = QueryStringHelper.GetQueryString<int>(Request.Query, "changeId", 0);

            if (changeId > 0)
                clientChangeId = changeId;

            var sizeComparison = await GetSizeComparison(desaId, contentType, contentSubtype, clientChangeId);

            var contentQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType)
                .Where(sc => sc.Subtype == contentSubtype)
                .Where(sc => sc.ChangeId >= clientChangeId);

            if (!string.IsNullOrWhiteSpace(contentSubtype))
                contentQuery = contentQuery.Where(sc => sc.Subtype == contentSubtype);

            var sidekaContent = await contentQuery.OrderByDescending(sc => sc.ChangeId).FirstOrDefaultAsync();

            if (sidekaContent == null)
                return StatusCode((int)HttpStatusCode.NotFound, new Dictionary<string, string>());

            var content = JsonConvert.DeserializeObject<JObject>(sidekaContent.Content);

            if (sidekaContent.ApiVersion == "1.0")
                content["columns"] = JArray.FromObject(new string[] { "nik", "nama_penduduk", "tempat_lahir", "tanggal_lahir", "jenis_kelamin", "pendidikan", "agama", "status_kawin", "pekerjaan", "pekerjaan_ped", "kewarganegaraan", "kompetensi", "no_telepon", "email", "no_kitas", "no_paspor", "golongan_darah", "status_penduduk", "status_tinggal", "kontrasepsi", "difabilitas", "no_kk", "nama_ayah", "nama_ibu", "hubungan_keluarga", "nama_dusun", "rw", "rt", "alamat_jalan" });

            var returnData = new Dictionary<string, object>()
            {
                { "success", true },
                { "changeId", sidekaContent.ChangeId },
                { "apiVersion", sidekaContent.ApiVersion },
                { "columns", content["columns"] },
                // TODO: remove this later
                { "change_id", sidekaContent.ChangeId },
            };

            Dictionary<string, object> diffs = null;

            if (sidekaContent.ChangeId == clientChangeId)
            {
                returnData.Add("diffs", new List<object>());
            }
            else if (sizeComparison["contentSize"] > sizeComparison["diffSize"])
            {
                returnData.Add("data", content["data"]);
            }
            else
            {
                diffs = await GetDiffsNewerThanClient(desaId, contentType, contentSubtype,
                        clientChangeId, (JObject)content["columns"]);

                returnData.Add("diffs", diffs);
            }
         
            sw.Stop();
            await Logs((int)auth["user_id"], desaId, "", "get_content", contentType, contentSubtype, sw.Elapsed.Milliseconds);
            return Ok(returnData);
        }

        [HttpGet("update_sizes/v2/{desaId}/{contentType}")]
        [HttpGet("update_sizes/v2/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> UpdateSizes(int desaId, string contentType, string contentSubtype = null)
        {
            var clientChangeId = 0;
            var changeId = QueryStringHelper.GetQueryString<int>(Request.Query, "changeId", 0);

            if (changeId > 0)
                clientChangeId = changeId;

            var contentQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType)
                .Where(sc => sc.Subtype == contentSubtype)
                .Where(sc => sc.ChangeId >= clientChangeId);

            var sidekaContents = await contentQuery.OrderByDescending(sc => sc.ChangeId).ToListAsync();
            var sizes = new List<Dictionary<string, int>>();
            var result = new Dictionary<string, object>()
            {
                { "success", true },
                { "content", sizes }
            };

            foreach (var sidekaContent in sidekaContents)
            {
                var sidekaContentJObject = JsonConvert.DeserializeObject<JObject>(sidekaContent.Content);
                var sizeItem = new Dictionary<string, int>();

                try
                {
                    var content = new SidekaContentViewModel(sidekaContentJObject);
                    var contentSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(content.Data));
                    var diffSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(content.Diffs));

                    sizeItem.Add("contentSize", contentSize);

                    if (content.Diffs == null)
                        sizeItem.Add("diffSize", 0);
                    else
                        sizeItem.Add("diffSize", diffSize);

                    sidekaContent.ContentSize = contentSize;
                    sidekaContent.DiffSize = diffSize;

                    dbContext.Update(sidekaContent);
                    await dbContext.SaveChangesAsync();
                    sizes.Add(sizeItem);
                }

                catch (Exception ex) { }
            }

            return Ok(result);
        }

        [HttpPost("content/v2/{desaId}/{contentType}")]
        [HttpPost("content/v2/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> PostContentV2([FromBody]JObject contentJObject, int desaId, string contentType, string contentSubtype = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>() { { "message", "Invalid or no token" } });

            var permission = contentType;
            if (new string[] { "perencanaan", "penganggaran", "spp", "penerimaan" }.Contains(contentType))
                permission = "keuangan";
            var roles = (List<string>)auth["roles"];
            if (!roles.Contains("administrator") && !roles.Contains(permission))
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>() { { "message", "Your account doesn't have the permission" } });

            var content = new SidekaContentViewModel(contentJObject);

            // Validate
            foreach (var column in content.Columns)
            {
                if (content.Diffs != null && content.Diffs.ContainsKey(column.Key))
                {
                    var index = 0;
                    foreach (var diff in content.Diffs[column.Key])
                    {
                        var location = string.Format("Diff {0} ({1}) tab {2}", index, "added", column.Key);
                        var invalid = Validate(column.Value, diff.Added, location);
                        if (invalid != null)
                            return invalid;

                        location = string.Format("Diff {0} ({1}) tab {2}", index, "modified", column.Key);
                        invalid = Validate(column.Value, diff.Modified, location);
                        if (invalid != null)
                            return invalid;

                        location = string.Format("Diff {0} ({1}) tab {2}", index, "deleted", column.Key);
                        invalid = Validate(column.Value, diff.Deleted, location);
                        if (invalid != null)
                            return invalid;
                    }
                }

                if (content.Data != null && content.Data.ContainsKey(column.Key))
                {
                    var location = string.Format("Data tab {0}", column.Key);
                    var invalid = Validate(column.Value, content.Data[column.Key], location);
                    if (invalid != null)
                        return invalid;
                }
            }

            var clientChangeId = 0;
            var changeId = QueryStringHelper.GetQueryString<int>(Request.Query, "changeId", 0);
            if (changeId > 0)
                clientChangeId = changeId;

            // Find max change id
            var maxChangeIdQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType);

            if (!string.IsNullOrWhiteSpace(contentSubtype))
                maxChangeIdQuery = maxChangeIdQuery.Where(sc => sc.Subtype == contentSubtype);

            var maxChangeId = await maxChangeIdQuery.Select(sc => sc.ChangeId).DefaultIfEmpty(0).MaxAsync();

            // TODO: This is risky!! Consider changing change_id column to serial or autoincrement
            var newChangeId = maxChangeId + 1;

            var newContent = new SidekaContentViewModel();        

            // Initialize new content to be saved
            foreach (var column in content.Columns)
            {
                newContent.Data[column.Key] = new List<object>().ToArray();
                newContent.Columns[column.Key] = column.Value;
                if (content.Diffs != null && content.Diffs.ContainsKey(column.Key))
                    newContent.Diffs[column.Key] = content.Diffs[column.Key];
                else
                    newContent.Diffs[column.Key] = new List<SidekaDiff>().ToArray();
            }

            var latestContentQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType);

            if (!string.IsNullOrWhiteSpace(contentSubtype))
                latestContentQuery = latestContentQuery.Where(sc => sc.Subtype == contentSubtype);

            var latestContentString = await latestContentQuery
                .OrderByDescending(sc => sc.ChangeId)
                .Select(sc => sc.Content)
                .FirstOrDefaultAsync();

            JObject latestContentJObject = null;
            if (string.IsNullOrWhiteSpace(latestContentString))
            {
                latestContentJObject = new JObject
                {
                    { "data", new JObject() },
                    { "columns", contentJObject["columns"] }
                };
            }
            else
            {
                latestContentJObject = JsonConvert.DeserializeObject<JObject>(latestContentString);
            }

            var diffs = await GetDiffsNewerThanClient(desaId, contentType, contentSubtype, clientChangeId, (JObject)contentJObject["columns"]);

            if (latestContentJObject["data"] is JArray && contentType == "penduduk")
                newContent.Data["penduduk"] = MergeDiffs(newContent.Columns["penduduk"], newContent.Diffs["penduduk"], new List<object>().ToArray());
            else
            {
                var latestContent = new SidekaContentViewModel(latestContentJObject);
                foreach(var column in content.Columns)
                {
                    // Initialize so the latest content have the same tab with the posted content
                    if (!latestContent.Columns.ContainsKey(column.Key))
                        latestContent.Columns[column.Key] = column.Value;
                    if (!latestContent.Data.ContainsKey(column.Key))
                        latestContent.Data[column.Key] = new List<object>().ToArray();

                    if (content.Data != null && content.Data[column.Key] != null && 
                        new string[] { "perencanaan", "penganggaran", "penerimaan", "spp" }.Contains(contentType))
                    {
                        // Special case for client who posted data instead of diffs
                        newContent.Data[column.Key] = content.Data[column.Key];

                        // Add new diffs to show that the content is rewritten
                        var sidekaDiff = new SidekaDiff
                        {
                            Added = new List<object>().ToArray(),
                            Modified = new List<object>().ToArray(),
                            Deleted = new List<object>().ToArray(),
                            Total = 0,
                            Rewritten = true
                        };

                        newContent.Diffs[column.Key].Append(sidekaDiff);
                    }
                    else if (newContent.Diffs[column.Key].Length > 0)
                    {
                        // There's diffs in the posted content for this tab, apply them to latest data
                        var latestColumns = latestContent.Columns[column.Key];
                        var transformedLatestData = TransformData(
                            latestContentJObject["columns"][column.Key], 
                            contentJObject["columns"][column.Key], 
                            latestContent.Data[column.Key]);
                        var mergedData = MergeDiffs(column.Value, content.Diffs[column.Key], transformedLatestData);
                        newContent.Data[column.Key] = mergedData;
                        newContent.Columns[column.Key] = column.Value;
                    }
                    else
                    {
                        // There's no diffs in the posted content for this tab, use the old data
                        newContent.Data[column.Key] = latestContent.Data[column.Key];
                    }
                }
            }

            var contentSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(newContent.Data));
            var diffSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(diffs));

            var sidekaContent = new SidekaContent
            {
                DesaId = desaId,
                Type = contentType,
                Subtype = contentSubtype,
                Content = JsonConvert.SerializeObject(newContent),
                DateCreated = DateTime.Now,
                CreatedBy = (int)auth["user_id"],
                ChangeId = newChangeId,
                ApiVersion = configuration.GetValue<string>("ApiVersion"),
                ContentSize = contentSize,
                DiffSize = diffSize
            };

            dbContext.Add(sidekaContent);
            await dbContext.SaveChangesAsync();

            var result = new Dictionary<string, object>()
            {
                { "success", true },
                { "changeId", newChangeId },
                { "change_id", newChangeId },
                { "diffs", diffs },
                { "columns", content.Columns },
            };

            sw.Stop();
            await Logs((int)auth["user_id"], desaId, "", "save_content", contentType, contentSubtype, sw.Elapsed.Milliseconds);

            return Ok(result);
        }

        private async Task<Dictionary<string, object>> GetDiffsNewerThanClient(int desaId, string contentType, 
            string contentSubtype, int clientChangeId, JObject clientColumns)
        {
            var result = new Dictionary<string, object>();
            foreach (var key in clientColumns.Properties().Select(c => c.Name))
                result.Add(key, new List<object>());

            var newerQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType)
                .Where(sc => sc.ChangeId > clientChangeId);

            if (!string.IsNullOrWhiteSpace(contentSubtype))
                newerQuery = newerQuery
                    .Where(sc => sc.Subtype == contentSubtype)
                    .OrderBy(sc => sc.ChangeId);

            var contents = await newerQuery.Select(sc => sc.Content).ToListAsync();

            foreach (var contentString in contents)
            {
                var contentJObject = JsonConvert.DeserializeObject<JObject>(contentString);
                try
                {
                    var content = new SidekaContentViewModel(contentJObject);

                    if (content.Diffs == null)
                        continue;

                    foreach (var diff in content.Diffs)
                    {
                        if (clientColumns[diff.Key] == null)
                            continue;

                        var diffTabColumns = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(content.Columns[diff.Key]));
                        var clientTabColumns = clientColumns[diff.Key];
                        foreach (var diffContent in content.Diffs[diff.Key])
                        {
                            if (JToken.DeepEquals(diffTabColumns, clientTabColumns))
                                ((List<object>)result[diff.Key]).Add(diffContent);
                            else
                            {
                                var transformedDiff = new SidekaDiff
                                {
                                    Added = TransformData(diffTabColumns, clientTabColumns, diffContent.Added),
                                    Modified = TransformData(diffTabColumns, clientTabColumns, diffContent.Modified),
                                    Deleted = TransformData(diffTabColumns, clientTabColumns, diffContent.Deleted)
                                };
                                transformedDiff.Total = transformedDiff.Added.Length + transformedDiff.Modified.Length + transformedDiff.Deleted.Length;
                                ((List<object>)result[diff.Key]).Add(transformedDiff);
                            }
                        }
                    }
                }
                catch(Exception ex) { }
            }

            return result;
        }

        private async Task<Dictionary<string, int>> GetSizeComparison(int desaId, string contentType, string contentSubtype, 
            int clientChangeId)
        {
            var contentQuery = dbContext.SidekaContent
                .Where(sc => sc.DesaId == desaId)
                .Where(sc => sc.Type == contentType)
                .Where(sc => sc.ChangeId > clientChangeId);

            var totalDiffSizeQuery = await contentQuery.SumAsync(sc => sc.DiffSize);

            var contentSizeQuery = await contentQuery.OrderByDescending(sc => sc.ChangeId)
                    .Select(e => e.ContentSize)
                    .FirstOrDefaultAsync();

            return new Dictionary<string, int>()
            {
                {"contentSize", contentSizeQuery != null ? contentSizeQuery.Value : 0},
                {"diffSize", totalDiffSizeQuery != null ? totalDiffSizeQuery.Value : 0}
            };
        }

        [HttpGet("desa")]
        public async Task<IActionResult> GetAllDesa()
        {
            var result = await dbContext.SidekaDesa.ToListAsync();
            return Ok(result);
        }

        private Dictionary<string, int> CompareSizes(JToken content, Dictionary<string, object> diffs)
        {
            var contentSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(content));
            var diffSize = ASCIIEncoding.Unicode.GetByteCount(JsonConvert.SerializeObject(diffs));

            return new Dictionary<string, int>
            {
                { "contentSize", contentSize },
                { "diffSize", contentSize }
            };
        }

        private object[] TransformData(JToken fromColumns, JToken toColumns, object[] data)
        {
            if (toColumns is JValue && (string)toColumns == "dict")
                fromColumns = JToken.FromObject("dict");

            if (JToken.DeepEquals(fromColumns, toColumns))
                return data;

            var fromData = new List<object>();
            foreach (var d in data)
            {
                var obj = ArrayToObject((object[])d, fromColumns.ToList());
                fromData.Add(obj);
            }

            var toData = new List<object>();
            foreach (var d in fromData)
            {
                var arr = ObjectToArray((Dictionary<string, object>)d, toColumns.ToList());
                toData.Add(arr);
            }

            return toData.ToArray();
        }

        private Dictionary<string, object> ArrayToObject(object[] arr, List<JToken> columns)
        {
            var result = new Dictionary<string, object>();
            var counter = 0;

            foreach (string column in columns)
            {
                result.Add(column, arr[counter]);
                counter += 1;
            }

            return result;
        }

        private object[] ObjectToArray(Dictionary<string, object> obj, List<JToken> columns)
        {
            var result = new List<object>();
            foreach (string column in columns)
                result.Add(obj[column]);
            return result.ToArray();
        }

        private object[] MergeDiffs(SidekaColumnConfig columns, SidekaDiff[] diffs, object[] dataArray)
        {
            var data = dataArray.ToList();


            foreach(var diff in diffs)
            {
                foreach(var added in diff.Added)
                {
                    data.Add(added);
                }

                foreach(var modified in diff.Modified)
                {
                    for (var i = 0; i <= data.Count - 1; i++)
                    {
                        if (columns.IsDict)
                        {
                            var datumId = (string)((Dictionary<string, object>)data[i])["id"];
                            var modifiedId = (string)((Dictionary<string, object>)modified)["id"];
                            if (datumId == modifiedId)
                                data[i] = modified;
                        }
                        else
                        {
                            if (((object[])data[i])[0] == ((object[])modified)[0])
                                data[i] = modified;
                        }
                    }
                }

                foreach(var deleted in diff.Deleted)
                {
                    for(var i = data.Count - 1; i >= 0; i--)
                    {
                        if (columns.IsDict)
                        {
                            var datumId = (string)((Dictionary<string, object>)data[i])["id"];
                            var deletedId = (string)((Dictionary<string, object>)deleted)["id"];
                            if (datumId == deletedId)
                                data.RemoveAt(i);
                        }
                        else
                        {
                            if (((object[])data[i])[0] == ((object[])deleted)[0])
                                data.RemoveAt(i);
                        }
                    }
                }
            }

            return data.ToArray();
        }
        
        private IActionResult Validate(SidekaColumnConfig columns, object[] diffTypes, string location)
        {
            var index = 0;
            foreach(var diffType in diffTypes)
            {
                Type t = diffType.GetType();
                bool isDiffTypeDict = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);                
                if (columns.IsDict)
                {
                    if (!isDiffTypeDict)
                        return StatusCode((int)HttpStatusCode.BadRequest,
                            new Dictionary<string, string>() { { "message", string.Format("Invalid item, dict expected, at row {0} at {1}", index, location) } });
                }
                else
                {
                    if (!t.IsArray)
                        return StatusCode((int)HttpStatusCode.BadRequest,
                            new Dictionary<string, string>() { { "message", string.Format("Invalid item, array expected, at row {0} at {1}", index, location) } });
                    else if (columns.Columns.Length != ((object[])diffType).Length)
                        return StatusCode((int)HttpStatusCode.BadRequest,
                           new Dictionary<string, string>() { { "message",
                                   string.Format("Invalid item, expecting array of length {0} instead of {1}, at row {2} at {3}", 
                                   columns.Columns.Length, ((object[])diffType).Length, index, location) } });
                }
                index++;
            }
            return null;
        }

        private string GetValueFromHeaders(string key)
        {
            var keyValuePair = Request.Headers.Where(h => h.Key == key).FirstOrDefault();
            if (!keyValuePair.Equals(new KeyValuePair<string, StringValues>()))
                return keyValuePair.Value;
            return null;
        }

        private async Task<Dictionary<string, object>> GetAuth(int desaId)
        {
            var token = GetValueFromHeaders("X-Auth-Token");
            if (token == null)
                return null;

            var sidekaToken = await dbContext.SidekaToken
                .Where(t => t.Token == token && t.DesaId == desaId)
                .FirstOrDefaultAsync();
            if (sidekaToken == null)
                return null;

            var capabilities = string.Format("wp_{0}_capabilities", sidekaToken.DesaId);
            var userMeta = await dbContext.WordpressUserMeta
                .Where(wum => wum.UserId == sidekaToken.UserId && wum.MetaKey == capabilities)
                .FirstOrDefaultAsync();
            var roles = (Hashtable)new PhpSerializer().Deserialize(userMeta.MetaValue);

            var result = new Dictionary<string, object>
            {
                { "user_id", sidekaToken.UserId },
                { "desa_id", sidekaToken.DesaId },
                { "token", sidekaToken.Token },
                // TODO: This one here is a potential bug because not tested for keys > 1
                { "roles", roles.Keys.Cast<string>().ToList() }
            };

            return result;
        }

        private async Task FillAuth(Dictionary<string, object> auth)
        {
            var desaId = (int)auth["desa_id"];

            var desaNameQuery = string.Format("SELECT * FROM wp_{0}_options WHERE option_name = 'blogname'", desaId);
            var desaName = await dbContext.WordpressOption.FromSql(desaNameQuery).FirstOrDefaultAsync();

            var siteUrlQuery = string.Format("SELECT * FROM wp_{0}_options WHERE option_name = 'siteurl'", desaId);
            var siteUrl = await dbContext.WordpressOption.FromSql(siteUrlQuery).FirstOrDefaultAsync();

            var userId = (int)auth["user_id"];
            var user = await dbContext.WordpressUser.Where(wu => wu.ID == userId).FirstOrDefaultAsync();

            auth.Add("desa_name", desaName.OptionValue);
            auth.Add("siteurl", siteUrl.OptionValue);
            auth.Add("user_display_name", user.DisplayName);
        }

        private async Task Logs(int userId, int desaId, string token, string action, string contentType, string contentSubtype, int totalTime)
        {
            if (string.IsNullOrWhiteSpace(token))
                token = GetValueFromHeaders("X-Auth-Token");

            var version = GetValueFromHeaders("X-Sideka-Version");
            var ip = HttpContext.Connection.RemoteIpAddress.ToString();
            var platform = GetValueFromHeaders("X-Platform") ?? GetValueFromHeaders("X-Platfrom");

            var log = new SidekaLog
            {
                UserId = userId,
                DesaId = desaId,
                DateAccessed = DateTime.Now,
                Token = token,
                Action = action,
                Type = contentType,
                Subtype = contentSubtype,
                TotalTime = totalTime,
                Version = version,
                Ip = ip,
                Platform = platform
            };

            dbContext.Add(log);
            await dbContext.SaveChangesAsync();
        }
    }
}
