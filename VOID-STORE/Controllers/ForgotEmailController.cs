using System;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class ForgotEmailController
    {
        // kullanici adina ait e-postayi bulup maskeli bir sekilde dondurur
        public string GetMaskedEmail(string username)
        {
            // kullanici adini veritabaninda ara
            string query = "SELECT Email FROM Users WHERE Username = @Username";
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@Username", username)
            };

            object result = DatabaseManager.ExecuteScalar(query, parameters);

            if (result != null)
            {
                string email = result.ToString();
                return MaskEmail(email);
            }

            // eger kullanici bulunamazsa
            return string.Empty;
        }

        // guvenlik icin e-postayi maskeleme islemi yapar orn hmafu@gmail.com -> hm***@gmail.com
        private string MaskEmail(string email)
        {
            try
            {
                string[] parts = email.Split('@');
                if (parts.Length != 2) return email;

                string namePart = parts[0];
                string domainPart = parts[1];

                if (namePart.Length <= 2)
                {
                    // isim cok kisaysa tamamini yildizla veya ilk harfi goster
                    return namePart.Substring(0, 1) + "***@" + domainPart;
                }
                else
                {
                    // ilk 2 harfi goster kalanini maskele
                    string visiblePart = namePart.Substring(0, 2);
                    return visiblePart + "***@" + domainPart;
                }
            }
            catch
            {
                // bir hata olusursa duz maske don
                return "***@***.***";
            }
        }
    }
}
