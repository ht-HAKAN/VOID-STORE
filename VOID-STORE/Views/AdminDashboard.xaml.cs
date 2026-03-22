using System;
using System.Windows;
using System.Windows.Input;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class AdminDashboard : Window
    {
        public AdminDashboard()
        {
            // ekrani baslat
            InitializeComponent();
            LoadDashboardStats();
        }

        private void LoadDashboardStats()
        {
            // sayilari doldur
            try
            {
                txtTotalGamesValue.Text = GetCount("SELECT COUNT(*) FROM Games").ToString();
                txtActiveGamesValue.Text = GetCount("SELECT COUNT(*) FROM Games WHERE IsActive = 1").ToString();
                txtPassiveGamesValue.Text = GetCount("SELECT COUNT(*) FROM Games WHERE IsActive = 0").ToString();
            }
            catch
            {
                // sayilari sifirla
                txtTotalGamesValue.Text = "0";
                txtActiveGamesValue.Text = "0";
                txtPassiveGamesValue.Text = "0";
            }
        }

        private int GetCount(string query)
        {
            // sayi sorgusu don
            object result = DatabaseManager.ExecuteScalar(query);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // pencereyi surukle
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                return;
            }

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

        private void ToggleWindowStateButton_Click(object sender, RoutedEventArgs e)
        {
            // pencere durumunu degistir
            ToggleWindowState();
        }

        private void StoreButton_Click(object sender, RoutedEventArgs e)
        {
            // magazaya don
            ProfileMenuToggle.IsChecked = false;
            MainAppWindow mainWindow = new MainAppWindow();
            mainWindow.Left = Left;
            mainWindow.Top = Top;
            mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            mainWindow.Show();
            Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // girise don
            ProfileMenuToggle.IsChecked = false;
            Login loginWindow = new Login();
            loginWindow.Left = Left;
            loginWindow.Top = Top;
            loginWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            loginWindow.Show();
            Close();
        }

        private void GamesSectionButton_Click(object sender, RoutedEventArgs e)
        {
            // sonraki adim bilgisi
            CustomError.ShowDialog("Oyun yönetimi ekranı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }

        private void OpenCreateViewButton_Click(object sender, RoutedEventArgs e)
        {
            // sonraki adim bilgisi
            CustomError.ShowDialog("Oyun ekleme ekranı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }

        private void OpenEditViewButton_Click(object sender, RoutedEventArgs e)
        {
            // sonraki adim bilgisi
            CustomError.ShowDialog("Oyun güncelleme ekranı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }

        private void OpenDeleteViewButton_Click(object sender, RoutedEventArgs e)
        {
            // sonraki adim bilgisi
            CustomError.ShowDialog("Silme ve durum yönetimi ekranı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }

        private void ProfileEditButton_Click(object sender, RoutedEventArgs e)
        {
            // profil duzenleme sonraki adim
            ProfileMenuToggle.IsChecked = false;
            CustomError.ShowDialog("Profil düzenleme alanı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }
        private void ToggleWindowState()
        {
            // pencereyi buyut ya da eski haline don
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
