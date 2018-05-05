using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Helpers
{
    public static class IdGenerator
    {
        public static string GuidToBase64(Guid guid)
        {
            return Convert.ToBase64String(guid.ToByteArray()).Replace("/", "-").Replace("+", "_").Replace("=", "");
        }

        public static Guid Base64ToGuid(string base64)
        {
            Guid guid = default(Guid);
            base64 = base64.Replace("-", "/").Replace("_", "+") + "==";

            try
            {
                guid = new Guid(Convert.FromBase64String(base64));
            }
            catch (Exception ex)
            {
                throw new Exception("Bad Base64 conversion to GUID", ex);
            }

            return guid;
        }
    }
}
