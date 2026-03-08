using System;
using System.Windows;
using System.Windows.Input;

namespace VOID_STORE
{
    public partial class ForgotPassword : Window
    {
        public ForgotPassword()
        {
            InitializeComponent();
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
            loginScreen.Show();
            this.Close();
        }

        private void SendCode_Click(object sender, RoutedEventArgs e)
        {
            // E-posta gönderme işlemi eklenecek.
        }
    }
}
