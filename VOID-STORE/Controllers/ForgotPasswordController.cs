using System;
using Microsoft.Data.SqlClient;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class ForgotPasswordController
    {
        // Şifre sıfırlama kodu oluştur ve veritabanına kaydet, ardından e-posta ile gönder.
        public bool SendResetCode(string email, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // Önce bu e-posta adresiyle kayıtlı bir kullanıcı var mı kontrol et
                string checkUserQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                int userExists = Convert.ToInt32(DatabaseManager.ExecuteScalar(checkUserQuery, new SqlParameter("@Email", email)));

                if (userExists == 0)
                {
                    errorMessage = "Sistemde böyle bir e-posta adresi bulunamadı.";
                    return false;
                }

                // Rastgele 6 haneli kod üret
                Random random = new Random();
                string code = random.Next(100000, 999999).ToString();

                // Kodu veritabanına kaydet (Geçerlilik süresi: 10 dakika)
                string insertCodeQuery = @"
                    INSERT INTO VerificationCodes (Email, Code, ExpirationDate, IsUsed)
                    VALUES (@Email, @Code, DATEADD(MINUTE, 10, GETDATE()), 0)";

                SqlParameter[] insertParams = new SqlParameter[]
                {
                    new SqlParameter("@Email", email),
                    new SqlParameter("@Code", code)
                };

                DatabaseManager.ExecuteNonQuery(insertCodeQuery, insertParams);

                // Kodu kullanıcıya e-posta olarak gönder
                bool isEmailSent = EmailManager.SendResetCodeEmail(email, code);

                if (!isEmailSent)
                {
                    errorMessage = "Sistem hatası: E-posta gönderilemedi. Lütfen daha sonra tekrar deneyin.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Bir hata oluştu: " + ex.Message;
                return false;
            }
        }

        // Girilen doğrulama kodunun geçerliliğini kontrol et
        public bool VerifyCode(string email, string enteredCode, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // Girilen kodun doğruluğunu ve süresini kontrol et
                string checkCodeQuery = "SELECT CodeId FROM VerificationCodes WHERE Email = @Email AND Code = @Code AND IsUsed = 0 AND ExpirationDate > GETDATE()";
                SqlParameter[] checkParams = new SqlParameter[]
                {
                    new SqlParameter("@Email", email),
                    new SqlParameter("@Code", enteredCode)
                };

                object result = DatabaseManager.ExecuteScalar(checkCodeQuery, checkParams);

                if (result != null)
                {
                    // Kod doğruysa "kullanıldı" olarak işaretle
                    int codeId = Convert.ToInt32(result);
                    string updateCodeQuery = "UPDATE VerificationCodes SET IsUsed = 1 WHERE CodeId = @CodeId";
                    DatabaseManager.ExecuteNonQuery(updateCodeQuery, new SqlParameter("@CodeId", codeId));

                    return true;
                }
                else
                {
                    errorMessage = "Girdiğiniz doğrulama kodu hatalı veya süresi dolmuş.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Doğrulama sırasında hata oluştu: " + ex.Message;
                return false;
            }
        }

        // Kullanıcının şifresini yeni belirlediği şifre ile değiştir
        public bool ResetUserPassword(string email, string newPassword, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // Yeni şifreyi Hashle (Güvenlik için düz metin olarak kaydetmiyoruz)
                string hashedPassword = SecurityManager.HashPassword(newPassword);

                // Veritabanında kullanıcının şifresini güncelle
                string updatePasswordQuery = "UPDATE Users SET PasswordHash = @PasswordHash WHERE Email = @Email";
                SqlParameter[] updateParams = new SqlParameter[]
                {
                    new SqlParameter("@PasswordHash", hashedPassword),
                    new SqlParameter("@Email", email)
                };

                DatabaseManager.ExecuteNonQuery(updatePasswordQuery, updateParams);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Şifre güncellenirken bir hata oluştu: " + ex.Message;
                return false;
            }
        }
    }
}
