using VOID_STORE.Models;
using System;
using System.Windows;
using System.Windows.Input;
using VOID_STORE.Controllers;

namespace VOID_STORE.Views
{
    public partial class Login : Window
    {
        // giris isteklerini yonet
        private readonly LoginController _loginController;

        public Login()
        {
            // formu baslat
            InitializeComponent();
            // giris denetleyicisini hazirla
            _loginController = new LoginController();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // pencereyi surukle
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // pencereyi kucult
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // uygulamayi kapat
            Application.Current.Shutdown();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // giris bilgilerini al
            string usernameOrEmail = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            // bos alanlari kontrol et
            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                // eksik bilgiyi bildir
                CustomError.ShowDialog("Lütfen kullanıcı adı ve şifrenizi girin.", "GİRİŞ HATASI");
                return;
            }

            try
            {
                // kullanici kaydini denetle
                bool isValidUser = _loginController.ValidateUser(usernameOrEmail, password, out bool isEmailVerified, out bool isAdmin);

                if (isValidUser)
                {
                    // dogrulama durumunu kontrol et
                    if (!isEmailVerified)
                    {
                        // dogrulama eksigini bildir
                        CustomError.ShowDialog("Lütfen e-posta adresinize gönderilen doğrulama kodu ile hesabınızı onaylayın.", "DOĞRULANMAMIŞ HESAP");
                        return;
                    }

                    // yonetici hesabini ayir
                    if (isAdmin)
                    {
                        // admin secim ekranina gec
                        AdminRoleSelection adminRoleSelection = new AdminRoleSelection();
                        adminRoleSelection.Left = this.Left;
                        adminRoleSelection.Top = this.Top;
                        adminRoleSelection.WindowStartupLocation = WindowStartupLocation.Manual;
                        adminRoleSelection.Show();
                        this.Close();
                        return;
                    }

                    // ana pencereyi ac
                    MainAppWindow mainWindow = new MainAppWindow();
                    mainWindow.Show();
                    // giris ekranini kapat
                    Close();
                }
                else
                {
                    // hatali girisi bildir
                    CustomError.ShowDialog("Kullanıcı adı veya şifre hatalı.", "GİRİŞ BAŞARISIZ");
                }
            }
            catch (Exception ex)
            {
                // sistem hatasini goster
                CustomError.ShowDialog("Bağlantı sırasında bir hata oluştu: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void AccountRecovery_Click(object sender, RoutedEventArgs e)
        {
            // kurtarma ekranini ac
            AccountRecovery recoveryScreen = new AccountRecovery();

            // mevcut konumu koru
            recoveryScreen.Left = Left;
            recoveryScreen.Top = Top;
            recoveryScreen.WindowStartupLocation = WindowStartupLocation.Manual;

            // yeni ekrana gec
            recoveryScreen.Show();
            Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // kayit ekranini ac
            Register registerScreen = new Register();
            registerScreen.Left = Left;
            registerScreen.Top = Top;
            registerScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            registerScreen.Show();
            Close();
        }

        private void GuestLogin_Click(object sender, RoutedEventArgs e)
        {
            // misafir olarak devam et
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Show();
            Close();
        }
    }
}
