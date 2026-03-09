using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using System.Data;

namespace VOID_STORE
{
    public partial class Login : Window
    {
        public Login()
        {
            // Login formu yüklendiğinde içerisindeki görsel bileşenleri başlat.
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Pencereyi sürükleyebilmek için
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Login penceresinin ekrandaki durumunu simge durumuna küçült.
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            //  Kapatma butonuna bastığında uygulamayı kapat
            Application.Current.Shutdown();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Kullanıcı adı veya E-posta (Login için iki seçenek de geçerli)
            string usernameOrEmail = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            // BOŞ ALAN KONTROLÜ: Kullanıcı adı (veya e-posta) ile şifrenin eksiksiz girildiğini denetle.
            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                // Bilgiler eksikse ekranda özel bir hata penceresi (CustomError) göster ve işlemi durdur (return).
                CustomError.ShowDialog("Lütfen kullanıcı adı ve şifrenizi girin.", "GİRİŞ HATASI");
                return;
            }

            try
            {
                // Doğrulama için girilen şifreyi Hashle.
                string hashedPassword = SecurityManager.HashPassword(password);

                // Veritabanında eşleşen kullanıcıyı bulma sorgusu (Username veya Email aynı anda kontrol ediliyor)
                string loginQuery = "SELECT UserId, IsAdmin, IsEmailVerified FROM Users WHERE (Username = @User OR Email = @User) AND PasswordHash = @Password";
                SqlParameter[] loginParams = new SqlParameter[]
                {
                    new SqlParameter("@User", usernameOrEmail),
                    new SqlParameter("@Password", hashedPassword)
                };

                DataTable dt = DatabaseManager.ExecuteQuery(loginQuery, loginParams);

                if (dt.Rows.Count > 0)
                {
                    // Dönen veri tablosunun 0 indeksli ilk satırından belirtilen sütun adlarına göre verileri oku ve değişkene ata.
                    bool isEmailVerified = Convert.ToBoolean(dt.Rows[0]["IsEmailVerified"]);
                    bool isAdmin = Convert.ToBoolean(dt.Rows[0]["IsAdmin"]);

                    if (!isEmailVerified)
                    {
                        CustomError.ShowDialog("Lütfen e-posta adresinize gönderilen kod ile hesabınızı doğrulayın.", "DOĞRULANMAMIŞ HESAP");
                        return;
                    }

                    if (isAdmin)
                    {
                        MessageBoxResult result = MessageBox.Show("ADMİN ALGILANDI.\n\nYönetici paneline gitmek için 'Evet'e, kullanıcı mağazasına gitmek için 'Hayır'a tıklayın.", "Yönetici Girişi", MessageBoxButton.YesNo, MessageBoxImage.Information);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // ADMİN PANELİNE GEÇİŞ EKLENECEK
                            CustomError.ShowDialog("Admin paneli henüz yapım aşamasında, mağazaya yönlendiriliyorsunuz.", "BİLGİ");
                        }
                    }

                    // Başarılı girişte ana uygulama ekranı
                    MainAppWindow mainWindow = new MainAppWindow();
                    // Ana ekranı görünür kıl.
                    mainWindow.Show();
                    // İşlemi biten mevcut Login ekranını kapat.
                    this.Close();
                }
                else
                {
                    CustomError.ShowDialog("Kullanıcı adı veya şifre hatalı.", "GİRİŞ BAŞARISIZ");
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Bağlantı sırasında hata oluştu: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            // Şifresini unutan kullanıcılar için Şifremi Unuttum penceresini oluştur.
            ForgotPassword forgotScreen = new ForgotPassword();
            
            // Pencerenin sürükleme sırasında bulunduğu ekrandaki koordinatını yeni pencereye aktar.
            forgotScreen.Left = this.Left;
            forgotScreen.Top = this.Top;
            forgotScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            
            // İlgili pencereyi aç ve mevcut Login ekranını kapat.
            forgotScreen.Show();
            this.Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // Kayıt numarasına geçiş yap.
            Register registerScreen = new Register();
            registerScreen.Left = this.Left;
            registerScreen.Top = this.Top;
            registerScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            registerScreen.Show();
            this.Close();
        }

        private void GuestLogin_Click(object sender, RoutedEventArgs e)
        {
            // Misafir girişi, doğrudan ana sayfaya atar
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Show();
            this.Close(); // Mevcut login formunu kapat
        }
    }
}