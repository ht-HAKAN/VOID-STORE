using System.Windows;
using System.Windows.Input;

namespace VOID_STORE.Views
{
    public partial class AdminRoleSelection : Window
    {
        public AdminRoleSelection()
        {
        // acilis verilerini hazirla
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        // baslik alanindan pencereyi surukle
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
        // pencereyi alt sekmeye al
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // uygulamayi kapat
            Application.Current.Shutdown();
        }

        private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
        {
            // yonetim panelini ac
            AdminDashboard adminDashboard = new AdminDashboard();
            adminDashboard.Show();
            Close();
        }

        private void StoreButton_Click(object sender, RoutedEventArgs e)
        {
            // magazaya kullanici olarak gec
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Show();
            Close();
        }
    }
}
