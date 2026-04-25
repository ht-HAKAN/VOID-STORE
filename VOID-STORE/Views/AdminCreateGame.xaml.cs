using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class AdminCreateGame : Window
    {
        private static readonly Regex PriceInputRegex = new(@"^(?:|0|[1-9]\d{0,3})(?:[.,]\d{0,2})?$");
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
            DataObject.AddPastingHandler(txtReleaseDate, ReleaseDateTextBox_OnPaste);
            lstCategory.ItemsSource = GameCategoryCatalog.All;
            lstFeatures.ItemsSource = GameFeatureCatalog.All;

            ResetFormFields();
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
            UpdateNavigationState();
        }

        private void tglIsFree_Click(object sender, RoutedEventArgs e)
        {
            bool isFree = tglIsFree.IsChecked == true;
            gridPriceInput.IsEnabled = !isFree;
            borderDiscount.IsEnabled = !isFree;

            if (isFree)
            {
                txtPrice.Text = "0";
                txtDiscountRate.Text = "0";
                txtDiscountStart.Clear();
                txtDiscountEnd.Clear();
            }
            
            UpdateNavigationState();
        }

        private void OnlyNumbers_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void UpdateNavigationState()
        {
            if (TabItemGenel == null || TabItemMedya == null || TabItemSistem == null) return;

            // Step 1 Validation (Temel Bilgiler)
            bool isPriceValid = tglIsFree.IsChecked == true || !string.IsNullOrWhiteSpace(txtPrice.Text);
            bool isStep1Valid = !string.IsNullOrWhiteSpace(txtTitle.Text) &&
                               isPriceValid &&
                               lstCategory.SelectedItem != null &&
                               !string.IsNullOrWhiteSpace(txtDeveloper.Text) &&
                               !string.IsNullOrWhiteSpace(txtPublisher.Text);

            TabItemGenel.Tag = isStep1Valid ? "Valid" : null;
            TabItemMedya.IsEnabled = isStep1Valid;

            // Step 2 Validation (Medya - En az kapak fotoğrafı zorunlu)
            bool isStep2Valid = !string.IsNullOrWhiteSpace(_selectedCoverPath);

            TabItemMedya.Tag = isStep2Valid ? "Valid" : null;
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
        // fiyat yazimini duzenle
            if (string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                return;
            }

            string normalized = txtPrice.Text.Trim().Replace(".", ",");

            if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.GetCultureInfo("tr-TR"),
                out decimal price))
            {
                return;
            }

            txtPrice.Text = price % 1 == 0
                ? price.ToString("0", CultureInfo.GetCultureInfo("tr-TR"))
                : price.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"));
        }

        private void ReleaseDateTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
        // tarih girisini sinirla
            string nextValue = GetNextText(txtReleaseDate.Text, e.Text, txtReleaseDate.SelectionStart, txtReleaseDate.SelectionLength);
            e.Handled = !Regex.IsMatch(nextValue, @"^\d{0,2}(\.\d{0,2}(\.\d{0,4})?)?$");
        }

        private void ReleaseDateTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtReleaseDate.Text == "GG.AA.YYYY")
            {
                txtReleaseDate.Text = "";
                txtReleaseDate.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void ReleaseDateTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtReleaseDate.Text))
            {
                txtReleaseDate.Text = "GG.AA.YYYY";
                txtReleaseDate.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85));
            }
        }

        private void ReleaseDateTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
        // yapistirilan tarihi denetle
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = (string)e.DataObject.GetData(typeof(string));
            if (!Regex.IsMatch(pastedText, @"^\d{0,2}(\.\d{0,2}(\.\d{0,4})?)?$"))
            {
                e.CancelCommand();
            }
        }

        private void ToggleWindowState()
        {
        // pencere boyutunu degistir
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private GameCreateRequest BuildRequest()
        {
        // form verisini tek istekte topla
            return new GameCreateRequest
            {
                Title = txtTitle.Text.Trim(),
                Category = GameCategoryCatalog.Normalize(lstCategory.SelectedItem?.ToString()),
                PriceText = txtPrice.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                Developer = txtDeveloper.Text.Trim(),
                Publisher = txtPublisher.Text.Trim(),
                ReleaseDateText = txtReleaseDate.Text.Trim(),
                TrailerVideoSourcePath = _selectedTrailerPath,
                MinimumRequirements = txtMinimumRequirements.Text.Trim(),
                RecommendedRequirements = txtRecommendedRequirements.Text.Trim(),
                SupportedLanguages = txtSupportedLanguages.Text.Trim(),
                CoverImageSourcePath = _selectedCoverPath,
                Platforms = GetSelectedPlatforms(),
                Features = GetSelectedFeatures(),
                GalleryImageSourcePaths = _selectedGalleryPaths.ToList(),
                
                // Yeni Alanlar
                IsFree = tglIsFree.IsChecked == true,
                DiscountRate = int.TryParse(txtDiscountRate.Text, out int rate) ? rate : 0,
                DiscountStartDate = ParseOptionalDate(txtDiscountStart.Text),
                DiscountEndDate = ParseOptionalDate(txtDiscountEnd.Text)
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
            txtReleaseDate.Clear();
            txtMinimumRequirements.Clear();
            txtRecommendedRequirements.Clear();
            txtSupportedLanguages.Clear();
            lstCategory.SelectedItem = null;
            lstFeatures.UnselectAll();

            txtTitleError.Visibility = Visibility.Collapsed;
            txtPriceError.Visibility = Visibility.Collapsed;
            txtDeveloperError.Visibility = Visibility.Collapsed;
            txtPublisherError.Visibility = Visibility.Collapsed;
            txtCategoryError.Visibility = Visibility.Collapsed;

            tglWindows.IsChecked = false;
            tglMacOs.IsChecked = false;
            tglLinux.IsChecked = false;

            _selectedTrailerPath = string.Empty;
            _selectedGalleryPaths.Clear();

            tglIsFree.IsChecked = false;
            gridPriceInput.IsEnabled = true;
            borderDiscount.IsEnabled = true;
            txtDiscountRate.Text = "0";
            txtDiscountStart.Clear();
            txtDiscountEnd.Clear();

            UpdateCoverPreview();
            UpdateTrailerState();
            UpdateGalleryState();
            UpdateNavigationState();
        }

        private bool ValidateForm()
        {
            bool isValid = true;

            // Oyun Adı
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                txtTitleError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                txtTitleError.Visibility = Visibility.Collapsed;
            }

            // Fiyat
            bool isPriceRequired = tglIsFree.IsChecked != true;
            if (isPriceRequired && string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                txtPriceError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                txtPriceError.Visibility = Visibility.Collapsed;
            }

            // Kategori
            if (lstCategory.SelectedItem == null)
            {
                txtCategoryError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                txtCategoryError.Visibility = Visibility.Collapsed;
            }

            // Yapımcı
            if (string.IsNullOrWhiteSpace(txtDeveloper.Text))
            {
                txtDeveloperError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                txtDeveloperError.Visibility = Visibility.Collapsed;
            }

            // Yayıncı
            if (string.IsNullOrWhiteSpace(txtPublisher.Text))
            {
                txtPublisherError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                txtPublisherError.Visibility = Visibility.Collapsed;
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
