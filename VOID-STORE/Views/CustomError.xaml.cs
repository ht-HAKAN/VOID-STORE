using System;
using System.Windows;
using System.Windows.Input;

using VOID_STORE.Models;


namespace VOID_STORE.Views
{
    public partial class CustomError : Window
    {
        public CustomError(string title, string message, bool isSuccess = false)
        {
            InitializeComponent();
            
            // hata başlığını ve mesajını ayarla
            txtTitle.Text = title.ToUpper();
            txtMessage.Text = message;

            // Eğer başarılı bir işlemse, yazıları ve butonları yeşil yap
            if (isSuccess)
            {
                var greenBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00CC00")); // Canlı Yeşil
                txtTitle.Foreground = greenBrush;
                btnOk.Background = greenBrush;
            }
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
        public static void ShowDialog(string message, string title = "HATA", bool isSuccess = false)
        {
            CustomError errorWindow = new CustomError(title, message, isSuccess);
            errorWindow.ShowDialog(); // ShowDialog ile ekranı kitler, kapanana kadar arkaya tıklanamaz
        }
    }
}
