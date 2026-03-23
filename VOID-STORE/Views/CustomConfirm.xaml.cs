using System.Windows;
using System.Windows.Input;

namespace VOID_STORE.Views
{
    public partial class CustomConfirm : Window
    {
        public CustomConfirm(string title, string message, string confirmText)
        {
            InitializeComponent();

            txtTitle.Text = title.ToUpperInvariant();
            txtMessage.Text = message;
            btnConfirm.Content = confirmText;
        }

        public static bool ShowDialog(string title, string message, string confirmText = "Onayla", Window? owner = null)
        {
            CustomConfirm confirmWindow = new CustomConfirm(title, message, confirmText);

            if (owner != null)
            {
                confirmWindow.Owner = owner;
            }

            return confirmWindow.ShowDialog() == true;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
