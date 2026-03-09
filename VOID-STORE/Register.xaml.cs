using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;

namespace VOID_STORE
{
    public partial class Register : Window
    {
        public Register()
        {
            // Form üzerindeki bileşenlerin (butonlar, textboxlar vb.) yüklenmesini başlat.
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Farenin sol tuşuna basılı tutulduğunda formu ekranda sürüklemeyi sağla.
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove(); // Formun lokasyonunu güncelle.
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Pencerenin durumunu 'Simge Durumuna Küçültülmüş' olarak güncelle.
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Arka plandaki tüm işlemleri durdur ve uygulamayı tamamen sonlandır.
            Application.Current.Shutdown();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Login ekranı nesnesini hafızada oluştur.
            Login loginScreen = new Login();
            
            // Mevcut pencerenin ekrandaki koordinatlarını yeni pencereye aktar. (Sürükleme pozisyonunu koru)
            loginScreen.Left = this.Left;
            loginScreen.Top = this.Top;
            loginScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            
            // Login ekranını kullanıcıya göster.
            loginScreen.Show();
            
            // Bulunulan mevcut kayıt penceresini kapat.
            this.Close();
        }

        private void RegisterAction_Click(object sender, RoutedEventArgs e)
        {
            // TextBox'lardan alınan verilerin başındaki ve sonundaki boşlukları .Trim() ile temizle.
            string email = txtEmail.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;
            string confirmPassword = txtConfirmPassword.Password;

            // 1. Boş alan kontrolü
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                CustomError.ShowDialog("Lütfen tüm alanları eksiksiz doldurun.", "KAYIT HATASI");
                return;
            }

            // 2. Şifre eşleşme kontrolü
            if (password != confirmPassword)
            {
                CustomError.ShowDialog("Girdiğiniz şifreler birbiriyle eşleşmiyor.", "KAYIT HATASI");
                return;
            }

            // 3. Kullanıcı Adı Kısıtlamaları (Sadece harf, rakam ve _, minimum 3 maksimum 16 karakter)
            if (username.Length < 3 || username.Length > 16)
            {
                CustomError.ShowDialog("Kullanıcı adı en az 3, en fazla 16 karakter uzunluğunda olmalıdır.", "KAYIT HATASI");
                return;
            }

            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            {
                CustomError.ShowDialog("Kullanıcı adı sadece harf, rakam ve alt çizgi (_) içerebilir. Özel karakterler ve boşluk kullanılamaz.", "KAYIT HATASI");
                return;
            }

            // 4. E-POSTA ADRESİ DOĞRULAMASI: İçerisinde '@' ve '.' karakterlerinin olup olmadığını kontrol et.
            // Eksikse hata diyalog penceresini çıkar.
            if (!email.Contains("@") || !email.Contains("."))
            {
                CustomError.ShowDialog("Lütfen geçerli bir e-posta adresi girin.", "KAYIT HATASI");
                return;
            }

            // 5. VERİTABANI KONTROL SORGUSU: Girilen e-posta veya kullanıcı adının sistemde mevcut olup olmadığını kontrol et.
            string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email OR Username = @Username";
            
            // SQL sorgusuna gönderilecek parametreleri dizi halinde tanımla.
            SqlParameter[] checkParams = new SqlParameter[]
            {
                new SqlParameter("@Email", email),
                new SqlParameter("@Username", username)
            };

            try
            {
                // Kullanıcı varlık sorgusunu çalıştır ve dönen değeri integer tipine dönüştürerek değişkene ata.
                int count = Convert.ToInt32(DatabaseManager.ExecuteScalar(checkQuery, checkParams));
                if (count > 0)
                {
                    CustomError.ShowDialog("Bu e-posta adresi veya kullanıcı adı zaten kullanımda.", "KAYIT HATASI");
                    return;
                }

                // 6. Şifreyi şifreleme (Hashleme) işlemi
                string hashedPassword = SecurityManager.HashPassword(password);

                // 7. Kullanıcıyı onaysız olarak veritabanına ekle (IsEmailVerified = 0)
                string insertUserQuery = "INSERT INTO Users (Username, Email, PasswordHash, IsAdmin, Balance, IsEmailVerified) VALUES (@Username, @Email, @Password, 0, 0.00, 0)";
                SqlParameter[] insertParams = new SqlParameter[]
                {
                    new SqlParameter("@Username", username),
                    new SqlParameter("@Email", email),
                    new SqlParameter("@Password", hashedPassword)
                };
                DatabaseManager.ExecuteNonQuery(insertUserQuery, insertParams);

                // 8. E-posta için 6 haneli doğrulama kodu oluştur.
                Random rnd = new Random();
                string code = rnd.Next(100000, 999999).ToString();

                // 9. Doğrulama kodunu veritabanına ekle (10 dk süreyle)
                string insertCodeQuery = "INSERT INTO VerificationCodes (Email, Code, ExpirationDate, IsUsed) VALUES (@Email, @Code, DATEADD(minute, 10, GETDATE()), 0)";
                SqlParameter[] codeParams = new SqlParameter[]
                {
                    new SqlParameter("@Email", email),
                    new SqlParameter("@Code", code)
                };
                DatabaseManager.ExecuteNonQuery(insertCodeQuery, codeParams);

                // 10. SMTP üzerinden e-postayı gönderme işlemi
                bool isEmailSent = EmailManager.SendVerificationEmail(email, code);

                if (isEmailSent)
                {
                    // 11. Başarılı ise doğrulama ekranına geçiş yap
                    CodeVerification verifyScreen = new CodeVerification(email); 
                    verifyScreen.Left = this.Left; // Pencere konumunu koru
                    verifyScreen.Top = this.Top;
                    verifyScreen.WindowStartupLocation = WindowStartupLocation.Manual;
                    verifyScreen.Show();
                    this.Close();
                }
                else
                {
                    CustomError.ShowDialog("Mail ayarlarında sorun var, kayıt olundu ancak doğrulama e-postası gönderilemedi.", "E-POSTA HATASI");
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Veritabanı hatası: " + ex.Message, "SİSTEM HATASI");
            }
        }
    }
}
