using System;
using System.Data;
using System.Text.RegularExpressions;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class RegisterController
    {
        // kayıt formundan gelen bilgileri doğrula yeni kullanıcı oluştur ve doğrulama kodunu gönder
        public string Register(string email, string username, string password, string confirmPassword)
        {
            // boş alan kontrolü
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                return "Lütfen tüm alanları eksiksiz doldurun.";
            }

            // şifre eşleme kontrolü
            if (password != confirmPassword)
            {
                return "Girdiğiniz şifreler birbiriyle eşleşmiyor.";
            }

            // kullanıcı adı uzunluk kontrolü
            if (username.Length < 3 || username.Length > 16)
            {
                return "Kullanıcı adı en az 3 en fazla 16 karakter uzunluğunda olmalıdır.";
            }

            // kullanıcı adı karakter kontrolü
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            {
                return "Kullanıcı adı sadece harf rakam ve alt çizgi içerebilir.";
            }

            // eposta format kontrolü
            if (!email.Contains("@") || !email.Contains("."))
            {
                return "Lütfen geçerli bir e posta adresi girin.";
            }

            // veritabanında aynı eposta veya kullanıcı adı var mı kontrol et
            string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email OR Username = @Username";
            SqlParameter[] checkParams =
            {
                new SqlParameter("@Email", email),
                new SqlParameter("@Username", username)
            };

            int count = Convert.ToInt32(DatabaseManager.ExecuteScalar(checkQuery, checkParams));
            if (count > 0)
            {
                return "Bu e posta adresi veya kullanıcı adı zaten kullanımda.";
            }

            // şifreyi hashle
            string hashedPassword = SecurityManager.HashPassword(password);

            // kullanıcıya onaysız kayıt at
            string insertUserQuery = "INSERT INTO Users (Username, Email, PasswordHash, IsAdmin, Balance, IsEmailVerified) VALUES (@Username, @Email, @Password, 0, 0.00, 0)";
            SqlParameter[] insertParams =
            {
                new SqlParameter("@Username", username),
                new SqlParameter("@Email", email),
                new SqlParameter("@Password", hashedPassword)
            };
            DatabaseManager.ExecuteNonQuery(insertUserQuery, insertParams);

            // altı haneli doğrulama kodu oluştur
            Random rnd = new();
            string code = rnd.Next(100000, 999999).ToString();

            // kodu veritabanına on dakika süresiyle kaydet
            string insertCodeQuery = "INSERT INTO VerificationCodes (Email, Code, ExpirationDate, IsUsed) VALUES (@Email, @Code, DATE_ADD(NOW(), INTERVAL 10 MINUTE), 0)";
            SqlParameter[] codeParams =
            {
                new SqlParameter("@Email", email),
                new SqlParameter("@Code", code)
            };
            DatabaseManager.ExecuteNonQuery(insertCodeQuery, codeParams);

            // smtp üzerinden doğrulama epostasını gönder
            bool isEmailSent = EmailManager.SendVerificationEmail(email, code);

            if (!isEmailSent)
            {
                return "Mail ayarlarında sorun var kayıt olundu ancak doğrulama e postası gönderilemedi.";
            }

            // başarılı kayıt boş string döndür
            return string.Empty;
        }
    }
}
