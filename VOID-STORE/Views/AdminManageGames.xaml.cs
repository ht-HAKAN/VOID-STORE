using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class AdminManageGames : Window
    {
        private enum ManageSection
        {
            Pending,
            Listed,
            Unlisted
        }

        private readonly AdminGameController _adminGameController;
        private List<AdminGameListItem> _items = new();
        private ManageSection _currentSection;
        private AdminGameListItem? _selectedItem;

        public AdminManageGames()
        {
            InitializeComponent();
            _adminGameController = new AdminGameController();

            try
            {
                _adminGameController.EnsureSchema();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Oyun yönetimi hazırlanamadı: " + ex.Message, "SİSTEM HATASI");
                Loaded += (_, _) => Close();
                return;
            }

            ApplySection(ManageSection.Pending);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        // basliktan pencereyi tasi
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

        private void ToggleWindowStateButton_Click(object sender, RoutedEventArgs e)
        {
        // pencere boyutunu degistir
            ToggleWindowState();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
        // pencereyi kapat
            Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
        // yonetim paneline geri don
            Close();
        }

        private void PendingTabButton_Click(object sender, RoutedEventArgs e)
        {
        // onay bekleyen kayitlari ac
            ApplySection(ManageSection.Pending);
        }

        private void ListedTabButton_Click(object sender, RoutedEventArgs e)
        {
        // listelenen oyunlari ac
            ApplySection(ManageSection.Listed);
        }

        private void UnlistedTabButton_Click(object sender, RoutedEventArgs e)
        {
        // liste disi oyunlari ac
            ApplySection(ManageSection.Unlisted);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        // listeyi arama metnine gore daralt
            LoadItems(_selectedItem?.GameId ?? 0);
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        // secilen kaydin detayini yukle
            if (lstGames.SelectedItem is not AdminGameListItem selectedItem)
            {
                _selectedItem = null;
                ShowPlaceholder("Soldan bir kayıt seçin.");
                return;
            }

            try
            {
                _selectedItem = selectedItem;
                GameManageDetail detail = _adminGameController.GetManagementDetail(selectedItem);
                ApplyDetail(detail);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Kayıt bilgileri yüklenemedi: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
        {
        // secili kaydin ana islemini baslat
            if (_selectedItem == null)
            {
                return;
            }

            try
            {
                if (_currentSection == ManageSection.Pending)
                {
                    HandlePendingPrimaryAction();
                }
                else if (_currentSection == ManageSection.Listed)
                {
                    if (!CustomConfirm.ShowDialog("LİSTE DIŞI", "Seçili oyunu mağaza listesinden kaldırmak istiyor musunuz?", "Liste Dışına Al", this))
                    {
                        return;
                    }

                    _adminGameController.SetGameListedState(_selectedItem.GameId, false);
                    CustomError.ShowDialog("Oyun liste dışına alındı.", "BAŞARILI", true);
                }
                else
                {
                    if (!CustomConfirm.ShowDialog("YENİDEN LİSTELE", "Seçili oyunu yeniden mağazada göstermek istiyor musunuz?", "Yeniden Listele", this))
                    {
                        return;
                    }

                    _adminGameController.SetGameListedState(_selectedItem.GameId, true);
                    CustomError.ShowDialog("Oyun yeniden listelendi.", "BAŞARILI", true);
                }

                RefreshOwnerStats();
                LoadItems();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("İşlem tamamlanamadı: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void SecondaryActionButton_Click(object sender, RoutedEventArgs e)
        {
        // secili kaydin yardimci islemini baslat
            if (_selectedItem == null)
            {
                return;
            }

            try
            {
                if (_currentSection == ManageSection.Pending)
                {
                    if (_selectedItem.IsPendingNewGame)
                    {
                        if (!CustomConfirm.ShowDialog("KAYDI REDDET", "Bu yeni oyun kaydını kaldırmak istiyor musunuz?", "Reddet", this))
                        {
                            return;
                        }

                        _adminGameController.RejectPendingNewGame(_selectedItem.GameId);
                        CustomError.ShowDialog("Oyun kaydı kaldırıldı.", "BAŞARILI", true);
                    }
                    else
                    {
                        if (!CustomConfirm.ShowDialog("DEĞİŞİKLİĞİ REDDET", "Gönderilen değişiklikleri kaldırmak istiyor musunuz?", "Reddet", this))
                        {
                            return;
                        }

                        _adminGameController.RejectPendingDraft(_selectedItem.GameId);
                        CustomError.ShowDialog("Gönderilen değişiklikler kaldırıldı.", "BAŞARILI", true);
                    }
                }
                else
                {
                    if (!CustomConfirm.ShowDialog("KALICI SİL", "Seçili oyunu veritabanından ve görsellerden tamamen kaldırmak istiyor musunuz?", "Kalıcı Sil", this))
                    {
                        return;
                    }

                    _adminGameController.DeleteGamePermanently(_selectedItem.GameId);
                    CustomError.ShowDialog("Oyun kalıcı olarak silindi.", "BAŞARILI", true);
                }

                RefreshOwnerStats();
                LoadItems();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("İşlem tamamlanamadı: " + ex.Message, "SİSTEM HATASI");
            }
        }

        private void ApplySection(ManageSection section)
        {
        // secili bolumu degistir
            _currentSection = section;
            _selectedItem = null;
            txtSearch.Text = string.Empty;

            ApplyTabButtonStyles();

            if (section == ManageSection.Pending)
            {
                txtHeaderTitle.Text = "Oyun Yönetimi";
                txtHeaderHint.Text = "Onay bekleyen yeni kayıtları ve değişiklikleri buradan yönetin.";
                txtListTitle.Text = "Onay Bekleyen";
                txtListHint.Text = "Yeni kayıtlar ve gönderilen değişiklikler burada görüntülenir.";
                txtPlaceholderHint.Text = "Soldan onay bekleyen bir kayıt seçin.";
            }
            else if (section == ManageSection.Listed)
            {
                txtHeaderTitle.Text = "Listelenmiş Oyunlar";
                txtHeaderHint.Text = "Mağazada görünen oyunların yayın durumunu buradan yönetin.";
                txtListTitle.Text = "Yayındaki Oyunlar";
                txtListHint.Text = "Bu listede mağazada görünen oyunlar yer alır.";
                txtPlaceholderHint.Text = "Soldan listelenmiş bir oyun seçin.";
            }
            else
            {
                txtHeaderTitle.Text = "Liste Dışı Oyunlar";
                txtHeaderHint.Text = "Geçici olarak gizlenen oyunları yeniden yayına alabilirsiniz.";
                txtListTitle.Text = "Liste Dışı Oyunlar";
                txtListHint.Text = "Bu listede mağazada gizlenen oyunlar yer alır.";
                txtPlaceholderHint.Text = "Soldan liste dışı bir oyun seçin.";
            }

            LoadItems();
        }

        private void ApplyTabButtonStyles()
        {
        // sekme gorunumlerini yenile
            ApplyTabButtonStyle(btnPendingTab, _currentSection == ManageSection.Pending);
            ApplyTabButtonStyle(btnListedTab, _currentSection == ManageSection.Listed);
            ApplyTabButtonStyle(btnUnlistedTab, _currentSection == ManageSection.Unlisted);
        }

        private void ApplyTabButtonStyle(Button button, bool isActive)
        {
        // secili sekme gorunumunu uygula
            button.Background = CreateBrush(isActive ? "#FFFFFF" : "#111114");
            button.BorderBrush = CreateBrush(isActive ? "#FFFFFF" : "#1C1C22");
            button.Foreground = CreateBrush(isActive ? "#09090B" : "#FFFFFF");
        }

        private void LoadItems(int preserveGameId = 0)
        {
        // secili bolumun listesini doldur
            _items = _currentSection switch
            {
                ManageSection.Pending => _adminGameController.GetPendingReviewGames(txtSearch.Text.Trim()).ToList(),
                ManageSection.Listed => _adminGameController.GetListedGames(txtSearch.Text.Trim()).ToList(),
                _ => _adminGameController.GetUnlistedGames(txtSearch.Text.Trim()).ToList()
            };

            lstGames.ItemsSource = null;
            lstGames.ItemsSource = _items;

            AdminGameListItem? selectedItem = null;

            if (preserveGameId > 0)
            {
                selectedItem = _items.FirstOrDefault(item => item.GameId == preserveGameId);
            }

            if (selectedItem == null && _items.Count > 0)
            {
                selectedItem = _items[0];
            }

            lstGames.SelectedItem = selectedItem;

            if (_items.Count == 0)
            {
                ShowPlaceholder("Bu bölümde görüntülenecek kayıt bulunmuyor.");
            }
            else if (selectedItem == null)
            {
                ShowPlaceholder("Soldan bir kayıt seçin.");
            }
        }

        private void HandlePendingPrimaryAction()
        {
        // onay bekleyen kayit icin islemi belirle
            if (_selectedItem == null)
            {
                return;
            }

            if (_selectedItem.IsPendingNewGame)
            {
                if (!CustomConfirm.ShowDialog("OYUNU ONAYLA", "Bu oyunu mağazada listelenmeye hazır hale getirmek istiyor musunuz?", "Onayla", this))
                {
                    return;
                }

                _adminGameController.ApprovePendingNewGame(_selectedItem.GameId);
                CustomError.ShowDialog("Oyun onaylandı ve mağazada listelenmeye hazır.", "BAŞARILI", true);
                return;
            }

            if (!CustomConfirm.ShowDialog("DEĞİŞİKLİĞİ ONAYLA", "Gönderilen değişiklikleri yayındaki sürüme uygulamak istiyor musunuz?", "Onayla", this))
            {
                return;
            }

            _adminGameController.ApprovePendingDraft(_selectedItem.GameId);
            CustomError.ShowDialog("Değişiklikler onaylandı ve yayındaki sürüm güncellendi.", "BAŞARILI", true);
        }

        private void ApplyDetail(GameManageDetail detail)
        {
        // sag alandaki detaylari doldur
            DetailPlaceholder.Visibility = Visibility.Collapsed;
            DetailScroll.Visibility = Visibility.Visible;

            if (detail.LiveState != null)
            {
                txtDetailTitle.Text = "Değişiklik Karşılaştırması";
                txtDetailHint.Text = "Solda yayındaki sürüm sağda onay bekleyen değişiklikler yer alır.";
                SingleStateCard.Visibility = Visibility.Collapsed;
                CompareGrid.Visibility = Visibility.Visible;

                FillStateCard(
                    detail.LiveState,
                    imgLiveCover,
                    txtLiveSummary,
                    txtLiveDescription,
                    wpLivePlatforms,
                    lstLiveGallery);

                FillStateCard(
                    detail.CurrentState,
                    imgIncomingCover,
                    txtIncomingSummary,
                    txtIncomingDescription,
                    wpIncomingPlatforms,
                    lstIncomingGallery);

                btnPrimaryAction.Content = "Değişiklikleri Onayla";
                btnPrimaryAction.Visibility = Visibility.Visible;
                btnSecondaryAction.Content = "Değişikliği Reddet";
                btnSecondaryAction.Visibility = Visibility.Visible;
                return;
            }

            CompareGrid.Visibility = Visibility.Collapsed;
            SingleStateCard.Visibility = Visibility.Visible;

            if (detail.IsPendingNewGame)
            {
                txtDetailTitle.Text = "Onay Bekleyen Oyun";
                txtDetailHint.Text = "Yeni oyun kaydını inceleyip mağaza için onaylayabilirsiniz.";
                btnPrimaryAction.Content = "Oyunu Onayla";
                btnSecondaryAction.Content = "Kaydı Reddet";
                btnPrimaryAction.Visibility = Visibility.Visible;
                btnSecondaryAction.Visibility = Visibility.Visible;
            }
            else if (_currentSection == ManageSection.Listed)
            {
                txtDetailTitle.Text = "Listelenmiş Oyun";
                txtDetailHint.Text = "Bu oyun şu anda mağazada görünür durumda.";
                btnPrimaryAction.Content = "Liste Dışına Al";
                btnSecondaryAction.Content = "Kalıcı Sil";
                btnPrimaryAction.Visibility = Visibility.Visible;
                btnSecondaryAction.Visibility = Visibility.Visible;
            }
            else
            {
                txtDetailTitle.Text = "Liste Dışı Oyun";
                txtDetailHint.Text = "Bu oyun şu anda mağazada gizli durumda.";
                btnPrimaryAction.Content = "Yeniden Listele";
                btnSecondaryAction.Content = "Kalıcı Sil";
                btnPrimaryAction.Visibility = Visibility.Visible;
                btnSecondaryAction.Visibility = Visibility.Visible;
            }

            FillStateCard(
                detail.CurrentState,
                imgSingleCover,
                txtSingleSummary,
                txtSingleDescription,
                wpSinglePlatforms,
                lstSingleGallery);
        }

        private void FillStateCard(GameEditState state, Image image, TextBlock summary, TextBlock description, WrapPanel platformsPanel, ListBox galleryList)
        {
        // secilen surum kartini doldur
            image.Source = GameAssetManager.LoadBitmap(state.CoverImageSourcePath);
            summary.Text = BuildSummary(state);
            description.Text = string.IsNullOrWhiteSpace(state.Description) ? "Açıklama girilmemiş." : state.Description;
            galleryList.ItemsSource = state.GalleryImageSourcePaths.Select(Path.GetFileName).ToList();
            SetPlatformBadges(platformsPanel, state.Platforms);
        }

        private string BuildSummary(GameEditState state)
        {
        // kisa oyun bilgisi metnini hazirla
            string categoryText = string.IsNullOrWhiteSpace(state.Category) ? "-" : state.Category;
            string priceText = string.IsNullOrWhiteSpace(state.PriceText) ? "-" : $"{state.PriceText} ₺";
            string developerText = string.IsNullOrWhiteSpace(state.Developer) ? "-" : state.Developer;
            string publisherText = string.IsNullOrWhiteSpace(state.Publisher) ? "-" : state.Publisher;
            string releaseText = string.IsNullOrWhiteSpace(state.ReleaseDateText) ? "-" : state.ReleaseDateText;
            string trailerText = string.IsNullOrWhiteSpace(state.TrailerVideoPath)
                ? "Fragman eklenmedi"
                : Path.GetFileName(state.TrailerVideoPath);
            string minimumText = string.IsNullOrWhiteSpace(state.MinimumRequirements) ? "Belirtilmedi" : state.MinimumRequirements.Trim();
            string recommendedText = string.IsNullOrWhiteSpace(state.RecommendedRequirements) ? "Belirtilmedi" : state.RecommendedRequirements.Trim();
            string languagesText = string.IsNullOrWhiteSpace(state.SupportedLanguages) ? "Belirtilmedi" : state.SupportedLanguages.Trim();
            string featuresText = state.Features.Count == 0 ? "Belirtilmedi" : string.Join(", ", state.Features);

            return $"Genel Bilgiler\nOyun Adı  {state.Title}\nKategori  {categoryText}\nYapımcı  {developerText}\nYayıncı  {publisherText}\nFiyat  {priceText}\nÇıkış Tarihi  {releaseText}\nFragman  {trailerText}\n\nOyun Özellikleri\n{featuresText}\n\nMinimum Sistem Gereksinimleri\n{minimumText}\n\nÖnerilen Sistem Gereksinimleri\n{recommendedText}\n\nDesteklenen Diller\n{languagesText}";
        }

        private void SetPlatformBadges(WrapPanel panel, IEnumerable<string> platforms)
        {
        // platform etiketlerini yeniden kur
            panel.Children.Clear();

            foreach (string platform in platforms)
            {
                Border badge = new Border
                {
                    Background = CreateBrush("#111114"),
                    BorderBrush = CreateBrush("#1C1C22"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 10, 10)
                };

                badge.Child = new TextBlock
                {
                    Text = platform,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                };

                panel.Children.Add(badge);
            }
        }

        private void ShowPlaceholder(string message)
        {
        // secim yoksa bos ekrani ac
            DetailScroll.Visibility = Visibility.Collapsed;
            DetailPlaceholder.Visibility = Visibility.Visible;
            txtPlaceholderHint.Text = message;
        }

        private void RefreshOwnerStats()
        {
        // dashboard sayilarini yenile
            if (Owner is AdminDashboard dashboard)
            {
                dashboard.RefreshDashboardStats();
            }
        }

        private SolidColorBrush CreateBrush(string colorValue)
        {
        // secili duruma uygun rengi hazirla
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorValue));
        }

        private void ToggleWindowState()
        {
        // pencere boyutunu degistir
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}

