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

            return Ok(result);
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            var token = GetTokenFromHeaders();
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
        
        [HttpPost("content/{desaId}/{contentType}/subtypes")]
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

            // TODO: logs(auth["user_id"], desa_id, "", "get_content", content_type, content_subtype)
            return Ok(content);
        }

        [HttpPost("content/{desaId}/{contentType}")]
        [HttpPost("content/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> PostContent([FromBody]Dictionary<string, object> data, int desaId,
            string contentType, string contentSubtype = null)
        {
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

            // TODO: logs(auth["user_id"], desa_id, "", "save_content", content_type, content_subtype)
            dbContext.Add(sidekaContent);
            await dbContext.SaveChangesAsync();

            return Ok(new Dictionary<string, object>() { { "success", true } });
        }

        [HttpGet("content/v2/{desaId}/{contentType}")]
        [HttpGet("content/v2/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> GetContentV2(int desaId, string contentType, string contentSubtype = null)
        {
            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>());

            var clientChangeId = 0;
            var changeId = QueryStringHelper.GetQueryString<int>(Request.Query, "changeId", 0);
            if (changeId > 0)
                clientChangeId = changeId;

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

            var content = JsonConvert.DeserializeObject<JObject>(sidekaContent.Content).ToDictionary();
            if (sidekaContent.ApiVersion == "1.0")
                content["columns"] = new string[] { "nik", "nama_penduduk", "tempat_lahir", "tanggal_lahir", "jenis_kelamin", "pendidikan", "agama", "status_kawin", "pekerjaan", "pekerjaan_ped", "kewarganegaraan", "kompetensi", "no_telepon", "email", "no_kitas", "no_paspor", "golongan_darah", "status_penduduk", "status_tinggal", "kontrasepsi", "difabilitas", "no_kk", "nama_ayah", "nama_ibu", "hubungan_keluarga", "nama_dusun", "rw", "rt", "alamat_jalan" };

            var returnData = new Dictionary<string, object>()
            {
                { "success", true },
                { "changeId", changeId },
                { "apiVersion", sidekaContent.ApiVersion },
                { "columns", content["columns"] },
                // TODO: remove this later
                { "change_id", changeId },
            };

            if (clientChangeId == 0)
                returnData.Add("data", content["data"]);
            else if (changeId == clientChangeId)
                returnData.Add("diffs", new List<object>());
            else
            {
                var diffs = GetDiffsNewerThanClientAsync(desaId, contentType, contentSubtype, 
                    clientChangeId, (Dictionary<string, object>)content["columns"]);
                returnData.Add("diffs", diffs);
            }

            // TODO: logs(auth["user_id"], desa_id, "", "get_content", content_type, content_subtype)
            return Ok(returnData);
        }

        private async Task GetDiffsNewerThanClientAsync(int desaId, string contentType, string contentSubtype, 
            int clientChangeId, Dictionary<string, object> clientColumns)
        {
            var result = new Dictionary<string, object>();
            foreach(var key in clientColumns.Keys)
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
            foreach(var contentString in contents)
            {
                var content = JsonConvert.DeserializeObject<JObject>(contentString).ToDictionary();
                if (!content.ContainsKey("diffs"))
                    continue;

                var columns = (Dictionary<string, object>)content["columns"];
                var diffs = (Dictionary<string, object>)content["diffs"];
                foreach (var diff in diffs)
                {
                    if (!clientColumns.ContainsKey(diff.Key))
                        continue;
                                       
                    var diffTabColumns = columns[diff.Key];
                    var clientTabColumns = clientColumns[diff.Key];
                    foreach(Dictionary<string, object> diffContent in (IEnumerable<object>)diffs[diff.Key])
                    {
                        if (IsColumnEqual(diffTabColumns, clientTabColumns))
                            ((List<object>)result[diff.Key]).Add(diffContent);
                        else
                        {
                            var transformedDiff = new Dictionary<string, object>();
                            foreach(var type in new string[] { "added", "modified", "deleted" })
                            {
                                transformedDiff.Add(type, TransformData(diffTabColumns, clientTabColumns, (object[])diffContent[type]));
                                ((List<object>)result[diff.Key]).Add(transformedDiff);
                            }
                        }
                    }
                }
            }   
        }

        [HttpGet("desa")]
        public async Task<IActionResult> GetAllDesa()
        {
            var result = await dbContext.SidekaDesa.ToListAsync();
            return Ok(result);
        }

        private object[]TransformData(object fromColumns, object toColumns, object[] data)
        {
            if (toColumns is string && (string)toColumns == "dict")
                fromColumns = "dict";

            if (IsColumnEqual(fromColumns, toColumns))
                return data;

            var fromData = new List<object>();
            foreach(var d in (IEnumerable<object>)data)
            {
                var obj = ArrayToObject((object[])d.Value, fromColumns.Keys.ToList());
                fromData.Add(obj);
            }

            var toData = new List<object>();
            foreach(var d in fromData)
            {
                var arr = ObjectToArray(d, toColumns.Keys.ToList());
                toData.Add(arr);
            }

            return toData;
        }  

        private object ArrayToObject(object[] arr, List<string> columns)
        {
            var result = new Dictionary<string, object>();
            var counter = 0;

            foreach(var column in columns)
            {
                result.Add(column, arr[counter]);
                counter += 1;
            }

            return result;
        }

        private object[] ObjectToArray(object obj, List<string> columns)
        {
            var result = new List<object>();
            foreach(var column in columns)
                result.Add(obj.GetType().GetProperty(column).GetValue(obj, null));
            return result.ToArray();
        }

        private bool IsColumnEqual(object first, object second)
        {
            if (first is string && second is string && first == second)
                return true;
            if (first is Array && second is Array && Enumerable.SequenceEqual((IEnumerable<string>)first, (IEnumerable<string>)second))
                return true;
            return false;
        }   

        private string GetTokenFromHeaders()
        {
            var keyValuePair = Request.Headers.Where(h => h.Key == "X-Auth-Token").FirstOrDefault();
            if (!keyValuePair.Equals(new KeyValuePair<string, StringValues>()))
                return keyValuePair.Value;
            return null;
        }

        private async Task<Dictionary<string, object>> GetAuth(int desaId)
        {
            var token = GetTokenFromHeaders();
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
                { "roles", roles.Keys }
            };

            return result;
        }


        private async Task FillAuth(Dictionary<string, object> auth)
        {
            var desaId = (int)auth["desa_id"];
            var userId = (int)auth["user_id"];
            var desa = await dbContext.SidekaDesa.Where(sd => sd.BlogId == desaId).FirstOrDefaultAsync();
            var user = await dbContext.WordpressUser.Where(wu => wu.ID == userId).FirstOrDefaultAsync();
            auth.Add("desa_name", desa.Desa);
            auth.Add("siteurl", desa.Domain);
            auth.Add("user_display_name", user.DisplayName);
        }
    }
}
