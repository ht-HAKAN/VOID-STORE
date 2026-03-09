using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace VOID_STORE
{
    public enum VerificationType
    {
        Registration,
        PasswordReset
    }

    public partial class CodeVerification : Window
    {
        private string _email;
        private VerificationType _verificationType;

        public CodeVerification(string email = "", VerificationType type = VerificationType.Registration)
        {
            // Sayfa açıldığında içerisindeki görsel WPF bileşenlerini hazır hale getir.
            InitializeComponent();
            _email = email;
            // İşlemin Kayıt Olma mı yoksa Şifre Sıfırlama mı olduğunu tut.
            _verificationType = type;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Kod doğrulama penceresini simge durumuna küçült.
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Uygulamayı kapat.
            Application.Current.Shutdown();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ForgotPassword forgotScreen = new ForgotPassword();
            forgotScreen.Left = this.Left;
            forgotScreen.Top = this.Top;
            forgotScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            forgotScreen.Show();
            this.Close();
        }

        private void CodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox currentBox && currentBox.Text.Length == 1)
            {
                if (currentBox == txtCode1) txtCode2.Focus();
                else if (currentBox == txtCode2) txtCode3.Focus();
                else if (currentBox == txtCode3) txtCode4.Focus();
                else if (currentBox == txtCode4) txtCode5.Focus();
                else if (currentBox == txtCode5) txtCode6.Focus();
                // 6 haneli kod kutuları arasında yazı yazınca otomatik diğerine geçme
            }
        }

        private void VerifyCode_Click(object sender, RoutedEventArgs e)
        {
            // Ekranda bulunan 6 ayrı haneli metin kutusundaki (TextBox) verileri birleştirerek tek bir string değişkene ata.
            string enteredCode = txtCode1.Text + txtCode2.Text + txtCode3.Text + txtCode4.Text + txtCode5.Text + txtCode6.Text;

            // KOD UZUNLUK KONTROLÜ: Girilen kodun tam 6 haneli olup olmadığını denetle.
            if (enteredCode.Length < 6)
            {
                CustomError.ShowDialog("Lütfen 6 haneli doğrulama kodunu eksiksiz girin.", "EKSİK KOD");
                return;
            }

            try
            {
                // Veritabanı üzerinden kod doğrulama sorgusu (Mail adresi, kod, daha önce kullanılmamış olması ve süresinin dolmamış olması gerekiyor)
                string checkCodeQuery = "SELECT CodeId FROM VerificationCodes WHERE Email = @Email AND Code = @Code AND IsUsed = 0 AND ExpirationDate > GETDATE()";
                SqlParameter[] checkParams = new SqlParameter[]
                {
                    new SqlParameter("@Email", _email),
                    new SqlParameter("@Code", enteredCode)
                };

                object result = DatabaseManager.ExecuteScalar(checkCodeQuery, checkParams);

                if (result != null)
                {
                    // Doğrulanan kodu artık kullanılmış olarak (IsUsed = 1) işaretle.
                    int codeId = Convert.ToInt32(result);
                    string updateCodeQuery = "UPDATE VerificationCodes SET IsUsed = 1 WHERE CodeId = @CodeId";
                    DatabaseManager.ExecuteNonQuery(updateCodeQuery, new SqlParameter("@CodeId", codeId));

                    // İşlem türü KAYIT ONAYI (Registration) ise çalışacak blok.
                    if (_verificationType == VerificationType.Registration)
                    {
                        // Kullanıcının e-posta onay durumunu (IsEmailVerified) 1 olarak güncelleyen SQL sorgusu
                        string verifyUserQuery = "UPDATE Users SET IsEmailVerified = 1 WHERE Email = @Email";
                        // Sorguyu veritabanına ilet ve kalıcı olarak güncelle.
                        DatabaseManager.ExecuteNonQuery(verifyUserQuery, new SqlParameter("@Email", _email));

                        CustomError.ShowDialog("Hesabınız başarıyla doğrulandı! Şimdi giriş yapabilirsiniz.", "BAŞARILI");

                        // Başarılı doğrulama sonrası kullanıcıyı mevcut Login sayfasına yönlendir.
                        Login loginScreen = new Login();
                        loginScreen.Left = this.Left;
                        loginScreen.Top = this.Top;
                        loginScreen.Show();
                    }
                    else
                    {
                        // Enum kontrolü ile şifre sıfırlama sayfasına geçiş
                        ResetPassword resetScreen = new ResetPassword(); // Geçici değişken aktarımı
                        resetScreen.Left = this.Left;
                        resetScreen.Top = this.Top;
                        resetScreen.Show();
                    }
                    this.Close();
                }
                else
                {
                    CustomError.ShowDialog("Girdiğiniz doğrulama kodu hatalı veya süresi dolmuş.", "GEÇERSİZ KOD");
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Doğrulama sırasında hata oluştu: " + ex.Message, "SİSTEM HATASI");
            }
        }
    }
}
