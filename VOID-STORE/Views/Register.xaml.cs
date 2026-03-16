using VOID_STORE.Models;
using System;
using System.Windows;
using System.Windows.Input;
using VOID_STORE.Controllers;

namespace VOID_STORE.Views
{
    public partial class Register : Window
    {
        private readonly RegisterController _registerController;

        public Register()
        {
            // form uzerindeki bilesenlerin yuklenmesini baslat
            InitializeComponent();
            _registerController = new RegisterController();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // fareyi sol tus basili tutulduğunda formu surukle
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // pencereyi simge durumuna kucult
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // arka plandaki tum islemleri durdur ve uygulamayi tamamen sonlandir
            Application.Current.Shutdown();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // login ekranina don
            Login loginScreen = new Login();
            loginScreen.Left = this.Left;
            loginScreen.Top = this.Top;
            loginScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            loginScreen.Show();
            this.Close();
        }

        private void RegisterAction_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;
            string confirmPassword = txtConfirmPassword.Password;

            try
            {
                // controllera form bilgilerini gonder dogrulama ve kayit islemini yapmasini iste
                string errorMessage = _registerController.Register(email, username, password, confirmPassword);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // controller hata mesaji dondurduyse ekranda goster
                    CustomError.ShowDialog(errorMessage, "KAYIT HATASI");
                    return;
                }

                // basarili kayit dogrulama ekranina gec
                CodeVerification verifyScreen = new CodeVerification(email);
                verifyScreen.Left = this.Left;
                verifyScreen.Top = this.Top;
                verifyScreen.WindowStartupLocation = WindowStartupLocation.Manual;
                verifyScreen.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Veritabanı hatası: " + ex.Message, "SİSTEM HATASI");
            }
        }
    }
}


