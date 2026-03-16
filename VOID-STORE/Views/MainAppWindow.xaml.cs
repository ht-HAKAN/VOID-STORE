using System;
using System.Windows;
using System.Windows.Input;

using VOID_STORE.Models;


namespace VOID_STORE.Views
{
    public partial class MainAppWindow : Window
    {
        public MainAppWindow()
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

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Kullanıcıyı login ekranına geri atar
            Login loginWindow = new Login();
            loginWindow.Left = this.Left;
            loginWindow.Top = this.Top;
            loginWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen; 
            loginWindow.Show();
            
            this.Close();
        }
    }
}
