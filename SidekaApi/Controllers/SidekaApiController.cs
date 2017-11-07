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

            await Logs((int)auth["user_id"], desaId, "", "save_content", contentType, contentSubtype);
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
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>() { { "message", "Invalid or no token" } });

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

            if (clientChangeId == 0)
                returnData.Add("data", content["data"]);
            else if (sidekaContent.ChangeId == clientChangeId)
                returnData.Add("diffs", new List<object>());
            else
            {
                var diffs = await GetDiffsNewerThanClientAsync(desaId, contentType, contentSubtype,
                    clientChangeId, (JObject)content["columns"]);
                returnData.Add("diffs", diffs);
            }

            await Logs((int)auth["user_id"], desaId, "", "get_content", contentType, contentSubtype);
            return Ok(returnData);
        }

        [HttpPost("content/v2/{desaId}/{contentType}")]
        [HttpPost("content/v2/{desaId}/{contentType}/{contentSubtype}")]
        public async Task<IActionResult> PostContentV2(int desaId, string contentType, string contentSubtype = null)
        {
            var auth = await GetAuth(desaId);
            if (auth == null)
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>() { { "message", "Invalid or no token" } });

            var permission = contentType;
            if (new string[] { "perencanaan", "penganggaran", "spp", "penerimaan" }.Contains(contentType))
                permission = "keuangan";
            var roles = (IEnumerable<string>)auth["roles"];
            if (!roles.Contains("administrator") && !roles.Contains(permission))
                return StatusCode((int)HttpStatusCode.Forbidden, new Dictionary<string, string>() { { "message", "Your account doesn't have the permission" } });

            // Validate


            return null;
        }

        private async Task<Dictionary<string, object>> GetDiffsNewerThanClientAsync(int desaId, string contentType, 
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
                var content = new SidekaContentViewModel(contentJObject);

                if (content.diffs == null)
                    continue;

                foreach (var diff in content.diffs)
                {
                    if (clientColumns[diff.Key] == null)
                        continue;

                    var diffTabColumns = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(content.columns[diff.Key]));
                    var clientTabColumns = clientColumns[diff.Key];
                    foreach (var diffContent in content.diffs[diff.Key])
                    {
                        if (!JToken.DeepEquals(diffTabColumns, clientTabColumns))
                            ((List<object>)result[diff.Key]).Add(diffContent);
                        else
                        {
                            var transformedDiff = new SidekaDiff
                            {
                                added = TransformData(diffTabColumns, clientTabColumns, diffContent.added),
                                modified = TransformData(diffTabColumns, clientTabColumns, diffContent.modified),
                                deleted = TransformData(diffTabColumns, clientTabColumns, diffContent.deleted)
                            };
                            transformedDiff.total = transformedDiff.added.Length + transformedDiff.modified.Length + transformedDiff.deleted.Length;
                            ((List<object>)result[diff.Key]).Add(transformedDiff);
                        }
                    }
                }
            }

            return result;
        }

        [HttpGet("desa")]
        public async Task<IActionResult> GetAllDesa()
        {
            var result = await dbContext.SidekaDesa.ToListAsync();
            return Ok(result);
        }

        private object[] TransformData(JToken fromColumns, JToken toColumns, object[] data)
        {
            if (toColumns is JValue && (string)toColumns == "dict")
                fromColumns = JToken.FromObject("dict");

            if (!JToken.DeepEquals(fromColumns, toColumns))
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
                { "roles", roles.Keys }
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

        private async Task Logs(int userId, int desaId, string token, string action, string contentType, string contentSubtype)
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
                Type = contentType,
                Subtype = contentSubtype,
                Version = version,
                Ip = ip,
                Platform = platform
            };

            dbContext.Add(log);
            await dbContext.SaveChangesAsync();
        }
    }
}
