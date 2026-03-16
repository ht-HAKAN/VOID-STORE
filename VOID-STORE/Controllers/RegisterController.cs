using System;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class RegisterController
    {
        // kayit formundan gelen bilgileri dogrula yeni kullanici olustur ve dogrulama kodunu gonder
        public string Register(string email, string username, string password, string confirmPassword)
        {
            // bos alan kontrolu
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
                return "Lütfen tüm alanları eksiksiz doldurun.";

            // sifre esleme kontrolu
            if (password != confirmPassword)
                return "Girdiğiniz şifreler birbiriyle eşleşmiyor.";

            // kullanici adi uzunluk kontrolu
            if (username.Length < 3 || username.Length > 16)
                return "Kullanıcı adı en az 3, en fazla 16 karakter uzunluğunda olmalıdır.";

            // kullanici adi karakter kontrolu
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
                return "Kullanıcı adı sadece harf, rakam ve alt çizgi (_) içerebilir.";

            // eposta format kontrolu
            if (!email.Contains("@") || !email.Contains("."))
                return "Lütfen geçerli bir e-posta adresi girin.";

            // veritabaninda ayni eposta veya kullanici adi var mi kontrol et
            string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email OR Username = @Username";
            SqlParameter[] checkParams = new SqlParameter[]
            {
                new SqlParameter("@Email", email),
                new SqlParameter("@Username", username)
            };

            int count = Convert.ToInt32(DatabaseManager.ExecuteScalar(checkQuery, checkParams));
            if (count > 0)
                return "Bu e-posta adresi veya kullanıcı adı zaten kullanımda.";

            // sifreyi hashle
            string hashedPassword = SecurityManager.HashPassword(password);

            // kullaniciya onaysiz kayit at (isemailverified = 0)
            string insertUserQuery = "INSERT INTO Users (Username, Email, PasswordHash, IsAdmin, Balance, IsEmailVerified) VALUES (@Username, @Email, @Password, 0, 0.00, 0)";
            SqlParameter[] insertParams = new SqlParameter[]
            {
                new SqlParameter("@Username", username),
                new SqlParameter("@Email", email),
                new SqlParameter("@Password", hashedPassword)
            };
            DatabaseManager.ExecuteNonQuery(insertUserQuery, insertParams);

            // 6 haneli dogrulama kodu olustur
            Random rnd = new Random();
            string code = rnd.Next(100000, 999999).ToString();

            // kodu veritabanina 10 dk suresiyle kaydet
            string insertCodeQuery = "INSERT INTO VerificationCodes (Email, Code, ExpirationDate, IsUsed) VALUES (@Email, @Code, DATEADD(minute, 10, GETDATE()), 0)";
            SqlParameter[] codeParams = new SqlParameter[]
            {
                new SqlParameter("@Email", email),
                new SqlParameter("@Code", code)
            };
            DatabaseManager.ExecuteNonQuery(insertCodeQuery, codeParams);

            // smtp uzerinden dogrulama epostasini gonder
            bool isEmailSent = EmailManager.SendVerificationEmail(email, code);

            if (!isEmailSent)
                return "Mail ayarlarında sorun var, kayıt olundu ancak doğrulama e-postası gönderilemedi.";

            // basarili kayit bos string dondur
            return string.Empty;
        }
    }
}
