using System;
using System.Security.Cryptography;
using System.Text;

namespace VOID_STORE
{
    public static class SecurityManager
    {
        // Şifreleri güvenli bir şekilde saklamak için SHA256 ile şifreleme metodunu oluştur.
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // Düz metni byte dizisine çevir.
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                
                // Byte dizisini string formatına çevir.
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
