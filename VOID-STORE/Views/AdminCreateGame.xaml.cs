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
        private List<string> _selectedGalleryPaths = new();

        public AdminCreateGame()
        {
        // acilis verilerini hazirla
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

            ResetFormFields();
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

        private void ToggleWindowStateButton_Click(object sender, RoutedEventArgs e)
        {
            // pencere durumunu degistir
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

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
        // form alanlarini temizle
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
        // oyunu onay icin kaydet
            try
            {
                GameCreateRequest request = BuildRequest();
                int gameId = _adminGameController.CreateGame(request);

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
            // fiyat girisini kurala gore sinirla
            string nextValue = GetNextText(txtPrice.Text, e.Text, txtPrice.SelectionStart, txtPrice.SelectionLength);
            e.Handled = !PriceInputRegex.IsMatch(nextValue);
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
        // fiyat yazimini toparla
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
            // tarih girisini kurala gore sinirla
            string nextValue = GetNextText(txtReleaseDate.Text, e.Text, txtReleaseDate.SelectionStart, txtReleaseDate.SelectionLength);
            e.Handled = !Regex.IsMatch(nextValue, @"^\d{0,2}(\.\d{0,2}(\.\d{0,4})?)?$");
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
            // pencere boyutunu buyut ya da geri al
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private GameCreateRequest BuildRequest()
        {
        // formdaki verileri tek istekte topla
            return new GameCreateRequest
            {
                Title = txtTitle.Text.Trim(),
                PriceText = txtPrice.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                Developer = txtDeveloper.Text.Trim(),
                Publisher = txtPublisher.Text.Trim(),
                ReleaseDateText = txtReleaseDate.Text.Trim(),
                TrailerUrl = txtTrailerUrl.Text.Trim(),
                MinimumRequirements = txtMinimumRequirements.Text.Trim(),
                RecommendedRequirements = txtRecommendedRequirements.Text.Trim(),
                SupportedLanguages = txtSupportedLanguages.Text.Trim(),
                CoverImageSourcePath = _selectedCoverPath,
                Platforms = GetSelectedPlatforms(),
                GalleryImageSourcePaths = _selectedGalleryPaths.ToList()
            };
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

        private void ResetFormFields()
        {
        // tum alanlari ilk haline getir
            txtTitle.Clear();
            txtPrice.Clear();
            txtDescription.Clear();
            txtDeveloper.Clear();
            txtPublisher.Clear();
            txtReleaseDate.Clear();
            txtTrailerUrl.Clear();
            txtMinimumRequirements.Clear();
            txtRecommendedRequirements.Clear();
            txtSupportedLanguages.Clear();

            tglWindows.IsChecked = false;
            tglMacOs.IsChecked = false;
            tglLinux.IsChecked = false;

            _selectedCoverPath = string.Empty;
            _selectedGalleryPaths.Clear();

            UpdateCoverPreview();
            UpdateGalleryState();
        }

        private void UpdateCoverPreview()
        {
        // kapak onizlemesini guncelle
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
        // galeri bilgisini guncelle
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

        private static string GetNextText(string currentText, string incomingText, int selectionStart, int selectionLength)
        {
        // girilecek yeni metni hazirla
            string prefix = currentText[..selectionStart];
            string suffix = currentText[(selectionStart + selectionLength)..];
            return prefix + incomingText + suffix;
        }
    }
}
