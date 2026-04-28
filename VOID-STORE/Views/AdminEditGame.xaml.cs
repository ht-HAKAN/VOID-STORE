using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class AdminEditGame : Window
    {
        private static readonly Regex PriceInputRegex = new(@"^\d*$");
        private readonly AdminGameController _adminGameController;
        private List<AdminGameListItem> _games = new();
        private int _selectedGameId;
        private string _selectedCoverPath = string.Empty;
        private string _selectedTrailerPath = string.Empty;
        private List<string> _selectedGalleryPaths = new();

        public AdminEditGame()
        {
        // acilis verilerini yukle
            InitializeComponent();
            _adminGameController = new AdminGameController();

            try
            {
                _adminGameController.EnsureSchema();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(
                    "Oyun güncelleme altyapısı hazırlanamadı: " + ex.Message,
                    "SİSTEM HATASI");
                Loaded += (_, _) => Close();
                return;
            }

            DataObject.AddPastingHandler(txtPrice, PriceTextBox_OnPaste);
            LoadCategoryToggles();
            lstFeatures.ItemsSource = GameFeatureCatalog.All;

            LoadGames();
            ShowEditor(false);
        }
        // Seçilen oyunun verilerini doldurmak için PopulateTimeComboBoxes metoduna artık gerek yok
        private void LoadCategoryToggles()
        {
            wpCategories.Children.Clear();
            foreach (var category in GameCategoryCatalog.All)
            {
                var tgl = new System.Windows.Controls.Primitives.ToggleButton
                {
                    Content = category,
                    Style = (Style)FindResource("CategoryToggleStyle")
                };
                tgl.Click += (s, e) => {
                    var selectedCount = wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Count(x => x.IsChecked == true);
                    if (selectedCount > 3)
                    {
                        ((System.Windows.Controls.Primitives.ToggleButton)s).IsChecked = false;
                        CustomError.ShowDialog("En fazla 3 kategori seçebilirsiniz.", "BİLGİ");
                    }
                    UpdateNavigationState();
                };
                wpCategories.Children.Add(tgl);
            }
        }

        private string GetSelectedCategories()
        {
            var selected = wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>()
                .Where(x => x.IsChecked == true)
                .Select(x => x.Content?.ToString() ?? string.Empty);
            
            return GameCategoryCatalog.Normalize(string.Join(", ", selected));
        }

        private void SetSelectedCategories(string categoryString)
        {
            if (string.IsNullOrWhiteSpace(categoryString)) return;
            
            // Farklı ayraçları destekle ve boşlukları temizle
            var cats = categoryString.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            foreach (var tgl in wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>())
            {
                string toggleContent = tgl.Content?.ToString() ?? string.Empty;
                tgl.IsChecked = cats.Any(c => c.Equals(toggleContent, StringComparison.OrdinalIgnoreCase));
            }
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

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        // listeyi arama metnine gore daralt
            LoadGames(_selectedGameId);
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        // secilen oyunun verisini yukle
            if (lstGames.SelectedItem is not AdminGameListItem selectedItem)
            {
                ShowEditor(false);
                return;
            }

            try
            {
                GameEditState state = _adminGameController.GetGameEditState(selectedItem.GameId);
                _selectedGameId = selectedItem.GameId;
                ApplyState(state);
                ShowEditor(true);

                // Animasyonu tetikle
                if (Resources["FadeInAnimation"] is Storyboard sb)
                {
                    EditorScroll.BeginStoryboard(sb);
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(
                    "Oyun bilgileri yüklenemedi: " + ex.Message,
                    "SİSTEM HATASI");
            }
        }

        private void SelectCoverButton_Click(object sender, RoutedEventArgs e)
        {
        // yeni kapak gorselini sec
            if (_selectedGameId == 0)
            {
                return;
            }

            OpenFileDialog dialog = CreateImageDialog(false);

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string coverValidationMessage = GameAssetManager.ValidateCoverImage(dialog.FileName);

            if (!string.IsNullOrWhiteSpace(coverValidationMessage))
            {
                CustomError.ShowDialog(coverValidationMessage, "DOĞRULAMA HATASI");
                return;
            }

            _selectedCoverPath = dialog.FileName;
            UpdateCoverPreview();
            UpdateNavigationState();
        }

        private void SelectGalleryButton_Click(object sender, RoutedEventArgs e)
        {
        // galeriye yeni gorsel ekle
            if (_selectedGameId == 0)
            {
                return;
            }

            OpenFileDialog dialog = CreateImageDialog(true);

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            List<string> mergedPaths = _selectedGalleryPaths
                .Concat(dialog.FileNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (mergedPaths.Count > AdminGameController.MaxGalleryImageCount)
            {
                CustomError.ShowDialog(
                    $"En fazla {AdminGameController.MaxGalleryImageCount} oyun görseli ekleyebilirsiniz.",
                    "DOĞRULAMA HATASI");
                return;
            }

            _selectedGalleryPaths = mergedPaths;
            UpdateGalleryState();
            UpdateNavigationState();
        }

        private void RemoveGalleryButton_Click(object sender, RoutedEventArgs e)
        {
        // secilen gorseli listeden kaldir
            if (lstGalleryFiles.SelectedIndex < 0 || lstGalleryFiles.SelectedIndex >= _selectedGalleryPaths.Count)
            {
                return;
            }

            _selectedGalleryPaths.RemoveAt(lstGalleryFiles.SelectedIndex);
            UpdateGalleryState();
        }

        private void SelectTrailerButton_Click(object sender, RoutedEventArgs e)
        {
        // yeni fragman videosunu sec
            if (_selectedGameId == 0)
            {
                return;
            }

            OpenFileDialog dialog = CreateVideoDialog();

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _selectedTrailerPath = dialog.FileName;
            UpdateTrailerState();
        }

        private void ClearTrailerButton_Click(object sender, RoutedEventArgs e)
        {
        // secili fragman videosunu kaldir
            if (_selectedGameId == 0)
            {
                return;
            }

            _selectedTrailerPath = string.Empty;
            UpdateTrailerState();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
        // secili oyunun verisini yeniden yukle
            if (_selectedGameId == 0)
            {
                return;
            }

            try
            {
                GameEditState state = _adminGameController.GetGameEditState(_selectedGameId);
                ApplyState(state);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(
                    "Oyun bilgileri yenilenemedi: " + ex.Message,
                    "SİSTEM HATASI");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
        // yeni surumu onaya gonder
            if (_selectedGameId == 0)
            {
                return;
            }

            if (!ValidateForm())
            {
                return;
            }

            try
            {
                GameDraftSaveRequest request = BuildRequest();
                _adminGameController.SaveGameDraft(request);

                if (Owner is AdminDashboard dashboard)
                {
                    dashboard.RefreshDashboardStats();
                }

                CustomError.ShowDialog(
                    "Değişiklikler kaydedildi. Güncellenen sürüm onaya gönderildi.",
                    "BAŞARILI",
                    true);

                LoadGames(_selectedGameId);
                ApplyState(_adminGameController.GetGameEditState(_selectedGameId));
            }
            catch (InvalidOperationException ex)
            {
                CustomError.ShowDialog(ex.Message, "DOĞRULAMA HATASI");
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(
                    "Güncelleme kaydı sırasında bir hata oluştu: " + ex.Message,
                    "SİSTEM HATASI");
            }
        }


        private void Input_Changed(object sender, EventArgs e)
        {
            if (txtFreeHint != null && txtPrice != null)
            {
                bool isFree = txtPrice.Text == "0" || string.IsNullOrWhiteSpace(txtPrice.Text);
                txtFreeHint.Visibility = isFree ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateNavigationState();
        }


        private void OnlyNumbers_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        // saat degeri 0-23 arasinda olmasini zorlayan metot
        private void HourTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out int val)) val = 0;
                val = Math.Max(0, Math.Min(23, val));
                tb.Text = val.ToString("D2");
            }
        }

        // dakika degeri 0-59 arasinda olmasini zorlayan metot
        private void MinuteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out int val)) val = 0;
                val = Math.Max(0, Math.Min(59, val));
                tb.Text = val.ToString("D2");
            }
        }

        // indirim orani 0-100 araliginda olmali
        private void DiscountRate_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out int val)) val = 0;
                val = Math.Max(0, Math.Min(100, val));
                tb.Text = val.ToString();
            }
        }

        private void UpdateNavigationState()
        {
            if (TabItemGenel == null || TabItemMedya == null || TabItemSistem == null) return;

            // Step 1 Validation (Temel Bilgiler)
            bool isStep1Valid = !string.IsNullOrWhiteSpace(txtTitle.Text) &&
                               !string.IsNullOrWhiteSpace(txtPrice.Text) &&
                               wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Any(x => x.IsChecked == true) &&
                               !string.IsNullOrWhiteSpace(txtDeveloper.Text) &&
                               !string.IsNullOrWhiteSpace(txtPublisher.Text) &&
                               dpReleaseDate.SelectedDate != null;

            // Step 1 Validation
            TabItemMedya.IsEnabled = isStep1Valid;

            // Step 2 Validation
            bool isStep2Valid = !string.IsNullOrWhiteSpace(_selectedCoverPath);

            TabItemSistem.IsEnabled = isStep1Valid && isStep2Valid;
        }

        private void PriceTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
        // yapistirilan fiyat metnini denetle
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = (string)e.DataObject.GetData(typeof(string));
            string nextValue = GetNextText(txtPrice.Text, pastedText, txtPrice.SelectionStart, txtPrice.SelectionLength);

            if (!PriceInputRegex.IsMatch(nextValue))
            {
                e.CancelCommand();
            }
        }

        private void PriceTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                txtPrice.Text = "0";
            }
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void LoadGames(int preserveGameId = 0)
        {
        // yayindaki oyunlari yukle
            _games = _adminGameController
                .GetApprovedGames(txtSearch.Text.Trim())
                .ToList();

            lstGames.ItemsSource = null;
            lstGames.ItemsSource = _games;

            AdminGameListItem? selectedItem = null;

            if (preserveGameId > 0)
            {
                selectedItem = _games.FirstOrDefault(game => game.GameId == preserveGameId);
            }

            lstGames.SelectedItem = selectedItem;

            if (_games.Count == 0)
            {
                txtPlaceholderHint.Text = "Henüz düzenlenebilir yayındaki oyun bulunmuyor.";
                ShowEditor(false);
            }
            else if (selectedItem == null)
            {
                txtPlaceholderHint.Text = "Soldan bir oyun seçin. Sağ tarafta düzenleme alanı açılır.";
                ShowEditor(false);
            }
        }

        private void ApplyState(GameEditState state)
        {
        // secilen oyunun alanlarini doldur
            txtTitle.Text = state.Title;
            SetSelectedCategories(state.Category);
            txtPrice.Text = state.PriceText;
            txtDescription.Text = state.Description;
            txtDeveloper.Text = state.Developer;
            txtPublisher.Text = state.Publisher;
            // Tarih: sadece gun/ay/yil, saat kaldirildi
            try
            {
                if (state.ReleaseDate.HasValue)
                {
                    dpReleaseDate.SelectedDate = state.ReleaseDate.Value.Date;
                }
                else if (!string.IsNullOrWhiteSpace(state.ReleaseDateText))
                {
                    if (DateTime.TryParseExact(state.ReleaseDateText.Split(' ')[0].Trim(), "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                        dpReleaseDate.SelectedDate = dt.Date;
                    else
                        dpReleaseDate.SelectedDate = null;
                }
                else
                {
                    dpReleaseDate.SelectedDate = null;
                }
            }
            catch
            {
                dpReleaseDate.SelectedDate = null;
            }

            txtSupportedLanguages.Text = state.SupportedLanguages;
            
            // Yeni Alanlar
            txtFreeHint.Visibility = state.IsFree ? Visibility.Visible : Visibility.Collapsed;
            txtDiscountRate.Text = state.DiscountRate.ToString();
            
            dpDiscountStart.SelectedDate = state.DiscountStartDate;
            if (state.DiscountStartDate.HasValue)
            {
                txtDiscountStartHour.Text = state.DiscountStartDate.Value.Hour.ToString("D2");
                txtDiscountStartMinute.Text = state.DiscountStartDate.Value.Minute.ToString("D2");
            }
            else
            {
                txtDiscountStartHour.Text = "00";
                txtDiscountStartMinute.Text = "00";
            }

            dpDiscountEnd.SelectedDate = state.DiscountEndDate;
            if (state.DiscountEndDate.HasValue)
            {
                txtDiscountEndHour.Text = state.DiscountEndDate.Value.Hour.ToString("D2");
                txtDiscountEndMinute.Text = state.DiscountEndDate.Value.Minute.ToString("D2");
            }
            else
            {
                txtDiscountEndHour.Text = "00";
                txtDiscountEndMinute.Text = "00";
            }

            // Sistem gereksinimleri
            txtMinimumRequirements.Text = state.MinimumRequirements;
            txtRecommendedRequirements.Text = state.RecommendedRequirements;

            lstFeatures.UnselectAll();

            tglWindows.IsChecked = state.Platforms.Any(platform => platform.Equals("Windows", StringComparison.OrdinalIgnoreCase));
            tglMacOs.IsChecked = state.Platforms.Any(platform => platform.Equals("macOS", StringComparison.OrdinalIgnoreCase));
            tglLinux.IsChecked = state.Platforms.Any(platform => platform.Equals("Linux", StringComparison.OrdinalIgnoreCase));

            foreach (string feature in state.Features)
            {
                lstFeatures.SelectedItems.Add(feature);
            }

            _selectedCoverPath = state.CoverImageSourcePath;
            _selectedTrailerPath = state.TrailerVideoSourcePath;
            _selectedGalleryPaths = state.GalleryImageSourcePaths.ToList();

            if (state.HasPendingDraft)
            {
                txtDraftStatus.Text = "Kaydedilen değişiklikler";
                txtDraftHint.Text = "Bu alandaki bilgiler onay bekleyen son değişiklikleri gösterir.";
            }
            else
            {
                txtDraftStatus.Text = "Yayındaki sürüm";
                txtDraftHint.Text = "Bu alanda şu anda mağazada görünen bilgiler yer alır.";
            }

            UpdateCoverPreview();
            UpdateTrailerState();
            UpdateGalleryState();

            UpdateCoverPreview();
            UpdateTrailerState();
            UpdateGalleryState();

            UpdateNavigationState();
        }

        private bool ValidateForm()
        {
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                txtTitleError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                txtTitleError.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                isValid = false;
            }

            if (!wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Any(x => x.IsChecked == true))
            {
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(txtDeveloper.Text))
            {
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(txtPublisher.Text))
            {
                isValid = false;
            }

            // Çıkış Tarihi
            if (dpReleaseDate.SelectedDate == null)
            {
                isValid = false;
            }

            // Indirim Tarihi Validasyonu
            try
            {
                int discountRate = int.TryParse(txtDiscountRate.Text, out int dr) ? dr : 0;
                if (discountRate > 0)
                {
                    // Cikis tarihini sadece tarih olarak al (saat yok)
                    DateTime? releaseDateTime = dpReleaseDate.SelectedDate.HasValue
                        ? dpReleaseDate.SelectedDate.Value.Date
                        : (DateTime?)null;

                    // Indirim baslangic ve bitis tarihleri
                    DateTime? discStart = null;
                    if (dpDiscountStart.SelectedDate.HasValue)
                    {
                        int h = int.TryParse(txtDiscountStartHour.Text, out int dsh) ? dsh : 0;
                        int m = int.TryParse(txtDiscountStartMinute.Text, out int dsm) ? dsm : 0;
                        var d = dpDiscountStart.SelectedDate.Value;
                        discStart = new DateTime(d.Year, d.Month, d.Day, h, m, 0);
                    }

                    DateTime? discEnd = null;
                    if (dpDiscountEnd.SelectedDate.HasValue)
                    {
                        int h = int.TryParse(txtDiscountEndHour.Text, out int deh) ? deh : 0;
                        int m = int.TryParse(txtDiscountEndMinute.Text, out int dem) ? dem : 0;
                        var d = dpDiscountEnd.SelectedDate.Value;
                        discEnd = new DateTime(d.Year, d.Month, d.Day, h, m, 0);
                    }

                    if (releaseDateTime.HasValue && discStart.HasValue && discStart.Value < releaseDateTime.Value)
                    {
                        CustomError.ShowDialog("\u0130ndirim başlangıc tarihi, oyunun çıkış tarihinden önce olamaz.", "DOĞRULAMA HATASI");
                        return false;
                    }

                    if (discStart.HasValue && discEnd.HasValue && discEnd.Value <= discStart.Value)
                    {
                        CustomError.ShowDialog("\u0130ndirim bitiş tarihi ve saati, başlangıçtan sonra olmalıdır.", "DOĞRULAMA HATASI");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Tarih doğrulama sırasında hata: " + ex.Message, "HATA");
                return false;
            }

            if (!isValid)
            {
                CustomError.ShowDialog("Lütfen zorunlu alanları doldurun.", "DOĞRULAMA HATASI");
            }

            return isValid;
        }

        private void UpdateCoverPreview()
        {
        // kapak onizlemesini yenile
            txtCoverStatus.Text = string.IsNullOrWhiteSpace(_selectedCoverPath)
                ? "Henüz seçilmedi"
                : System.IO.Path.GetFileName(_selectedCoverPath);

            var bitmap = GameAssetManager.LoadBitmap(_selectedCoverPath);

            if (bitmap == null)
            {
                imgCoverPreview.Source = null;
                imgCoverPreview.Visibility = Visibility.Collapsed;
                CoverPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            imgCoverPreview.Source = bitmap;
            imgCoverPreview.Visibility = Visibility.Visible;
            CoverPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void UpdateGalleryState()
        {
        // galeri listesini yenile
            txtGalleryStatus.Text = $"{_selectedGalleryPaths.Count} / {AdminGameController.MaxGalleryImageCount} görsel seçildi";
            lstGalleryFiles.ItemsSource = null;
            lstGalleryFiles.ItemsSource = _selectedGalleryPaths
                .Select(System.IO.Path.GetFileName)
                .ToList();
        }

        private void UpdateTrailerState()
        {
        // fragman durumunu yenile
            txtTrailerStatus.Text = string.IsNullOrWhiteSpace(_selectedTrailerPath)
                ? "Fragman seçilmedi"
                : System.IO.Path.GetFileName(_selectedTrailerPath);
        }

        private void ShowEditor(bool isVisible)
        {
        // duzenleme alanini ac ya da gizle
            EditorScroll.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            EditorPlaceholder.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        private GameDraftSaveRequest BuildRequest()
        {
            // Sadece tarih, saat yok
            string releaseDateStr = dpReleaseDate.SelectedDate?.ToString("dd.MM.yyyy") ?? "";

            DateTime? discStart = null;
            if (dpDiscountStart.SelectedDate.HasValue)
            {
                var d = dpDiscountStart.SelectedDate.Value;
                int h = int.TryParse(txtDiscountStartHour.Text, out int parsedH) ? parsedH : 0;
                int m = int.TryParse(txtDiscountStartMinute.Text, out int parsedM) ? parsedM : 0;
                discStart = new DateTime(d.Year, d.Month, d.Day, h, m, 0);
            }

            DateTime? discEnd = null;
            if (dpDiscountEnd.SelectedDate.HasValue)
            {
                var d = dpDiscountEnd.SelectedDate.Value;
                int h = int.TryParse(txtDiscountEndHour.Text, out int parsedH) ? parsedH : 0;
                int m = int.TryParse(txtDiscountEndMinute.Text, out int parsedM) ? parsedM : 0;
                discEnd = new DateTime(d.Year, d.Month, d.Day, h, m, 0);
            }

            return new GameDraftSaveRequest
            {
                GameId = _selectedGameId,
                Title = txtTitle.Text.Trim(),
                Category = GetSelectedCategories(),
                PriceText = txtPrice.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                Developer = txtDeveloper.Text.Trim(),
                Publisher = txtPublisher.Text.Trim(),
                ReleaseDateText = releaseDateStr,
                TrailerVideoSourcePath = _selectedTrailerPath,
                MinimumRequirements = txtMinimumRequirements.Text.Trim(),
                RecommendedRequirements = txtRecommendedRequirements.Text.Trim(),
                SupportedLanguages = txtSupportedLanguages.Text.Trim(),
                CoverImageSourcePath = _selectedCoverPath,
                Platforms = GetSelectedPlatforms(),
                Features = GetSelectedFeatures(),
                GalleryImageSourcePaths = _selectedGalleryPaths.ToList(),
                
                IsFree = txtPrice.Text == "0",
                DiscountRate = int.TryParse(txtDiscountRate.Text, out int rate) ? rate : 0,
                DiscountStartDate = discStart,
                DiscountEndDate = discEnd
            };
        }

        private DateTime? ParseOptionalDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
            return null;
        }

        private List<string> GetSelectedPlatforms()
        {
        // secilen platformlari topla
            List<string> platforms = new();

            if (tglWindows.IsChecked == true)
            {
                platforms.Add("Windows");
            }

            if (tglMacOs.IsChecked == true)
            {
                platforms.Add("macOS");
            }

            if (tglLinux.IsChecked == true)
            {
                platforms.Add("Linux");
            }

            return platforms;
        }

        private List<string> GetSelectedFeatures()
        {
        // secilen ozellikleri topla
            return lstFeatures.SelectedItems
                .Cast<string>()
                .ToList();
        }

        private OpenFileDialog CreateImageDialog(bool allowMultiple)
        {
        // gorsel secim penceresini hazirla
            return new OpenFileDialog
            {
                Filter = "Görsel Dosyaları|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
                Multiselect = allowMultiple,
                Title = allowMultiple ? "Oyun görsellerini seçin" : "Kapak görselini seçin"
            };
        }

        private OpenFileDialog CreateVideoDialog()
        {
        // video secim penceresini hazirla
            return new OpenFileDialog
            {
                Filter = "Video Dosyaları|*.mp4;*.webm;*.mov;*.m4v",
                Multiselect = false,
                Title = "Oyun fragmanını seçin"
            };
        }

        private static string GetNextText(string currentText, string incomingText, int selectionStart, int selectionLength)
        {
        // yeni metni hesapla
            string prefix = currentText[..selectionStart];
            string suffix = currentText[(selectionStart + selectionLength)..];
            return prefix + incomingText + suffix;
        }
    }
}

