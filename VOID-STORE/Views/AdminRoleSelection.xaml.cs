using System.Windows;
using System.Windows.Input;

namespace VOID_STORE.Views
{
    public partial class AdminRoleSelection : Window
    {
        public AdminRoleSelection()
        {
            // ekrani baslat
            InitializeComponent();
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

        private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
        {
            // panele gec
            AdminDashboard adminDashboard = new AdminDashboard();
            adminDashboard.Show();
            Close();
        }

        private void StoreButton_Click(object sender, RoutedEventArgs e)
        {
            // magazaya gec
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Show();
            Close();
        }
    }
}
