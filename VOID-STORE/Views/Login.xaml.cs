using VOID_STORE.Models;
using System;
using System.Windows;
using System.Windows.Input;
using VOID_STORE.Controllers;

namespace VOID_STORE.Views
{
    public partial class Login : Window
    {
        private readonly LoginController _loginController;

        public Login()
        {
            // login formu yuklendiginde icerisindeki gorsel bilesenleri baslat
            InitializeComponent();
            _loginController = new LoginController();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // pencereyi suruklemek icin
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // login penceresinin ekrandaki durumunu simge durumuna kucult
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // kapatma butonuna bastiginda uygulamayi kapat
            Application.Current.Shutdown();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // kullanici adi veya eposta login icin iki secenek de gecerli
            string usernameOrEmail = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            // bos alan kontrolu bilgilerin eksiksiz girildigini denetle
            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                // bilgiler eksikse ekranda ozel bir hata penceresi goster ve islemi durdur
                CustomError.ShowDialog("Lütfen kullanıcı adı ve şifrenizi girin.", "GİRİŞ HATASI");
                return;
            }

            try
            {
                // controllera bilgileri yolla ve dogrulama sonucunu al
                bool isValidUser = _loginController.ValidateUser(usernameOrEmail, password, out bool isEmailVerified, out bool isAdmin);

                if (isValidUser)
                {
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
                            // admin paneline gecisi buraya ekle
                            CustomError.ShowDialog("Admin paneli henüz yapım aşamasında, mağazaya yönlendiriliyorsunuz.", "BİLGİ");
                        }
                    }

                    // basarili giriste ana uygulama ekrani
                    MainAppWindow mainWindow = new MainAppWindow();
                    // ana ekrani gorunur kil
                    mainWindow.Show();
                    // islemi biten mevcut login ekranini kapat
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
            // sifresini unutan kullanicilar icin sifremi unuttum penceresini olustur
            ForgotPassword forgotScreen = new ForgotPassword();
            
            // pencerenin surukleme sirasinda bulundugu ekrandaki koordinatini yeni pencereye aktar
            forgotScreen.Left = this.Left;
            forgotScreen.Top = this.Top;
            forgotScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            
            // ilgili pencereyi ac ve mevcut login ekranini kapat
            forgotScreen.Show();
            this.Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // kayit numarasina gecis yap
            Register registerScreen = new Register();
            registerScreen.Left = this.Left;
            registerScreen.Top = this.Top;
            registerScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            registerScreen.Show();
            this.Close();
        }

        private void GuestLogin_Click(object sender, RoutedEventArgs e)
        {
            // misafir girisi dogrudan ana sayfaya atar
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Show();
            this.Close(); // mevcut login formunu kapat
        }
    }
}

