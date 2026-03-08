using System;
using System.Windows;
using System.Windows.Input;

namespace VOID_STORE
{
    public partial class Register : Window
    {
        public Register()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Ekranı sürükleyebilmek için 
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Uygulamayı alta alma butonu
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Uygulamadan tamamen çıkış butonu
            Application.Current.Shutdown();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Giriş ekranına geri dönüş
            Login loginScreen = new Login();
            loginScreen.Show();
            this.Close();
        }

        private void RegisterAction_Click(object sender, RoutedEventArgs e)
        {
            // Kayıt olma özelliği eklenecek.
        }
    }
}
