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

            //var hash = Crypter.Phpass.Crypt(login["password"], user.UserPass.Substring(4, 8));
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

            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var sidekaToken = new SidekaToken();
            sidekaToken.UserId = user.ID;
            sidekaToken.Token = token;
            sidekaToken.DesaId = desaId;
            sidekaToken.Info = string.Empty;
            sidekaToken.DateCreated = DateTime.Now;

            await dbContext.SidekaToken.AddAsync(sidekaToken);
            await dbContext.SaveChangesAsync();

            var result = new Dictionary<string, object>()
            {
                { "success", success },
                { "desa_id", desaId },
                { "token", token },
                { "user_id", user.ID },
                { "user_nicename", user.UserNicename },
                { "apiVersion", configuration.GetValue<string>("ApiVersion") }
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

            var result = new Dictionary<string, object>();
            result.Add("user_id", sidekaToken.UserId);
            result.Add("desa_id", sidekaToken.DesaId);
            result.Add("token", sidekaToken.Token);
            // TODO: This one here is a potential bug because not tested for keys > 1
            result.Add("roles", roles.Keys);

            return result;
        }

        private string GetTokenFromHeaders()
        {
            var keyValuePair = Request.Headers.Where(h => h.Key == "X-Auth-Token").FirstOrDefault();
            if (!keyValuePair.Equals(new KeyValuePair<string, StringValues>()))
                return keyValuePair.Value;
            return null;
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
