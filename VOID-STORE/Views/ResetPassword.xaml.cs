using System;
using System.Windows;
using System.Windows.Input;

using VOID_STORE.Controllers;

namespace VOID_STORE.Views
{
    public partial class ResetPassword : Window
    {
        private string _email;
        private ForgotPasswordController _controller;

        public ResetPassword(string email = "")
        {
            InitializeComponent();
            _email = email;
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
            // Kullanıcı bu aşamada geri dönmek isterse Login'e dönsün
            Login loginScreen = new Login();
            loginScreen.Left = this.Left;
            loginScreen.Top = this.Top;
            loginScreen.WindowStartupLocation = WindowStartupLocation.Manual;
            loginScreen.Show();
            this.Close();
        }

        private void SavePassword_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = txtNewPassword.Password;
            string confirmPassword = txtConfirmNewPassword.Password;

            // Alanları kontrol et
            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                CustomError.ShowDialog("Lütfen her iki şifre alanını da doldurun.", "EKSİK BİLGİ");
                return;
            }

            if (newPassword.Length < 6)
            {
                CustomError.ShowDialog("Şifreniz en az 6 karakter olmalıdır.", "KISA ŞİFRE");
                return;
            }

            if (newPassword != confirmPassword)
            {
                CustomError.ShowDialog("Girdiğiniz şifreler birbiriyle eşleşmiyor.", "UYUMSUZ ŞİFRE");
                return;
            }

            // Controller üzerinden şifreyi güncelle
            bool isSuccess = _controller.ResetUserPassword(_email, newPassword, out string errorMessage);

            if (isSuccess)
            {
                CustomError.ShowDialog("Şifreniz başarıyla değiştirildi. Yeni şifrenizle giriş yapabilirsiniz.", "BAŞARILI");
                
                // Başarılı olursa login sayfasına dön
                Login loginScreen = new Login();
                loginScreen.Left = this.Left;
                loginScreen.Top = this.Top;
                loginScreen.WindowStartupLocation = WindowStartupLocation.Manual;
                loginScreen.Show();
                this.Close();
            }
            else
            {
                CustomError.ShowDialog(errorMessage, "HATA");
            }
        }
    }
}
