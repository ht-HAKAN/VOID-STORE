using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class AdminCreateGame : Window
    {
        private static readonly Regex PriceInputRegex = new(@"^\d*$");
        private readonly AdminGameController _adminGameController;
        private string _selectedCoverPath = string.Empty;
        private string _selectedTrailerPath = string.Empty;
        private List<string> _selectedGalleryPaths = new();

        public AdminCreateGame()
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
                    "Oyun ekleme altyapısı hazırlanamadı: " + ex.Message,
                    "SİSTEM HATASI");
                Loaded += (_, _) => Close();
                return;
            }

            DataObject.AddPastingHandler(txtPrice, PriceTextBox_OnPaste);
            LoadCategoryToggles();
            lstFeatures.ItemsSource = GameFeatureCatalog.All;

            ResetFormFields();
        }

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
        // onceki ekrana don
            Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
        // formu temizle
            ResetFormFields();
        }

        private void SelectCoverButton_Click(object sender, RoutedEventArgs e)
        {
        // kapak gorselini sec
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
        // galeri gorsellerini ekle
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
        }

        private void ClearGalleryButton_Click(object sender, RoutedEventArgs e)
        {
        // secilen gorselleri temizle
            _selectedCoverPath = string.Empty;
            _selectedGalleryPaths.Clear();
            UpdateCoverPreview();
            UpdateGalleryState();
        }

        private void SelectTrailerButton_Click(object sender, RoutedEventArgs e)
        {
        // fragman videosunu sec
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
        // secili fragman videosunu temizle
            _selectedTrailerPath = string.Empty;
            UpdateTrailerState();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
        // oyunu onay bekleyen kayit olarak ekle
            if (!ValidateForm())
            {
                return;
            }

            try
            {
                GameCreateRequest request = BuildRequest();
                _adminGameController.CreateGame(request);

                if (Owner is AdminDashboard dashboard)
                {
                    dashboard.RefreshDashboardStats();
                }

                CustomError.ShowDialog(
                    "Oyun kaydı oluşturuldu. Yayına alınması için onaya gönderildi.",
                    "BAŞARILI",
                    true);

                ResetFormFields();
            }
            catch (InvalidOperationException ex)
            {
                CustomError.ShowDialog(ex.Message, "DOĞRULAMA HATASI");
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(
                    "Oyun kaydı sırasında bir hata oluştu: " + ex.Message,
                    "SİSTEM HATASI");
            }
        }

        private void PriceTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // fiyat girisini sinirla
            string nextValue = GetNextText(txtPrice.Text, e.Text, txtPrice.SelectionStart, txtPrice.SelectionLength);
            e.Handled = !PriceInputRegex.IsMatch(nextValue);
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

            // Tab 1 Validation (Genel Bilgiler)
            bool isTab1Valid = IsTab1Valid();
            TabItemMedya.IsEnabled = isTab1Valid;

            // Tab 2 Validation (Medya)
            bool isTab2Valid = IsTab2Valid();
            TabItemMedya.Tag = isTab2Valid ? "Valid" : null;
            TabItemSistem.IsEnabled = isTab1Valid && isTab2Valid;
            
            // Gerçek zamanlı temizleme (Opsiyonel: Kullanıcı yazarken hataları silebiliriz)
            if (isTab1Valid) ClearTab1Errors();
            if (isTab2Valid) ClearTab2Errors();
        }

        private void ClearTab1Errors()
        {
            SetFieldValid(txtTitle, txtTitleError);
            SetFieldValid(txtPrice, txtPriceError);
            SetFieldValid(txtDeveloper, txtDeveloperError);
            SetFieldValid(txtPublisher, txtPublisherError);
            SetFieldValid(txtDescription, txtDescriptionError);
            SetFieldValid(dpReleaseDate, txtReleaseDateError);
            txtCategoryError.Visibility = Visibility.Collapsed;
        }

        private void ClearTab2Errors()
        {
            txtCoverError.Visibility = Visibility.Collapsed;
            SetFieldValid(lstGalleryFiles, txtGalleryError);
        }

        private void SetFieldInvalid(System.Windows.Controls.Control control, TextBlock errorBlock)
        {
            control.BorderBrush = System.Windows.Media.Brushes.Red;
            control.BorderThickness = new Thickness(1.5);
            errorBlock.Visibility = Visibility.Visible;
        }

        private void SetFieldValid(System.Windows.Controls.Control control, TextBlock errorBlock)
        {
            control.BorderBrush = TryFindResource("InputBorderBrush") as System.Windows.Media.Brush;
            if (control.BorderBrush == null) control.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 29, 34));
            control.BorderThickness = new Thickness(1);
            errorBlock.Visibility = Visibility.Collapsed;
        }

        private void ShowTab1Errors()
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text)) SetFieldInvalid(txtTitle, txtTitleError);
            else SetFieldValid(txtTitle, txtTitleError);

            if (string.IsNullOrWhiteSpace(txtPrice.Text)) SetFieldInvalid(txtPrice, txtPriceError);
            else SetFieldValid(txtPrice, txtPriceError);

            if (string.IsNullOrWhiteSpace(txtDeveloper.Text)) SetFieldInvalid(txtDeveloper, txtDeveloperError);
            else SetFieldValid(txtDeveloper, txtDeveloperError);

            if (string.IsNullOrWhiteSpace(txtPublisher.Text)) SetFieldInvalid(txtPublisher, txtPublisherError);
            else SetFieldValid(txtPublisher, txtPublisherError);

            if (string.IsNullOrWhiteSpace(txtDescription.Text)) SetFieldInvalid(txtDescription, txtDescriptionError);
            else SetFieldValid(txtDescription, txtDescriptionError);

            if (dpReleaseDate.SelectedDate == null) SetFieldInvalid(dpReleaseDate, txtReleaseDateError);
            else SetFieldValid(dpReleaseDate, txtReleaseDateError);

            txtCategoryError.Visibility = wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Any(x => x.IsChecked == true) 
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowTab2Errors()
        {
            txtCoverError.Visibility = string.IsNullOrWhiteSpace(_selectedCoverPath) ? Visibility.Visible : Visibility.Collapsed;
            
            if (_selectedGalleryPaths.Count < AdminGameController.MinGalleryImageCount) SetFieldInvalid(lstGalleryFiles, txtGalleryError);
            else SetFieldValid(lstGalleryFiles, txtGalleryError);
        }

        private void ShowTab3Errors()
        {
            txtPlatformError.Visibility = (tglWindows.IsChecked == true || tglMacOs.IsChecked == true || tglLinux.IsChecked == true) 
                ? Visibility.Collapsed : Visibility.Visible;

            txtFeatureError.Visibility = lstFeatures.SelectedItems.Count >= 1 ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrWhiteSpace(txtSupportedLanguages.Text)) SetFieldInvalid(txtSupportedLanguages, txtLanguagesError);
            else SetFieldValid(txtSupportedLanguages, txtLanguagesError);

            if (string.IsNullOrWhiteSpace(txtMinimumRequirements.Text)) SetFieldInvalid(txtMinimumRequirements, txtMinReqError);
            else SetFieldValid(txtMinimumRequirements, txtMinReqError);

            if (string.IsNullOrWhiteSpace(txtRecommendedRequirements.Text)) SetFieldInvalid(txtRecommendedRequirements, txtRecReqError);
            else SetFieldValid(txtRecommendedRequirements, txtRecReqError);
        }

        private bool IsTab1Valid()
        {
            return !string.IsNullOrWhiteSpace(txtTitle.Text) &&
                   !string.IsNullOrWhiteSpace(txtPrice.Text) &&
                   !string.IsNullOrWhiteSpace(txtDeveloper.Text) &&
                   !string.IsNullOrWhiteSpace(txtPublisher.Text) &&
                   !string.IsNullOrWhiteSpace(txtDescription.Text) &&
                   dpReleaseDate.SelectedDate != null &&
                   wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Any(x => x.IsChecked == true);
        }

        private bool IsTab2Valid()
        {
            return !string.IsNullOrWhiteSpace(_selectedCoverPath) &&
                   _selectedGalleryPaths.Count >= AdminGameController.MinGalleryImageCount &&
                   _selectedGalleryPaths.Count <= AdminGameController.MaxGalleryImageCount;
        }

        private bool IsTab3Valid()
        {
            return lstFeatures.SelectedItems.Count >= 1 &&
                   !string.IsNullOrWhiteSpace(txtSupportedLanguages.Text) &&
                   !string.IsNullOrWhiteSpace(txtMinimumRequirements.Text) &&
                   !string.IsNullOrWhiteSpace(txtRecommendedRequirements.Text) &&
                   (tglWindows.IsChecked == true || tglMacOs.IsChecked == true || tglLinux.IsChecked == true);
        }

        private void NextTab_Click(object sender, RoutedEventArgs e)
        {
            if (AdminTabControl.SelectedIndex == 0) // Genel Bilgiler -> Medya
            {
                if (!IsTab1Valid())
                {
                    ShowTab1Errors();
                    CustomError.ShowDialog("Lütfen Genel Bilgiler sayfasındaki tüm zorunlu alanları doldurun.", "DOĞRULAMA HATASI");
                    return;
                }
            }
            else if (AdminTabControl.SelectedIndex == 1) // Medya -> Sistem
            {
                if (!IsTab2Valid())
                {
                    ShowTab2Errors();
                    CustomError.ShowDialog($"Lütfen bir kapak fotoğrafı seçin ve en az {AdminGameController.MinGalleryImageCount} adet galeri görseli ekleyin.", "DOĞRULAMA HATASI");
                    return;
                }
            }

            if (AdminTabControl.SelectedIndex < AdminTabControl.Items.Count - 1)
            {
                AdminTabControl.SelectedIndex++;
            }
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

        private GameCreateRequest BuildRequest()
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

            return new GameCreateRequest
            {
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
            List<string> platforms = new List<string>();

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

        private void ResetFormFields()
        {
        // alanlari ilk haline getir
            txtTitle.Clear();
            txtPrice.Clear();
            txtDescription.Clear();
            txtDeveloper.Clear();
            txtPublisher.Clear();
            dpReleaseDate.SelectedDate = null;
            
            foreach (var tgl in wpCategories.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>())
                tgl.IsChecked = false;
            
            lstFeatures.UnselectAll();

            txtTitleError.Visibility = Visibility.Collapsed;

            tglWindows.IsChecked = false;
            tglMacOs.IsChecked = false;
            tglLinux.IsChecked = false;

            _selectedTrailerPath = string.Empty;
            _selectedGalleryPaths.Clear();

            txtPrice.Text = "0";
            txtDiscountRate.Text = "0";
            dpDiscountStart.SelectedDate = null;
            dpDiscountEnd.SelectedDate = null;

            // Sadece indirim saatlerini sifirla
            if (txtDiscountStartHour != null)
            {
                txtDiscountStartHour.Text = "00";
                txtDiscountStartMinute.Text = "00";
                txtDiscountEndHour.Text = "00";
                txtDiscountEndMinute.Text = "00";
            }
            
            txtSupportedLanguages.Clear();
            txtMinimumRequirements.Clear();
            txtRecommendedRequirements.Clear();

            ClearTab1Errors();
            ClearTab2Errors();
            txtPlatformError.Visibility = Visibility.Collapsed;
            txtFeatureError.Visibility = Visibility.Collapsed;
            SetFieldValid(txtSupportedLanguages, txtLanguagesError);
            SetFieldValid(txtMinimumRequirements, txtMinReqError);
            SetFieldValid(txtRecommendedRequirements, txtRecReqError);

            UpdateCoverPreview();
            UpdateTrailerState();
            UpdateGalleryState();
            UpdateNavigationState();
        }

        private bool ValidateForm()
        {
            if (!IsTab1Valid())
            {
                AdminTabControl.SelectedIndex = 0;
                ShowTab1Errors();
                CustomError.ShowDialog("Genel Bilgiler kısmında eksik alanlar var.", "DOĞRULAMA HATASI");
                return false;
            }

            if (!IsTab2Valid())
            {
                AdminTabControl.SelectedIndex = 1;
                ShowTab2Errors();
                CustomError.ShowDialog($"Medya kısmında eksik alanlar var (En az {AdminGameController.MinGalleryImageCount} görsel seçmelisiniz).", "DOĞRULAMA HATASI");
                return false;
            }

            if (!IsTab3Valid())
            {
                AdminTabControl.SelectedIndex = 2;
                ShowTab3Errors();
                CustomError.ShowDialog("Sistem & Platform kısmında eksik alanlar var (Özellikler, Diller ve Gereksinimler zorunludur).", "DOĞRULAMA HATASI");
                return false;
            }

            // Indirim tarihi validasyonu
            try
            {
                int discountRate = int.TryParse(txtDiscountRate.Text, out int dr) ? dr : 0;
                if (discountRate > 0)
                {
                    // Cikis tarihini sadece tarih olarak al (saat yok)
                    DateTime? releaseDateTime = dpReleaseDate.SelectedDate.HasValue
                        ? dpReleaseDate.SelectedDate.Value.Date
                        : (DateTime?)null;

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
                        CustomError.ShowDialog("İndirim başlangıç tarihi, oyunun çıkış tarihinden önce olamaz.", "DOĞRULAMA HATASI");
                        return false;
                    }

                    if (discStart.HasValue && discEnd.HasValue && discEnd.Value <= discStart.Value)
                    {
                        CustomError.ShowDialog("İndirim bitiş tarihi ve saati, başlangıçtan sonra olmalıdır.", "DOĞRULAMA HATASI");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Tarih doğrulama sırasında hata: " + ex.Message, "HATA");
                return false;
            }

            return true;
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

        private void UpdateTrailerState()
        {
        // fragman durumunu yenile
            txtTrailerStatus.Text = string.IsNullOrWhiteSpace(_selectedTrailerPath)
                ? "Fragman seçilmedi"
                : System.IO.Path.GetFileName(_selectedTrailerPath);
        }

        private void UpdateGalleryState()
        {
        // galeri bilgisini yenile
            txtGalleryStatus.Text = $"{_selectedGalleryPaths.Count} / {AdminGameController.MaxGalleryImageCount} görsel seçildi";
            lstGalleryFiles.ItemsSource = null;
            lstGalleryFiles.ItemsSource = _selectedGalleryPaths
                .Select(System.IO.Path.GetFileName)
                .ToList();
        }

        private OpenFileDialog CreateImageDialog(bool allowMultiple)
        {
        // gorsel secim penceresini hazirla
            return new OpenFileDialog
            {
                Filter = "Görsel Dosyaları|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
                Multiselect = allowMultiple,
                Title = allowMultiple ? "Oyun görsellerini seçin" : "Ana görseli seçin"
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
