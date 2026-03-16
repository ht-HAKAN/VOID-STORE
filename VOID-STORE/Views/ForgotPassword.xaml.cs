using System;
using System.Windows;
using System.Windows.Input;

using VOID_STORE.Controllers;

namespace VOID_STORE.Views
{
    public partial class ForgotPassword : Window
    {
        private ForgotPasswordController _controller;

        public ForgotPassword()
        {
            InitializeComponent();
            _controller = new ForgotPasswordController();
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
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Giriş ekranına geri dönüş
            Login loginScreen = new Login();
            loginScreen.Left = this.Left;
            loginScreen.Top = this.Top;
            loginScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            loginScreen.Show();
            this.Close();
        }

        private void SendCode_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();

            if (string.IsNullOrEmpty(email))
            {
                CustomError.ShowDialog("Lütfen kayıtlı e-posta adresinizi girin.", "EKSİK BİLGİ");
                return;
            }

            // Controller üzerinden şifre sıfırlama kodu gönder
            bool isSuccess = _controller.SendResetCode(email, out string errorMessage);

            if (isSuccess)
            {
                // Başarılıysa, CodeVerification ekranına şifre sıfırlama türüyle ve e-postayla geç
                CodeVerification verifyScreen = new CodeVerification(email, VerificationType.PasswordReset);
                verifyScreen.Left = this.Left;
                verifyScreen.Top = this.Top;
                verifyScreen.WindowStartupLocation = WindowStartupLocation.Manual;
                verifyScreen.Show();
                this.Close();
            }
            else
            {
                CustomError.ShowDialog(errorMessage, "HATA");
            }
        }
    }
}
