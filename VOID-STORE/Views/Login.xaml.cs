using System;
using System.Windows;
using System.Windows.Input;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

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
                CustomError.ShowDialog("Lütfen kullanıcı adı ve şifrenizi girin.", "GİRİŞ HATASI");
                return;
            }

            try
            {
                // kullanici kaydini denetle
                bool isValidUser = _loginController.ValidateUser(usernameOrEmail, password, out bool isEmailVerified, out bool isAdmin);

                if (!isValidUser)
                {
                    CustomError.ShowDialog("Kullanıcı adı veya şifre hatalı.", "GİRİŞ BAŞARISIZ");
                    return;
                }

                // dogrulama durumunu kontrol et
                if (!isEmailVerified)
                {
                    CustomError.ShowDialog("Lütfen e posta adresinize gönderilen doğrulama kodu ile hesabınızı onaylayın.", "DOĞRULANMAMIŞ HESAP");
                    return;
                }

                AuthenticatedUserInfo authenticatedUser = _loginController.GetAuthenticatedUser(usernameOrEmail);
                UserSession.SetAuthenticated(authenticatedUser.UserId, authenticatedUser.Username, authenticatedUser.Balance);

                // yonetici hesabini ayir
                if (isAdmin)
                {
                    AdminRoleSelection adminRoleSelection = new AdminRoleSelection
                    {
                        Left = Left,
                        Top = Top,
                        WindowStartupLocation = WindowStartupLocation.Manual
                    };

                    adminRoleSelection.Show();
                    Close();
                    return;
                }

                // ana pencereyi ac
                MainAppWindow mainWindow = new MainAppWindow();
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Bağlantı sırasında bir hata oluştu: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void AccountRecovery_Click(object sender, RoutedEventArgs e)
        {
            // kurtarma ekranini ac
            AccountRecovery recoveryScreen = new AccountRecovery
            {
                Left = Left,
                Top = Top,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            recoveryScreen.Show();
            Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // kayit ekranini ac
            Register registerScreen = new Register
            {
                Left = Left,
                Top = Top,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            registerScreen.Show();
            Close();
        }

        private void GuestLogin_Click(object sender, RoutedEventArgs e)
        {
            // misafir olarak devam et
            UserSession.SetGuest();
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Show();
            Close();
        }
    }
}
