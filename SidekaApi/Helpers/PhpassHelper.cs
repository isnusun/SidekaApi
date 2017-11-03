using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace SidekaApi.Helpers
{
    public static class PhpassHelper
    {
        public static readonly string itoa64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public static string Crypt(string password, string hash)
        {
            var output = "*0";
            if (hash.StartsWith(output))
                output = "*1";
            if (!hash.StartsWith("$P$") && !hash.StartsWith("$H$"))
                return output;
            var count_log = itoa64.IndexOf(hash[3]);
            if (count_log < 7 || count_log > 30)
                return output;
            var count = 1 << count_log;
            var salt = hash.Substring(4, 8);
            if (salt.Length != 8)
                return output;

            using (var md5 = MD5.Create())
            {
                var hashMd5 = md5.ComputeHash(Encoding.UTF8.GetBytes(salt + password));
                var passwordByteArray = Encoding.UTF8.GetBytes(password);
                while (count > 0)
                {
                    hashMd5 = md5.ComputeHash(hashMd5.Concat(passwordByteArray).ToArray());
                    count -= 1;
                }

                return hash.Substring(0, 12) + Encode64(hashMd5, 16);
            }
        }

        public static string Encode64(byte[] input, int count)
        {
            var output = "";
            var cur = 0;
            while (cur < count)
            {
                var value = (int)input[cur];
                cur += 1;
                output += itoa64[value & 0x3f];
                if (cur < count)
                    value |= ((int)input[cur] << 8);
                output += itoa64[(value >> 6) & 0x3f];
                if (cur >= count)
                    break;
                cur += 1;
                if (cur < count)
                    value |= ((int)input[cur] << 16);
                output += itoa64[(value >> 12) & 0x3f];
                if (cur >= count)
                    break;
                cur += 1;
                output += itoa64[(value >> 18) & 0x3f];
            }
            return output;
        }
    }
}
