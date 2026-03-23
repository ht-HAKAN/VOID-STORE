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
            // acilis verilerini hazirla
            InitializeComponent();

            try
            {
                AdminGameSchemaManager.EnsureSchema();
            }
            catch
            {
            }

            LoadDashboardStats();
        }

        public void RefreshDashboardStats()
        {
        // ekrandaki sayilari yenile
            LoadDashboardStats();
        }

        private void LoadDashboardStats()
        {
        // kart sayilarini doldur
            try
            {
                txtTotalGamesValue.Text = GetCount("SELECT COUNT(*) FROM Games").ToString();
                txtActiveGamesValue.Text = GetCount("SELECT COUNT(*) FROM Games WHERE ApprovalStatus = 'approved' AND IsActive = 1").ToString();
                txtPassiveGamesValue.Text = GetCount("SELECT COUNT(*) FROM Games WHERE ApprovalStatus = 'approved' AND IsActive = 0").ToString();
                txtPendingGamesValue.Text = (
                    GetCount("SELECT COUNT(*) FROM Games WHERE ApprovalStatus = 'pending'") +
                    GetCount("SELECT COUNT(*) FROM GameDrafts WHERE DraftStatus = 'pending'"))
                    .ToString();
            }
            catch
            {
        // kart sayilarini sifirla
                txtTotalGamesValue.Text = "0";
                txtActiveGamesValue.Text = "0";
                txtPassiveGamesValue.Text = "0";
                txtPendingGamesValue.Text = "0";
            }
        }

        private int GetCount(string query)
        {
        // istenen sayi degerini getir
            object result = DatabaseManager.ExecuteScalar(query);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        // baslik alanindan pencereyi surukle
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
        // pencereyi alt sekmeye al
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
        // magazaya kullanici olarak don
            ProfileMenuToggle.IsChecked = false;
            MainAppWindow mainWindow = new MainAppWindow
            {
                Left = Left,
                Top = Top,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            mainWindow.Show();
            Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
        // oturumu kapatip giris ekranina don
            ProfileMenuToggle.IsChecked = false;
            Login loginWindow = new Login
            {
                Left = Left,
                Top = Top,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            loginWindow.Show();
            Close();
        }

        private void GamesSectionButton_Click(object sender, RoutedEventArgs e)
        {
            // hazir olmayan alani bildir
            CustomError.ShowDialog("Oyun yönetimi ekranı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }

        private void OpenCreateViewButton_Click(object sender, RoutedEventArgs e)
        {
            // oyun ekleme ekranini ac
            try
            {
                AdminCreateGame createWindow = new AdminCreateGame
                {
                    Owner = this,
                    Left = Left,
                    Top = Top,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };

                createWindow.ShowDialog();
                LoadDashboardStats();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Oyun ekleme ekranı açılamadı: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void OpenEditViewButton_Click(object sender, RoutedEventArgs e)
        {
            // oyun guncelleme ekranini ac
            try
            {
                AdminEditGame editWindow = new AdminEditGame
                {
                    Owner = this,
                    Left = Left,
                    Top = Top,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };

                editWindow.ShowDialog();
                LoadDashboardStats();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Oyun güncelleme ekranı açılamadı: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void OpenDeleteViewButton_Click(object sender, RoutedEventArgs e)
        {
            // oyun yonetimi ekranini ac
            try
            {
                AdminManageGames manageWindow = new AdminManageGames
                {
                    Owner = this,
                    Left = Left,
                    Top = Top,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };

                manageWindow.ShowDialog();
                LoadDashboardStats();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Oyun yönetimi ekranı açılamadı: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void ProfileEditButton_Click(object sender, RoutedEventArgs e)
        {
            // profil duzenleme alani hazir degil
            ProfileMenuToggle.IsChecked = false;
            CustomError.ShowDialog("Profil düzenleme alanı bir sonraki adımda hazırlanacak.", "BİLGİ");
        }

        private void ToggleWindowState()
        {
            // pencere boyutunu buyut ya da geri al
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
