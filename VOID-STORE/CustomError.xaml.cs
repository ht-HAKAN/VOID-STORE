using System;
using System.Windows;
using System.Windows.Input;

namespace VOID_STORE
{
    public partial class CustomError : Window
    {
        public CustomError(string title, string message)
        {
            InitializeComponent();
            
            // hata başlığını ve mesajını ayarla
            txtTitle.Text = title.ToUpper();
            txtMessage.Text = message;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Mesaj kutusunu kapat
            this.Close();
        }

        // MessageBox.Show gibi kolay kullanım için statik bir metot
        public static void ShowDialog(string message, string title = "HATA")
        {
            CustomError errorWindow = new CustomError(title, message);
            errorWindow.ShowDialog(); // ShowDialog ile ekranı kitler, kapanana kadar arkaya tıklanamaz
        }
    }
}
