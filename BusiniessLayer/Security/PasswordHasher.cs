using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace BusiniessLayer.Security
{


    public static class PasswordHasher
    {
        public static void Create(string password, out string hash, out string salt)
        {
            using var hmac = new HMACSHA256();
            salt = Convert.ToBase64String(hmac.Key);
            hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        public static bool Verify(string password, string hash, string salt)
        {
            using var hmac = new HMACSHA256(Convert.FromBase64String(salt));
            var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return computed == hash;
        }
    }
}
