using System;
using System.Data;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class LoginController
    {
        // girilen bilgilerin veritabaninda eslesip eslesmedigini kontrol et
        public bool ValidateUser(string usernameOrEmail, string password, out bool isEmailVerified, out bool isAdmin)
        {
            isEmailVerified = false;
            isAdmin = false;

            try
            {
                // sifreyi dogrulama icin hashle
                string hashedPassword = SecurityManager.HashPassword(password);

                // kullanici profilini veritabanindan cek
                string loginQuery = "SELECT UserId, IsAdmin, IsEmailVerified FROM Users WHERE (Username = @User OR Email = @User) AND PasswordHash = @Password";
                SqlParameter[] loginParams = new SqlParameter[]
                {
                    new SqlParameter("@User", usernameOrEmail),
                    new SqlParameter("@Password", hashedPassword)
                };

                DataTable dt = DatabaseManager.ExecuteQuery(loginQuery, loginParams);

                if (dt.Rows.Count > 0)
                {
                    isEmailVerified = Convert.ToBoolean(dt.Rows[0]["IsEmailVerified"]);
                    isAdmin = Convert.ToBoolean(dt.Rows[0]["IsAdmin"]);
                    return true;
                }
                
                return false;
            }
            catch (Exception)
            {
                throw; // hatayi view katmaninda yakala
            }
        }
    }
}
