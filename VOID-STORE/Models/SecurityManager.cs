using System;
using System.Security.Cryptography;
using System.Text;

namespace VOID_STORE.Models
{
    public static class SecurityManager
    {
        // sifreleri guvenli bir sekilde saklamak icin SHA256 ile sifreleme metodunu olustur
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // duz metni byte dizisine cevir
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                
                // byte dizisini string formatina cevir
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
