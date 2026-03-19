using System;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class ForgotUsernameController
    {
        // e-posta adresini alip veritabaninda kullaniciyi bulur ve mail atar
        public string SendUsernameReminder(string email)
        {
            // e-posta formati kontrolu
            if (!email.Contains("@") || !email.Contains("."))
            {
                return "Lütfen geçerli bir e-posta adresi girin.";
            }

            // kullaniciyi e-posta adresine gore veritabanindan cek
            string query = "SELECT Username FROM Users WHERE Email = @Email";
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@Email", email)
            };

            object result = DatabaseManager.ExecuteScalar(query, parameters);

            if (result != null)
            {
                string username = result.ToString();
                
                // kullanici bulundugunda hatirlatma maili yolla
                bool isEmailSent = EmailManager.SendUsernameReminderEmail(email, username);

                if (isEmailSent)
                {
                    return string.Empty; // islem basarili
                }
                else
                {
                    return "Kullanıcı adı bulundu ancak hatırlatma maili gönderilemedi. Lütfen daha sonra tekrar deneyin.";
                }
            }

            // e-posta sistemde yoksa
            return "Bu e-posta adresi sistemimizde kayıtlı değildir.";
        }
    }
}
