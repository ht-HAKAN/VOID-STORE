using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using VOID_STORE.Controllers;
using VOID_STORE.Models;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;

namespace VOID_STORE.Views
{
    public partial class MainAppWindow : Window
    {
        private static readonly Brush DefaultCardBorderBrush = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x1D));
        private const string OwnedStatusAccentHex = "#82E4B0";
        private const string TrailerPlayerHostName = "voidstore.local";
        private const double DetailMediaThumbnailWidth = 144;
        private const double DetailMediaThumbnailSpacing = 12;
        private const string TrailerPlayerHtml = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <style>
        html, body {
            margin: 0;
            width: 100%;
            height: 100%;
            overflow: hidden;
            background: #0a0a0d;
        }

        body {
            display: flex;
            align-items: stretch;
            justify-content: stretch;
        }

        #shell {
            position: relative;
            width: 100%;
            height: 100%;
            overflow: hidden;
            background: #0a0a0d;
        }

        #player {
            width: 100%;
            height: 100%;
            object-fit: cover;
            background: #0a0a0d;
            outline: none;
        }

        #player::-webkit-media-controls {
            display: none !important;
        }

        #player::-webkit-media-controls-enclosure {
            display: none !important;
        }

        #ui {
            position: absolute;
            inset: 0;
            pointer-events: none;
        }

        #ui::before {
            content: "";
            position: absolute;
            inset: 0;
            background: linear-gradient(to bottom, rgba(6, 8, 12, 0.42) 0%, rgba(6, 8, 12, 0) 22%, rgba(6, 8, 12, 0) 66%, rgba(6, 8, 12, 0.36) 100%);
            opacity: 0;
            transition: opacity 180ms ease;
        }

        #shell.show-ui #ui::before,
        #shell.is-paused #ui::before {
            opacity: 1;
        }

        .center-toggle,
        .tap-feedback {
            display: flex;
            align-items: center;
            justify-content: center;
            color: #ffffff;
        }

        .center-toggle {
            border: 1px solid rgba(255, 255, 255, 0.18);
            background: rgba(11, 14, 20, 0.58);
            backdrop-filter: blur(18px);
            -webkit-backdrop-filter: blur(18px);
            box-shadow: 0 14px 40px rgba(0, 0, 0, 0.28);
            cursor: pointer;
            pointer-events: auto;
            transition: transform 160ms ease, opacity 160ms ease, background 160ms ease, border-color 160ms ease;
        }

        .center-toggle:hover {
            transform: scale(1.03);
            background: rgba(16, 20, 28, 0.72);
            border-color: rgba(255, 255, 255, 0.34);
        }

        .center-toggle {
            position: absolute;
            left: 50%;
            top: 50%;
            width: 94px;
            height: 94px;
            border-radius: 47px;
            transform: translate(-50%, -50%) scale(0.94);
            opacity: 0;
        }

        #shell.is-paused .center-toggle,
        #shell.show-ui .center-toggle {
            opacity: 1;
            transform: translate(-50%, -50%) scale(1);
        }

        #shell.is-playing:not(.show-ui) .center-toggle {
            pointer-events: none;
        }

        .center-icon,
        .tap-icon {
            width: 24px;
            height: 24px;
            display: none;
        }

        .tap-feedback {
            position: absolute;
            left: 50%;
            top: 50%;
            width: 84px;
            height: 84px;
            border-radius: 42px;
            background: rgba(11, 14, 20, 0.68);
            border: 1px solid rgba(255, 255, 255, 0.2);
            backdrop-filter: blur(18px);
            -webkit-backdrop-filter: blur(18px);
            box-shadow: 0 18px 42px rgba(0, 0, 0, 0.3);
            opacity: 0;
            pointer-events: none;
            transform: translate(-50%, -50%) scale(0.78);
            transition: opacity 160ms ease, transform 160ms ease;
        }

        .tap-feedback.show {
            opacity: 1;
            transform: translate(-50%, -50%) scale(1);
        }

        #shell.is-playing .pause-icon,
        #shell.is-playing .tap-pause-icon,
        #shell.flash-pause .tap-pause-icon,
        #shell.is-paused .play-icon,
        #shell.is-paused .tap-play-icon,
        #shell.flash-play .tap-play-icon {
            display: block;
        }

        #shell.is-playing .play-icon,
        #shell.is-playing .tap-play-icon,
        #shell.is-paused .pause-icon,
        #shell.is-paused .tap-pause-icon,
        #shell.flash-play .tap-pause-icon,
        #shell.flash-pause .tap-play-icon {
            display: none;
        }
    </style>
</head>
<body>
    <div id="shell">
        <video id="player" preload="metadata" playsinline></video>
        <div id="ui">
            <button id="centerToggle" class="center-toggle" type="button" aria-label="Toggle playback">
                <svg class="center-icon play-icon" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M8 6L19 12L8 18V6Z" fill="currentColor"/>
                </svg>
                <svg class="center-icon pause-icon" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M8 6H11V18H8V6ZM13 6H16V18H13V6Z" fill="currentColor"/>
                </svg>
            </button>
            <div id="tapFeedback" class="tap-feedback" aria-hidden="true">
                <svg class="tap-icon tap-play-icon" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M8 6L19 12L8 18V6Z" fill="currentColor"/>
                </svg>
                <svg class="tap-icon tap-pause-icon" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M8 6H11V18H8V6ZM13 6H16V18H13V6Z" fill="currentColor"/>
                </svg>
            </div>
        </div>
    </div>
    <script>
        const shell = document.getElementById('shell');
        const video = document.getElementById('player');
        const centerToggle = document.getElementById('centerToggle');
        const tapFeedback = document.getElementById('tapFeedback');
        // video ayarları (sağ tık, indirme vb. engelle)
        video.controls = false;
        video.disablePictureInPicture = true;
        video.setAttribute('controlsList', 'nodownload noplaybackrate noremoteplayback nofullscreen');
        let chromeTimer = 0;
        let feedbackTimer = 0;

        function post(type, payload) {
            if (window.chrome && chrome.webview) {
                chrome.webview.postMessage(Object.assign({ type: type }, payload || {}));
            }
        }

        function getDuration() {
            return Number.isFinite(video.duration) ? video.duration : 0;
        }

        function sendTimeline() {
            post('timeline', {
                current: Number.isFinite(video.currentTime) ? video.currentTime : 0,
                duration: getDuration()
            });
        }

        function sendPlayback() {
            post('playback', {
                playing: !video.paused && !video.ended
            });
        }

        function showChrome(sticky) {
            shell.classList.add('show-ui');
            clearTimeout(chromeTimer);

            if (!sticky && !video.paused && !video.ended) {
                chromeTimer = window.setTimeout(function () {
                    shell.classList.remove('show-ui');
                }, 1600);
            }
        }

        // video durumuna göre UI'ı güncelle
        function syncSurface() {
            const playing = !video.paused && !video.ended;
            shell.classList.toggle('is-playing', playing);
            shell.classList.toggle('is-paused', !playing);
            showChrome(!playing);
        }

        function flashSurface(mode) {
            shell.classList.remove('flash-play', 'flash-pause');
            tapFeedback.classList.remove('show');
            void tapFeedback.offsetWidth;
            shell.classList.add(mode === 'play' ? 'flash-play' : 'flash-pause');
            tapFeedback.classList.add('show');
            clearTimeout(feedbackTimer);
            feedbackTimer = window.setTimeout(function () {
                tapFeedback.classList.remove('show');
                shell.classList.remove('flash-play', 'flash-pause');
            }, 420);
        }

        function togglePlayback() {
            if (!video.currentSrc) {
                return;
            }

            const shouldPlay = video.paused || video.ended;

            if (shouldPlay) {
                window.trailerPlayer.play();
            } else {
                window.trailerPlayer.pause();
            }

            flashSurface(shouldPlay ? 'play' : 'pause');
            showChrome(true);
        }

        window.trailerPlayer = {
            load: function (url, autoplay, muted, volume) {
                video.pause();
                video.controls = false;
                video.removeAttribute('controls');
                video.removeAttribute('src');
                video.load();
                video.src = url;
                video.currentTime = 0;
                video.muted = !!muted;

                if (typeof volume === 'number' && Number.isFinite(volume)) {
                    video.volume = Math.max(0, Math.min(1, volume));
                }

                sendTimeline();
                syncSurface();

                if (autoplay) {
                    const playPromise = video.play();
                    if (playPromise && typeof playPromise.catch === 'function') {
                        playPromise.catch(function (error) {
                            post('error', { message: String(error) });
                        });
                    }
                }
            },
            play: function () {
                const playPromise = video.play();
                if (playPromise && typeof playPromise.catch === 'function') {
                    playPromise.catch(function (error) {
                        post('error', { message: String(error) });
                    });
                }
            },
            pause: function () {
                video.pause();
            },
            seek: function (seconds) {
                if (!Number.isFinite(seconds)) {
                    return;
                }

                const duration = getDuration();
                video.currentTime = Math.max(0, duration > 0 ? Math.min(duration, seconds) : seconds);
                sendTimeline();
            },
            setVolume: function (value) {
                if (!Number.isFinite(value)) {
                    return;
                }

                video.volume = Math.max(0, Math.min(1, value));
            },
            setMuted: function (value) {
                video.muted = !!value;
                post('muted', { muted: video.muted, volume: video.volume });
            },
            stop: function () {
                video.pause();
                video.removeAttribute('src');
                video.load();
                sendTimeline();
                sendPlayback();
                syncSurface();
            },
            toggle: function () {
                togglePlayback();
            }
        };

        centerToggle.addEventListener('click', function (event) {
            event.stopPropagation();
            togglePlayback();
        });
        shell.addEventListener('mousemove', function () {
            showChrome(false);
        });
        shell.addEventListener('mouseleave', function () {
            if (!video.paused && !video.ended) {
                shell.classList.remove('show-ui');
            }
        });
        video.addEventListener('click', togglePlayback);
        video.addEventListener('loadedmetadata', sendTimeline);
        video.addEventListener('durationchange', sendTimeline);
        video.addEventListener('timeupdate', sendTimeline);
        video.addEventListener('seeking', sendTimeline);
        video.addEventListener('seeked', sendTimeline);
        video.addEventListener('play', function () {
            sendPlayback();
            syncSurface();
        });
        video.addEventListener('pause', function () {
            sendPlayback();
            syncSurface();
        });
        video.addEventListener('ended', function () {
            sendTimeline();
            post('ended');
            sendPlayback();
            syncSurface();
        });
        video.addEventListener('volumechange', function () {
            post('muted', { muted: video.muted, volume: video.volume });
        });
        video.addEventListener('error', function () {
            const message = video.error ? 'MediaError ' + video.error.code : 'Video could not be loaded.';
            post('error', { message: message });
        });

        syncSurface();
        post('ready');
    </script>
</body>
</html>
""";
        private readonly StoreController _storeController;
        private List<StoreCategoryItem> _categories = new();
        private List<StoreCategoryItem> _libraryCategories = new();
        private StoreGameDetail? _currentDetail;
        private Button? _activeSidebarButton;
        private int _currentPage = 1;
        private string _selectedCategory = "Tümü";
        private string _selectedLibraryCategory = "Tümü";
        private bool _isWishlistViewActive;
        private bool _isProfileEditMode;
        private string _pendingAvatarSourcePath = string.Empty;

        // geçici banner yolu
        private string _pendingBannerSourcePath = string.Empty;
        private readonly DispatcherTimer _trailerProgressTimer;
        private readonly DispatcherTimer _trailerOverlayTimer;
        private List<StoreGameCardItem> _storeItems = new();
        private bool _isTrailerSeekActive;
        private bool _isTrailerPlaying;
        private bool _isTrailerProgressUpdating;
        private bool _isTrailerMuted;
        private bool _isTrailerPlayerReady;
        private bool _isTrailerVolumeUpdating;
        private double _trailerCurrentSeconds;
        private double _trailerDurationSeconds;

        private sealed class DetailFeatureItem
        {
            public string Name { get; set; } = string.Empty;

            public string IconData { get; set; } = string.Empty;
        }

        public MainAppWindow()
        {
            InitializeComponent();
            _storeController = new StoreController();
            _downloadQueueTimer.Interval = TimeSpan.FromSeconds(1);
            _downloadQueueTimer.Tick += DownloadQueueTimer_Tick;
            _trailerProgressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _trailerProgressTimer.Tick += TrailerProgressTimer_Tick;
            _trailerOverlayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _trailerOverlayTimer.Tick += TrailerOverlayTimer_Tick;
            _isTrailerMuted = false;
            _trailerCurrentSeconds = 0;
            _trailerDurationSeconds = 0;
            sldTrailerVolume.Value = 0.7;
            sldTrailerProgress.IsEnabled = false;

            // oturum kontrolü
            EnsureSession();
            ConfigureProfileArea();
            UpdateSearchPlaceholder();
            ShowStoreView();
            // pencere ikonunu ayarla
            UpdateWindowGlyph();

            try
            {
                // veritabanı şemalarını kontrol et
                AdminGameSchemaManager.EnsureSchema();
                UserCommerceSchemaManager.EnsureSchema();
                FriendshipSchemaManager.EnsureSchema();

                // ödeme ve indirme durumlarını yükle
                InitializeCommerceState();
                InitializeDownloadState();
                BuildCategories();
                // mağaza sayfasını yükle ve kolonları hizala
                LoadStorePage();
                Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                // veritabanı hatasını yakala
                InitializeStoreFallbackState();
                CustomError.ShowDialog($"Veritabanına bağlanılamadı: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void InitializeStoreFallbackState()
        {
            // veritabanı yoksa boş başlat
            _categories = new List<StoreCategoryItem>
            {
                new StoreCategoryItem { Name = "Tümü", IsSelected = true }
            };
            _libraryCategories = new List<StoreCategoryItem>
            {
                new StoreCategoryItem { Name = "Tümü", IsSelected = true }
            };
            _storeItems = new List<StoreGameCardItem>();
            _libraryItems = new List<LibraryGameItem>();
            _downloadItems = new List<DownloadQueueItem>();
            _walletTransactions = new List<WalletTransactionItem>();
            _wishlistItems = new List<WishlistGameItem>();
            _ownedGameIds = new HashSet<int>();
            _cartGameIds = new HashSet<int>();
            _wishlistGameIds = new HashSet<int>();
            _downloadStates = new Dictionary<int, DownloadStateItem>();

            RefreshCategoryItems();
            RefreshLibraryCategoryItems();
            icStoreGames.ItemsSource = null;
            icStoreGames.ItemsSource = _storeItems;
            EmptyStoreState.Visibility = Visibility.Visible;
            txtStoreResultInfo.Text = "Veritabanı bağlantısı kurulamadı";
            txtPageInfo.Text = "-";
            txtPageInfo.Visibility = Visibility.Collapsed;
            btnPreviousPage.Visibility = Visibility.Collapsed;
            btnNextPage.Visibility = Visibility.Collapsed;
            btnPreviousPage.IsEnabled = false;
            btnNextPage.IsEnabled = false;
            _downloadQueueTimer.Stop();
        }

        private async Task EnsureTrailerPlayerAsync()
        {
            if (_isTrailerPlayerReady)
            {
                return;
            }

            await mediaDetailTrailer.EnsureCoreWebView2Async();

            if (mediaDetailTrailer.CoreWebView2 == null)
            {
                throw new InvalidOperationException("Trailer player could not be initialized.");
            }

            string assetHostRoot = Path.GetFullPath(
                Path.GetDirectoryName(GameAssetManager.GetAssetRoot()) ?? GameAssetManager.GetAssetRoot());

            mediaDetailTrailer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            mediaDetailTrailer.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            mediaDetailTrailer.CoreWebView2.Settings.IsZoomControlEnabled = false;
            mediaDetailTrailer.CoreWebView2.Settings.IsStatusBarEnabled = false;
            mediaDetailTrailer.CoreWebView2.Settings.IsPinchZoomEnabled = false;
            mediaDetailTrailer.CoreWebView2.SetVirtualHostNameToFolderMapping(
                TrailerPlayerHostName,
                assetHostRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            mediaDetailTrailer.CoreWebView2.WebMessageReceived -= TrailerPlayer_WebMessageReceived;
            mediaDetailTrailer.CoreWebView2.WebMessageReceived += TrailerPlayer_WebMessageReceived;
            mediaDetailTrailer.DefaultBackgroundColor = System.Drawing.Color.Black;

            TaskCompletionSource<bool> navigationCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void HandleNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                mediaDetailTrailer.NavigationCompleted -= HandleNavigationCompleted;

                if (args.IsSuccess)
                {
                    navigationCompletion.TrySetResult(true);
                    return;
                }

                navigationCompletion.TrySetException(new InvalidOperationException("Trailer player page could not be loaded."));
            }

            mediaDetailTrailer.NavigationCompleted += HandleNavigationCompleted;
            mediaDetailTrailer.NavigateToString(TrailerPlayerHtml);
            await navigationCompletion.Task;
            _isTrailerPlayerReady = true;
        }

        private async Task ExecuteTrailerScriptAsync(string script)
        {
            if (!_isTrailerPlayerReady || mediaDetailTrailer.CoreWebView2 == null)
            {
                return;
            }

            await mediaDetailTrailer.ExecuteScriptAsync(script);
        }

        private string BuildTrailerPlayerUrl(string absoluteTrailerPath)
        {
            string assetHostRoot = Path.GetFullPath(
                Path.GetDirectoryName(GameAssetManager.GetAssetRoot()) ?? GameAssetManager.GetAssetRoot());
            string relativePath = Path.GetRelativePath(assetHostRoot, absoluteTrailerPath)
                .Replace(Path.DirectorySeparatorChar, '/');

            string encodedPath = Uri.EscapeDataString(relativePath).Replace("%2F", "/");
            return $"https://{TrailerPlayerHostName}/{encodedPath}";
        }

        private void TrailerPlayer_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("type", out JsonElement typeElement))
                {
                    return;
                }

                string messageType = typeElement.GetString() ?? string.Empty;

                if (messageType != "ready" && mediaDetailTrailer.Visibility != Visibility.Visible)
                {
                    return;
                }

                switch (messageType)
                {
                    case "timeline":
                        _trailerCurrentSeconds = GetJsonDouble(root, "current");
                        _trailerDurationSeconds = GetJsonDouble(root, "duration");

                        if (!_isTrailerSeekActive)
                        {
                            UpdateTrailerTimeline();
                        }

                        break;

                    case "playback":
                        _isTrailerPlaying = GetJsonBoolean(root, "playing");
                        txtTrailerPlayPauseGlyph.Text = _isTrailerPlaying ? "\uE769" : "\uE768";
                        DetailTrailerControls.Visibility = Visibility.Visible;
                        break;

                    case "muted":
                        _isTrailerMuted = GetJsonBoolean(root, "muted");

                        if (root.TryGetProperty("volume", out JsonElement volumeElement) &&
                            volumeElement.ValueKind == JsonValueKind.Number)
                        {
                            _isTrailerVolumeUpdating = true;
                            sldTrailerVolume.Value = Math.Max(0, Math.Min(1, volumeElement.GetDouble()));
                            _isTrailerVolumeUpdating = false;
                        }

                        txtTrailerMuteGlyph.Text = _isTrailerMuted || sldTrailerVolume.Value <= 0
                            ? "\uE74F"
                            : "\uE767";
                        break;

                    case "ended":
                        _isTrailerPlaying = false;
                        _trailerCurrentSeconds = 0;
                        UpdateTrailerTimeline();
                        txtTrailerPlayPauseGlyph.Text = "\uE768";
                        DetailTrailerControls.Visibility = Visibility.Visible;
                        break;

                    case "error":
                        string message = root.TryGetProperty("message", out JsonElement messageElement)
                            ? messageElement.GetString() ?? "Fragman videosu açılamadı."
                            : "Fragman videosu açılamadı.";
                        _trailerProgressTimer.Stop();
                        _isTrailerPlaying = false;
                        CustomError.ShowDialog("Fragman videosu açılamadı: " + message, "Sistem Hatası", owner: this);
                        CloseTrailerButton_Click(this, new RoutedEventArgs());
                        break;
                }
            }
            catch
            {
            }
        }

        private static double GetJsonDouble(JsonElement root, string propertyName)
        {
            if (root.TryGetProperty(propertyName, out JsonElement element) &&
                element.ValueKind == JsonValueKind.Number)
            {
                double value = element.GetDouble();

                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    return Math.Max(0, value);
                }
            }

            return 0;
        }

        private static bool GetJsonBoolean(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element))
            {
                return false;
            }

            return element.ValueKind == JsonValueKind.True;
        }

        private void EnsureSession()
        {
            // oturum yoksa misafir olarak başlat
            if (string.IsNullOrWhiteSpace(UserSession.DisplayName))
            {
                UserSession.SetGuest();
            }
        }

        private void ConfigureProfileArea()
        {
            // profil alanı gösterimi
            txtProfileInitial.Text = UserSession.GetAvatarLetter();
            txtCategoryMenuLabel.Text = string.IsNullOrWhiteSpace(_selectedCategory) ? " Tümü" : $" {_selectedCategory}";

            if (UserSession.IsGuest)
            {
                btnProfilePrimaryAction.Content = "Kayıt Ol";
                btnProfileSecondaryAction.Content = "Giriş Yap";
            }
            else
            {
                btnProfilePrimaryAction.Content = "Profili Düzenle";
                btnProfileSecondaryAction.Content = "Çıkış Yap";
            }

            BitmapImage? headerAvatar = GameAssetManager.LoadBitmap(UserSession.ProfileImagePath);

            ApplyProfileImagePreview(imgProfile, txtProfileInitial, headerAvatar, UserSession.GetAvatarLetter());

            RefreshWalletPage();
            RefreshProfilePage();
        }

        private void BuildCategories()
        {
            // kategori listesini doldur
            _categories = new List<StoreCategoryItem>
            {
                new StoreCategoryItem { Name = "Tümü", IsSelected = true }
            };

            _categories.AddRange(
                _storeController.GetCategories().Select(category => new StoreCategoryItem
                {
                    Name = category,
                    IsSelected = false
                }));

            RefreshCategoryItems();
        }

        private void RefreshCategoryItems()
        {
            // kategorileri tazele
            icCategories.ItemsSource = null;
            icCategories.ItemsSource = _categories;
        }

        private void BuildLibraryCategories()
        {
            // kütüphane kategorilerini oluştur
            List<string> categoryNames = _libraryItems
                .Select(item => GameCategoryCatalog.Normalize(item.Category))
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category)
                .ToList();

            if (!string.Equals(_selectedLibraryCategory, "Tümü", StringComparison.OrdinalIgnoreCase) &&
                !categoryNames.Contains(_selectedLibraryCategory, StringComparer.OrdinalIgnoreCase))
            {
                _selectedLibraryCategory = "Tümü";
            }

            _libraryCategories = new List<StoreCategoryItem>
            {
                new StoreCategoryItem
                {
                    Name = "Tümü",
                    IsSelected = string.Equals(_selectedLibraryCategory, "Tümü", StringComparison.OrdinalIgnoreCase)
                }
            };

            _libraryCategories.AddRange(categoryNames.Select(category => new StoreCategoryItem
            {
                Name = category,
                IsSelected = string.Equals(category, _selectedLibraryCategory, StringComparison.OrdinalIgnoreCase)
            }));

            RefreshLibraryCategoryItems();
        }

        private void RefreshLibraryCategoryItems()
        {
            // kutuphane chiplerini yenile
            icLibraryCategories.ItemsSource = null;
            icLibraryCategories.ItemsSource = _libraryCategories;
        }

        private List<LibraryGameItem> GetVisibleLibraryItems()
        {
            // secili ture gore kutuphane kartlarini filtrele
            if (string.Equals(_selectedLibraryCategory, "Tümü", StringComparison.OrdinalIgnoreCase))
            {
                return _libraryItems;
            }

            return _libraryItems
                .Where(item => string.Equals(
                    GameCategoryCatalog.Normalize(item.Category),
                    _selectedLibraryCategory,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void LoadStorePage()
        {
            // magazadaki oyunlari sayfalama ile getir
            StoreGamePageResult result = _storeController.GetGames(txtSearch.Text, _selectedCategory, _currentPage);
            _currentPage = result.CurrentPage;

            _storeItems = result.Items.ToList();
            ApplyStoreOwnershipState();
            icStoreGames.ItemsSource = null;
            icStoreGames.ItemsSource = _storeItems;
            EmptyStoreState.Visibility = result.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateStoreGridColumns();

            txtStoreResultInfo.Text = result.TotalCount == 1
                ? "1 oyun mağazada listeleniyor"
                : $"{result.TotalCount} oyun mağazada listeleniyor";

            txtPageInfo.Text = $"{result.CurrentPage} / {result.TotalPages}";
            btnPreviousPage.IsEnabled = result.CurrentPage > 1;
            btnNextPage.IsEnabled = result.CurrentPage < result.TotalPages;
            btnPreviousPage.Visibility = result.TotalPages > 1 ? Visibility.Visible : Visibility.Collapsed;
            btnNextPage.Visibility = result.TotalPages > 1 ? Visibility.Visible : Visibility.Collapsed;
            txtPageInfo.Visibility = result.TotalPages > 1 ? Visibility.Visible : Visibility.Collapsed;

            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
        }

        private void ShowStoreView()
        {
            // ana magazaya don
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Visible;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;
            SetSidebarSelection(btnStoreNav);
            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
        }

        private void UpdateStoreGridColumns()
        {
            // kartlari soldan akit
            double viewportWidth = StoreGamesScrollViewer.ViewportWidth;

            if (viewportWidth <= 0)
            {
                viewportWidth = StoreGamesScrollViewer.ActualWidth;
            }

            if (viewportWidth <= 0)
            {
                return;
            }

            if (_storeItems.Count == 0)
            {
                icStoreGames.Width = double.NaN;
                return;
            }

            icStoreGames.Width = Math.Max(0, viewportWidth - 4);
        }

        private void ShowDetailView(StoreGameDetail detail)
        {
            // secilen oyunun detaylarini goster
            detail.MediaItems = detail.MediaItems
                .OrderByDescending(item => item.IsTrailer)
                .ToList();

            _currentDetail = detail;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;

            txtDetailCategory.Text = detail.Category;
            txtDetailTitle.Text = detail.Title;
            txtDetailPrice.Text = detail.PriceText;
            imgDetailCover.Source = detail.CoverPreview;
            txtDetailDeveloper.Text = DisplayOrFallback(detail.Developer);
            txtDetailPublisher.Text = DisplayOrFallback(detail.Publisher);
            txtDetailReleaseDate.Text = DisplayOrFallback(detail.ReleaseDateText);
            txtDetailCategoryValue.Text = detail.Category;
            txtDetailDescription.Text = DisplayOrFallback(detail.Description);
            txtMinimumRequirements.Text = DisplayOrFallback(detail.MinimumRequirements);
            txtRecommendedRequirements.Text = DisplayOrFallback(detail.RecommendedRequirements);
            txtSupportedLanguages.Text = DisplayOrFallback(detail.SupportedLanguages);
            icDetailPlatforms.ItemsSource = detail.Platforms.Count > 0 ? detail.Platforms : new List<string> { "Belirtilmedi" };
            icDetailFeatures.ItemsSource = BuildDetailFeatureItems(detail.Features);

            RefreshDetailMediaStrip();
            btnDetailMediaPrevious.Visibility = detail.MediaItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            btnDetailMediaNext.Visibility = detail.MediaItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

            if (detail.MediaItems.Count > 0)
            {
                SetSelectedMedia(detail.MediaItems[0].Name);
            }
            else
            {
                imgDetailHero.Source = detail.CoverPreview;
                imgDetailHero.Visibility = Visibility.Visible;
                btnDetailPlayTrailer.Visibility = Visibility.Collapsed;
                btnDetailCloseTrailer.Visibility = Visibility.Collapsed;
                mediaDetailTrailer.Visibility = Visibility.Collapsed;
                DetailMediaStripScrollViewer.ScrollToHorizontalOffset(0);
            }

            StoreHeaderPanel.Visibility = Visibility.Collapsed;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Visible;
            DetailContentPanel.Visibility = Visibility.Visible;
            InstallContentPanel.Visibility = Visibility.Collapsed;
            SetSidebarSelection(_isLibraryViewActive ? btnLibraryNav : btnStoreNav);
            ApplyDetailOwnershipState();
            ScrollSelectedMediaIntoView();
        }

        private List<DetailFeatureItem> BuildDetailFeatureItems(IEnumerable<string>? values)
        {
            // ozellik satirlarini ikonla zenginlestir
            List<string> featureNames = values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList()
                ?? new List<string>();

            if (featureNames.Count == 0)
            {
                featureNames.Add("Belirtilmedi");
            }

            return featureNames
                .Select(value => new DetailFeatureItem
                {
                    Name = value,
                    IconData = GetFeatureIconData(value)
                })
                .ToList();
        }

        private string GetFeatureIconData(string featureName)
        {
            // ozellige uygun sekli sec
            return featureName.Trim().ToLowerInvariant() switch
            {
                "tek oyunculu" => "M8,4.2A2.2,2.2 0 1 1 7.99,4.2 M4.8,12.5C5.32,10.73 6.44,9.8 8,9.8C9.56,9.8 10.68,10.73 11.2,12.5",
                "çok oyunculu" => "M5.2,4.8A1.8,1.8 0 1 1 5.19,4.8 M10.8,4.8A1.8,1.8 0 1 1 10.79,4.8 M2.8,12.4C3.25,10.97 4.15,10.2 5.4,10.2 M8.6,10.2C9.85,10.2 10.75,10.97 11.2,12.4 M5.9,12.4C6.32,11.16 7.11,10.5 8,10.5C8.89,10.5 9.68,11.16 10.1,12.4",
                "eşli oyun" => "M5.2,10.8L3.6,12.4C2.83,13.17 1.57,13.17 0.8,12.4C0.03,11.63 0.03,10.37 0.8,9.6L3.6,6.8C4.37,6.03 5.63,6.03 6.4,6.8L7.2,7.6 M10.8,5.2L12.4,3.6C13.17,2.83 14.43,2.83 15.2,3.6C15.97,4.37 15.97,5.63 15.2,6.4L12.4,9.2C11.63,9.97 10.37,9.97 9.6,9.2L8.8,8.4",
                "çevrim içi eşli oyun" => "M8,13.5A5.5,5.5 0 1 1 8,2.5 M2.8,8H13.2 M8,2.7C9.43,4.1 10.2,5.97 10.2,8C10.2,10.03 9.43,11.9 8,13.3C6.57,11.9 5.8,10.03 5.8,8C5.8,5.97 6.57,4.1 8,2.7",
                "pvp" => "M4.5,4.5L11.5,11.5 M11.5,4.5L4.5,11.5 M3.6,5.4L4.5,4.5L5.4,3.6 M10.6,12.4L11.5,11.5L12.4,10.6 M10.6,3.6L11.5,4.5L12.4,5.4 M3.6,10.6L4.5,11.5L5.4,12.4",
                "denetleyici desteği" => "M4.5,6.5H11.5C12.88,6.5 14,7.62 14,9C14,10.38 12.88,11.5 11.5,11.5H10.2L9,10.3H7L5.8,11.5H4.5C3.12,11.5 2,10.38 2,9C2,7.62 3.12,6.5 4.5,6.5 M4.9,9H6.8 M5.85,8.05V9.95 M10.6,8.2H10.61 M12,9.3H12.01",
                "çapraz platform" => "M3,5H8V2.5L12.5,7L8,11.5V9H3 M13,11H8V13.5L3.5,9L8,4.5V7H13",
                "bulut kayıtları" => "M5,12.5H11.2C12.75,12.5 14,11.25 14,9.7C14,8.28 12.94,7.1 11.56,6.92C11.14,5.16 9.56,3.9 7.7,3.9C5.51,3.9 3.73,5.68 3.73,7.87C2.72,8.3 2,9.3 2,10.48C2,11.6 2.9,12.5 4.02,12.5H7.7 M8,8.2V12.4 M6.6,11L8,12.4L9.4,11",
                _ => "M8,2.6A5.4,5.4 0 1 1 7.99,2.6 M8,6.4V8.3 M8,11.2H8.01"
            };
        }

        private void SetSelectedMedia(string mediaName)
        {
            // detay ekranindaki secili gorseli guncelle
            if (_currentDetail == null)
            {
                return;
            }

            foreach (StoreMediaItem item in _currentDetail.MediaItems)
            {
                item.IsSelected = string.Equals(item.Name, mediaName, StringComparison.OrdinalIgnoreCase);
            }

            StoreMediaItem? selectedItem = _currentDetail.MediaItems.FirstOrDefault(item => item.IsSelected)
                ?? _currentDetail.MediaItems.FirstOrDefault();

            if (selectedItem == null)
            {
                return;
            }

            if (selectedItem.IsTrailer)
            {
                StopTrailer();
                imgDetailHero.Visibility = Visibility.Visible;
                imgDetailHero.Source = selectedItem.Preview ?? _currentDetail.CoverPreview;
                btnDetailPlayTrailer.Visibility = Visibility.Visible;
                btnDetailCloseTrailer.Visibility = Visibility.Collapsed;
            }
            else
            {
                StopTrailer();
                imgDetailHero.Visibility = Visibility.Visible;
                imgDetailHero.Source = selectedItem.Preview;
                btnDetailPlayTrailer.Visibility = Visibility.Collapsed;
                btnDetailCloseTrailer.Visibility = Visibility.Collapsed;
            }

            RefreshDetailMediaStrip();
        }

        private void RefreshDetailMediaStrip()
        {
            // medya seridini tazele
            icDetailMedia.ItemsSource = null;
            icDetailMedia.ItemsSource = _currentDetail?.MediaItems;
            ScrollSelectedMediaIntoView();
        }

        private void ScrollSelectedMediaIntoView()
        {
            // secili ogeyi seritte gorunur tut
            if (_currentDetail == null)
            {
                return;
            }

            int selectedIndex = _currentDetail.MediaItems.FindIndex(item => item.IsSelected);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                DetailMediaStripScrollViewer.UpdateLayout();

                double viewportWidth = DetailMediaStripScrollViewer.ViewportWidth;
                if (viewportWidth <= 0)
                {
                    return;
                }

                double itemSpan = DetailMediaThumbnailWidth + DetailMediaThumbnailSpacing;
                double targetOffset = (selectedIndex * itemSpan) - ((viewportWidth - DetailMediaThumbnailWidth) / 2);
                double maxOffset = Math.Max(0, DetailMediaStripScrollViewer.ExtentWidth - viewportWidth);

                if (targetOffset < 0)
                {
                    targetOffset = 0;
                }

                if (targetOffset > maxOffset)
                {
                    targetOffset = maxOffset;
                }

                DetailMediaStripScrollViewer.ScrollToHorizontalOffset(targetOffset);
            }), DispatcherPriority.Loaded);
        }

        private void StopTrailer()
        {
            // medya alanini tekrar onizleme haline dondur
            _trailerProgressTimer.Stop();
            _trailerOverlayTimer.Stop();
            _isTrailerPlaying = false;
            _isTrailerSeekActive = false;
            _isTrailerProgressUpdating = false;
            _trailerCurrentSeconds = 0;
            _trailerDurationSeconds = 0;
            btnDetailPlayTrailer.Visibility = Visibility.Collapsed;
            btnDetailCloseTrailer.Visibility = Visibility.Collapsed;
            DetailTrailerControls.Visibility = Visibility.Collapsed;
            btnDetailMediaPrevious.Visibility = _currentDetail != null && _currentDetail.MediaItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            btnDetailMediaNext.Visibility = _currentDetail != null && _currentDetail.MediaItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

            if (_isTrailerPlayerReady)
            {
                _ = ExecuteTrailerScriptAsync("window.trailerPlayer.stop();");
            }

            mediaDetailTrailer.Visibility = Visibility.Collapsed;
            UpdateTrailerTimeline();
            txtTrailerPlayPauseGlyph.Text = "\uE768";
            txtTrailerMuteGlyph.Text = _isTrailerMuted || sldTrailerVolume.Value <= 0 ? "\uE74F" : "\uE767";
            sldTrailerProgress.IsEnabled = false;
        }

        private void SelectRelativeDetailMedia(int step)
        {
            // secili medyayi saga sola kaydir
            if (_currentDetail == null || _currentDetail.MediaItems.Count == 0)
            {
                return;
            }

            int currentIndex = _currentDetail.MediaItems.FindIndex(item => item.IsSelected);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = currentIndex + step;
            if (nextIndex < 0)
            {
                nextIndex = _currentDetail.MediaItems.Count - 1;
            }
            else if (nextIndex >= _currentDetail.MediaItems.Count)
            {
                nextIndex = 0;
            }

            SetSelectedMedia(_currentDetail.MediaItems[nextIndex].Name);
        }

        private void SetSidebarSelection(Button selectedButton)
        {
            // aktif menuyu sade sekilde vurgula
            _activeSidebarButton = selectedButton;
            ResetSidebarButton(btnStoreNav);
            ResetSidebarButton(btnLibraryNav);
            ResetSidebarButton(btnDownloadsNav);

            selectedButton.Background = System.Windows.Media.Brushes.White;
            selectedButton.Foreground = System.Windows.Media.Brushes.Black;
        }

        private void ClearSidebarSelection()
        {
            // sidebar secimi yoksa tumunu varsayilana cek
            _activeSidebarButton = null;
            ResetSidebarButton(btnStoreNav);
            ResetSidebarButton(btnLibraryNav);
            ResetSidebarButton(btnDownloadsNav);
        }

        private void ResetSidebarButton(Button button)
        {
            button.Background = System.Windows.Media.Brushes.Transparent;
            button.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C8CDD5"));
        }

        private void SidebarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // secili olmayan menude hafif bir yuzey goster
            if (sender is not Button button || button == _activeSidebarButton)
            {
                return;
            }

            button.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#121216"));
        }

        private void SidebarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // secili olmayan menuyu eski gorunume dondur
            if (sender is not Button button || button == _activeSidebarButton)
            {
                return;
            }

            button.Background = System.Windows.Media.Brushes.Transparent;
        }

        private void StoreCard_MouseEnter(object sender, MouseEventArgs e)
        {
            // kart ustunde sadece kapagi belirginlestir
            if (sender is not Button button)
            {
                return;
            }

            Border? cardGlow = FindDescendantByName<Border>(button, "CardGlow");
            Border? cardSurface = FindDescendantByName<Border>(button, "CardSurface");
            Border? cardHoverOverlay = FindDescendantByName<Border>(button, "CardHoverOverlay");
            Border? cardHoverFrame = FindDescendantByName<Border>(button, "CardHoverFrame");
            Image? cardCoverImage = FindDescendantByName<Image>(button, "CardCoverImage");

            if (cardGlow != null)
            {
                cardGlow.Opacity = 0.35;
            }

            if (cardHoverOverlay != null)
            {
                cardHoverOverlay.Opacity = 0.55;
            }

            if (cardHoverFrame != null)
            {
                cardHoverFrame.Opacity = 0;
                cardHoverFrame.BorderThickness = new Thickness(1);
            }

            if (cardCoverImage != null)
            {
                cardCoverImage.RenderTransform = new ScaleTransform(1.025, 1.025);
                cardCoverImage.Opacity = 0.96;
            }
        }

        private void StoreCard_MouseLeave(object sender, MouseEventArgs e)
        {
            // kart gorselini normal haline dondur
            if (sender is not Button button)
            {
                return;
            }

            Border? cardGlow = FindDescendantByName<Border>(button, "CardGlow");
            Border? cardSurface = FindDescendantByName<Border>(button, "CardSurface");
            Border? cardHoverOverlay = FindDescendantByName<Border>(button, "CardHoverOverlay");
            Border? cardHoverFrame = FindDescendantByName<Border>(button, "CardHoverFrame");
            Image? cardCoverImage = FindDescendantByName<Image>(button, "CardCoverImage");

            if (cardGlow != null)
            {
                cardGlow.Opacity = 0;
            }

            if (cardHoverOverlay != null)
            {
                cardHoverOverlay.Opacity = 0;
            }

            if (cardHoverFrame != null)
            {
                cardHoverFrame.Opacity = 0;
                cardHoverFrame.BorderThickness = new Thickness(1);
            }

            if (cardCoverImage != null)
            {
                cardCoverImage.RenderTransform = new ScaleTransform(1, 1);
                cardCoverImage.Opacity = 1;
            }
        }

        private T? FindDescendantByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            // gorsel agacta isimle kontrol ara
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T frameworkElement && frameworkElement.Name == name)
                {
                    return frameworkElement;
                }

                T? descendant = FindDescendantByName<T>(child, name);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private string DisplayOrFallback(string value)
        {
            // bos alanlar icin okunur yedek metin ver
            return string.IsNullOrWhiteSpace(value) ? "Belirtilmedi" : value.Trim();
        }

        private void UpdateSearchPlaceholder()
        {
            // arama kutusu bosken yardimci metni goster
            txtSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(txtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // baslik alanindan pencereyi tasir
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
            // uygulamayi kapat
            Application.Current.Shutdown();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // buyut kucult ikonunu yenile
            UpdateWindowGlyph();
            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
        }

        private void StoreGamesScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // magazadaki satirlari yeniden dengele
            UpdateStoreGridColumns();
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void UpdateWindowGlyph()
        {
            txtToggleWindowGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // arama sonucunu ilk sayfadan tekrar getir
            UpdateSearchPlaceholder();
            _currentPage = 1;
            LoadStorePage();
        }

        private void CategoryChip_Click(object sender, RoutedEventArgs e)
        {
            // secilen kategoriye gore listeyi yenile
            if (sender is not Button button || button.Tag is not string categoryName)
            {
                return;
            }

            _selectedCategory = categoryName;

            foreach (StoreCategoryItem category in _categories)
            {
                category.IsSelected = string.Equals(category.Name, categoryName, StringComparison.OrdinalIgnoreCase);
            }

            _currentPage = 1;
            RefreshCategoryItems();
            txtCategoryMenuLabel.Text = $" {_selectedCategory}";
            popCategoryMenu.IsOpen = false;
            LoadStorePage();
        }

        private void LibraryCategoryChip_Click(object sender, RoutedEventArgs e)
        {
            // secilen ture gore kutuphaneyi filtrele
            if (sender is not Button button || button.Tag is not string categoryName)
            {
                return;
            }

            _selectedLibraryCategory = categoryName;

            foreach (StoreCategoryItem category in _libraryCategories)
            {
                category.IsSelected = string.Equals(category.Name, categoryName, StringComparison.OrdinalIgnoreCase);
            }

            RefreshLibraryCategoryItems();
            RefreshLibraryPanel();
        }

        private void CategoryMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // kategori menusunu ac kapa
            popCategoryMenu.IsOpen = !popCategoryMenu.IsOpen;
        }

        private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            // onceki sayfaya git
            if (_currentPage <= 1)
            {
                return;
            }

            _currentPage--;
            LoadStorePage();
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            // sonraki sayfaya git
            _currentPage++;
            LoadStorePage();
        }

        private void StoreGameButton_Click(object sender, RoutedEventArgs e)
        {
            // secilen kartin detayini ac
            if (sender is not Button button || button.Tag is not int gameId)
            {
                return;
            }

            try
            {
                StoreGameDetail detail = _storeController.GetGameDetail(gameId);
                if (_isLibraryViewActive || _isDownloadsViewActive)
                {
                    ShowInstallView(detail);
                }
                else
                {
                    ShowDetailView(detail);
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Oyun sayfası açılamadı: " + ex.Message, "Sistem Hatası", owner: this);
            }
        }

        private void DetailMediaButton_Click(object sender, RoutedEventArgs e)
        {
            // secilen medya gorselini ana alana tasir
            if (sender is not Button button || button.Tag is not string mediaName)
            {
                return;
            }

            SetSelectedMedia(mediaName);
        }

        private void DetailMediaPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // onceki medya ogesine gec
            SelectRelativeDetailMedia(-1);
        }

        private void DetailMediaNextButton_Click(object sender, RoutedEventArgs e)
        {
            // sonraki medya ogesine gec
            SelectRelativeDetailMedia(1);
        }

        private void BackToStoreButton_Click(object sender, RoutedEventArgs e)
        {
            // detaydan onceki listeye don
            StopTrailer();

            if (_isInstallViewActive)
            {
                if (_isDownloadsViewActive)
                {
                    ShowDownloadsView();
                    return;
                }

                if (_isLibraryViewActive)
                {
                    ShowLibraryView();
                    return;
                }

                ShowStoreView();
                return;
            }

            if (_isLibraryViewActive)
            {
                ShowLibraryView();
                return;
            }

            ShowStoreView();
        }

        private async void PlayTrailerButton_Click(object sender, RoutedEventArgs e)
        {
            // secili fragman videosunu uygulama icinde oynat
            if (_currentDetail == null)
            {
                return;
            }

            try
            {
                string absoluteTrailerPath = ResolvePlayableTrailerPath();

                if (string.IsNullOrWhiteSpace(absoluteTrailerPath) || !File.Exists(absoluteTrailerPath))
                {
                CustomError.ShowDialog("Fragman videosu bulunamadı.", "Sistem Hatası", owner: this);
                    return;
                }

                await EnsureTrailerPlayerAsync();

                string trailerPlayerUrl = BuildTrailerPlayerUrl(absoluteTrailerPath);
                double volume = Math.Max(0, Math.Min(1, sldTrailerVolume.Value));

                _trailerCurrentSeconds = 0;
                _trailerDurationSeconds = 0;
                _isTrailerMuted = volume <= 0;
                mediaDetailTrailer.Visibility = Visibility.Visible;
                _isTrailerSeekActive = false;
                imgDetailHero.Visibility = Visibility.Collapsed;
                btnDetailPlayTrailer.Visibility = Visibility.Collapsed;
                btnDetailCloseTrailer.Visibility = Visibility.Visible;
                DetailTrailerControls.Visibility = Visibility.Visible;
                btnDetailMediaPrevious.Visibility = Visibility.Collapsed;
                btnDetailMediaNext.Visibility = Visibility.Collapsed;
                UpdateTrailerTimeline();
                txtTrailerPlayPauseGlyph.Text = "\uE769";
                txtTrailerMuteGlyph.Text = _isTrailerMuted || volume == 0
                    ? "\uE74F"
                    : "\uE767";
                sldTrailerProgress.IsEnabled = false;
                _isTrailerPlaying = true;
                await ExecuteTrailerScriptAsync(
                    $"window.trailerPlayer.load({JsonSerializer.Serialize(trailerPlayerUrl)}, true, {JsonSerializer.Serialize(_isTrailerMuted)}, {JsonSerializer.Serialize(volume)});");
                RestartTrailerOverlayTimer();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Fragman videosu açılamadı: " + ex.Message, "Sistem Hatası", owner: this);
            }
        }

        private void CloseTrailerButton_Click(object sender, RoutedEventArgs e)
        {
            // video alanini kapatip secili onizlemeye don
            StopTrailer();

            if (_currentDetail == null)
            {
                return;
            }

            StoreMediaItem? selectedItem = _currentDetail.MediaItems.FirstOrDefault(item => item.IsSelected);
            if (selectedItem != null && selectedItem.IsTrailer)
            {
                imgDetailHero.Visibility = Visibility.Visible;
                imgDetailHero.Source = selectedItem.Preview ?? _currentDetail.CoverPreview;
                btnDetailPlayTrailer.Visibility = Visibility.Visible;
            }
        }

        private string ResolvePlayableTrailerPath()
        {
            // oynatilacak fragman yolunu guvenli sekilde bul
            if (_currentDetail == null)
            {
                return string.Empty;
            }

            List<string> candidatePaths = new();

            StoreMediaItem? selectedItem = _currentDetail.MediaItems.FirstOrDefault(item => item.IsSelected && item.IsTrailer);
            if (selectedItem != null && !string.IsNullOrWhiteSpace(selectedItem.MediaUrl))
            {
                candidatePaths.Add(selectedItem.MediaUrl);
            }

            if (!string.IsNullOrWhiteSpace(_currentDetail.TrailerVideoPath))
            {
                candidatePaths.Add(_currentDetail.TrailerVideoPath);
            }

            string detectedTrailerPath = GameAssetManager.GetTrailerVideoPath(_currentDetail.GameId, false);
            if (!string.IsNullOrWhiteSpace(detectedTrailerPath))
            {
                candidatePaths.Add(detectedTrailerPath);
            }

            foreach (string candidatePath in candidatePaths
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string absolutePath = Path.IsPathRooted(candidatePath)
                    ? candidatePath
                    : GameAssetManager.GetAbsoluteAssetPath(candidatePath);

                if (!string.IsNullOrWhiteSpace(absolutePath) && File.Exists(absolutePath))
                {
                    return absolutePath;
                }
            }

            return string.Empty;
        }

        private void MediaDetailTrailer_MediaOpened(object sender, RoutedEventArgs e)
        {
            // web tabanli oynatici akisi bu eventi kullanmaz
        }

        private void MediaDetailTrailer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // web tabanli oynatici akisi bu eventi kullanmaz
        }

        private void MediaDetailTrailer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // web tabanli oynatici akisi bu eventi kullanmaz
        }

        private void TrailerProgressTimer_Tick(object? sender, EventArgs e)
        {
            // zaman bilgisi web oynaticidan olay tabanli gelir
            _trailerProgressTimer.Stop();
        }

        private void TrailerPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            // videoyu durdur veya yeniden baslat
            if (mediaDetailTrailer.Visibility != Visibility.Visible)
            {
                return;
            }

            if (!_isTrailerPlaying)
            {
                _ = ExecuteTrailerScriptAsync("window.trailerPlayer.play();");
                _isTrailerPlaying = true;
                txtTrailerPlayPauseGlyph.Text = "\uE769";
                RestartTrailerOverlayTimer();
                return;
            }

            _ = ExecuteTrailerScriptAsync("window.trailerPlayer.pause();");
            _isTrailerPlaying = false;
            txtTrailerPlayPauseGlyph.Text = "\uE768";
            _trailerOverlayTimer.Stop();
            DetailTrailerControls.Visibility = Visibility.Visible;
        }

        private void TrailerMuteButton_Click(object sender, RoutedEventArgs e)
        {
            // sesi ac veya kapat
            if (_isTrailerMuted && sldTrailerVolume.Value <= 0)
            {
                _isTrailerVolumeUpdating = true;
                sldTrailerVolume.Value = 0.7;
                _isTrailerVolumeUpdating = false;
            }

            _isTrailerMuted = !_isTrailerMuted;
            txtTrailerMuteGlyph.Text = _isTrailerMuted ? "\uE74F" : "\uE767";
            _ = ExecuteTrailerScriptAsync(
                $"window.trailerPlayer.setVolume({JsonSerializer.Serialize(Math.Max(0, Math.Min(1, sldTrailerVolume.Value)))});");
            _ = ExecuteTrailerScriptAsync($"window.trailerPlayer.setMuted({JsonSerializer.Serialize(_isTrailerMuted)});");
            RestartTrailerOverlayTimer();
        }

        private void TrailerVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // ses seviyesini kaydiricidan guncelle
            if (_isTrailerVolumeUpdating)
            {
                return;
            }

            double volume = Math.Max(0, Math.Min(1, sldTrailerVolume.Value));
            _isTrailerMuted = volume <= 0;

            txtTrailerMuteGlyph.Text = _isTrailerMuted || volume == 0
                ? "\uE74F"
                : "\uE767";
            _ = ExecuteTrailerScriptAsync($"window.trailerPlayer.setVolume({JsonSerializer.Serialize(volume)});");
            _ = ExecuteTrailerScriptAsync($"window.trailerPlayer.setMuted({JsonSerializer.Serialize(_isTrailerMuted)});");
            RestartTrailerOverlayTimer();
        }

        private void TrailerProgress_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // sure cubugu tutulurken otomatik guncellemeyi beklet
            _isTrailerSeekActive = true;
            DetailTrailerControls.Visibility = Visibility.Visible;
            _trailerOverlayTimer.Stop();
        }

        private void TrailerProgress_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // birakilan noktaya videoyu tasir
            ApplyTrailerSeek();
            _isTrailerSeekActive = false;
            RestartTrailerOverlayTimer();
        }

        private void TrailerProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // cubuga tiklandiginda da hedef sureye git
            if (_isTrailerProgressUpdating)
            {
                return;
            }

            if (!_isTrailerSeekActive && Mouse.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            ApplyTrailerSeek();
        }

        private void DetailMediaArea_MouseMove(object sender, MouseEventArgs e)
        {
            // fare hareket edince kontrol cubugunu yeniden goster
            if (!_isTrailerPlaying || mediaDetailTrailer.Visibility != Visibility.Visible)
            {
                return;
            }

            DetailTrailerControls.Visibility = Visibility.Visible;
            RestartTrailerOverlayTimer();
        }

        private void TrailerOverlayTimer_Tick(object? sender, EventArgs e)
        {
            // webview uzerinde gizlenen kontroller geri gosterilemedigi icin sabit acik tut
            _trailerOverlayTimer.Stop();
        }

        private void RestartTrailerOverlayTimer()
        {
            // webview uzerinde kontrolleri gorunur tut
            if (mediaDetailTrailer.Visibility != Visibility.Visible)
            {
                return;
            }

            DetailTrailerControls.Visibility = Visibility.Visible;
            _trailerOverlayTimer.Stop();
        }

        private void ApplyTrailerSeek()
        {
            // secilen sureye videoyu tasir
            if (_trailerDurationSeconds <= 0)
            {
                return;
            }

            double targetSeconds = Math.Max(0, Math.Min(sldTrailerProgress.Maximum, sldTrailerProgress.Value));
            _trailerCurrentSeconds = targetSeconds;
            _ = ExecuteTrailerScriptAsync($"window.trailerPlayer.seek({JsonSerializer.Serialize(targetSeconds)});");
            UpdateTrailerTimeline();
        }

        private void UpdateTrailerTimeline()
        {
            // zaman bilgisini ve ilerleme cubugunu esitle
            TimeSpan totalTime = TimeSpan.FromSeconds(Math.Max(0, _trailerDurationSeconds));
            TimeSpan currentTime = TimeSpan.FromSeconds(Math.Max(0, _trailerCurrentSeconds));

            _isTrailerProgressUpdating = true;
            sldTrailerProgress.Maximum = Math.Max(1, totalTime.TotalSeconds > 0 ? totalTime.TotalSeconds : 1);
            sldTrailerProgress.Value = Math.Min(sldTrailerProgress.Maximum, Math.Max(0, currentTime.TotalSeconds));
            sldTrailerProgress.IsEnabled = totalTime > TimeSpan.Zero;
            _isTrailerProgressUpdating = false;

            txtTrailerTime.Text = $"{FormatMediaTime(currentTime)} / {FormatMediaTime(totalTime)}";
        }

        private string FormatMediaTime(TimeSpan value)
        {
            // sure bilgisini okunur yaz
            return value.TotalHours >= 1
                ? value.ToString(@"hh\:mm\:ss")
                : value.ToString(@"mm\:ss");
        }

        private void Logo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // logoya tıklandığında mağazaya dön
            ShowStoreView();
        }

        private void StoreNavButton_Click(object sender, RoutedEventArgs e)
        {
            // magazayi tekrar goster
            ShowStoreView();
        }

        private void LibraryNavButton_Click(object sender, RoutedEventArgs e)
        {
            // kutuphane ekranina gec
            ShowLibraryView();
        }

        private void DownloadsNavButton_Click(object sender, RoutedEventArgs e)
        {
            // indirmeler ekranina gec
            ShowDownloadsView();
        }

        // arkadaslar popup'ini ac/kapat
        private void FriendsButton_Click(object sender, RoutedEventArgs e)
        {
            // misafire izin yok
            if (UserSession.IsGuest)
            {
                CustomError.ShowDialog("Arkadaş özelliklerini kullanmak için giriş yapın.", "Bilgi", owner: this);
                return;
            }

            // aciksa kapat
            if (popFriendsMenu.IsOpen)
            {
                popFriendsMenu.IsOpen = false;
                return;
            }

            // veriyi yenile, popup'i ac
            RefreshFriendsPopup();
            popFriendsMenu.IsOpen = true;

            // arama kutusuna focus
            _ = Dispatcher.BeginInvoke(new Action(() => txtFriendsQuickSearch.Focus()), DispatcherPriority.Input);
        }

        private void HeaderShellButton_Click(object sender, RoutedEventArgs e)
        {
            // istek listesi tam sayfaya acilsin
            if (sender == btnWishlistHeader)
            {
                if (UserSession.IsGuest)
                {
                    CustomError.ShowDialog("İstek listesi için giriş yapmanız gerekiyor.", "Bilgi", owner: this);
                    return;
                }

                ShowWishlistView();
                return;
            }

            // ust menudeki alanlar simdilik yonlendirme amacli kalir
            CustomError.ShowDialog("Bu bölüm henüz hazır değil.", "Bilgi", owner: this);
        }

        private void ProfileMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // profil menusu ac kapa
            popProfileMenu.IsOpen = !popProfileMenu.IsOpen;
        }

        private void ProfilePrimaryAction_Click(object sender, RoutedEventArgs e)
        {
            // misafir ve oturum acik durumlarina gore ilk menuyu yonet
            popProfileMenu.IsOpen = false;

            if (UserSession.IsGuest)
            {
                Login loginWindow = new Login
                {
                    Left = Left,
                    Top = Top,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };

                loginWindow.Show();
                Close();
                return;
            }

            ShowProfileView();
        }

        private void ProfileSecondaryAction_Click(object sender, RoutedEventArgs e)
        {
            // misafir ve oturum acik durumlarina gore ikinci menuyu yonet
            popProfileMenu.IsOpen = false;

            if (UserSession.IsGuest)
            {
                Login loginWindow = new Login
                {
                    Left = Left,
                    Top = Top,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };

                loginWindow.Show();
                Close();
                return;
            }

            if (!CustomConfirm.ShowDialog("Çıkış Yap", "Oturumu kapatmak istiyor musun?", "Çıkış Yap", this))
            {
                return;
            }

            UserSession.Clear();
            Login nextLoginWindow = new Login
            {
                Left = Left,
                Top = Top,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            nextLoginWindow.Show();
            Close();
        }

        private void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            // detaydaki oyunu sepete ekle
            if (_currentDetail == null)
            {
                return;
            }

            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            if (_currentDetail.IsOwned)
            {
                _isLibraryViewActive = true;
                _isDownloadsViewActive = false;
                ShowInstallView(_currentDetail);
                return;
            }

            try
            {
                _commerceController.AddToCart(UserSession.UserId, _currentDetail.GameId);
                RefreshCommerceState(false);
                ApplyDetailOwnershipState();
                CustomError.ShowDialog("Oyun sepete eklendi.", "Bilgi", owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void AddToWishlistButton_Click(object sender, RoutedEventArgs e)
        {
            // detay ekranindaki oyunu istek listesine ekle cikar
            if (_currentDetail == null)
            {
                return;
            }

            if (UserSession.IsGuest)
            {
                CustomError.ShowDialog("İstek listesi için giriş yapmanız gerekiyor.", "Bilgi", owner: this);
                return;
            }

            try
            {
                bool isInWishlist = _wishlistController.ToggleWishlist(UserSession.UserId, _currentDetail.GameId);
                RefreshCommerceState(false);
                ApplyDetailOwnershipState();

                if (_isWishlistViewActive)
                {
                    RefreshWishlistPage();
                }

                if (_currentDetail.IsOwned)
                {
                    CustomError.ShowDialog("Bu oyun kütüphanende bulunduğu için istek listesinde tutulmadı.", "Bilgi", owner: this);
                }
                else
                {
                    CustomError.ShowDialog(
                        isInWishlist ? "Oyun istek listene eklendi." : "Oyun istek listesinden çıkarıldı.",
                        "Bilgi",
                        owner: this);
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"İstek listesi güncellenemedi: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        // ticaret akışlarını tek yerden yönet
        private readonly CommerceController _commerceController = new();

        // istek listesi akışını yönet
        private readonly WishlistController _wishlistController = new();

        // profil akışını yönet
        private readonly ProfileController _profileController = new();

        // arkadaslik akislarini yonet
        private readonly FriendshipController _friendshipController = new();

        // baska profil aciksa id tutulur, null ise kendi profil
        private int? _viewedProfileUserId;
        private FriendshipRelationshipStatus _visitorRelationship = FriendshipRelationshipStatus.None;

        // arkadaslar sayfasinda aktif tab
        private string _friendsPageActiveTab = "friends";

        // arkadaslar sayfasinda son yapilan arama metni
        private string _friendsPageLastSearchQuery = string.Empty;

        // anlık sepet verisini tut
        private List<CartGameItem> _cartItems = new();

        // sahip olunan oyunları tut
        private List<LibraryGameItem> _libraryItems = new();

        // son işlem geçmişini tut
        private List<WalletTransactionItem> _walletTransactions = new();

        // sahiplik durumunu hızlı sorgula
        private HashSet<int> _ownedGameIds = new();

        // sepetteki oyunları hızlı sorgula
        private HashSet<int> _cartGameIds = new();

        // istek listesindeki oyunları hızlı sorgula
        private HashSet<int> _wishlistGameIds = new();

        // indirme durumlarını oyun koduna göre tut
        private Dictionary<int, DownloadStateItem> _downloadStates = new();

        // indirmeler sayfası için görünüm modelini tut
        private List<DownloadQueueItem> _downloadItems = new();

        // istek listesinin görünüm modelini tut
        private List<WishlistGameItem> _wishlistItems = new();

        // profil özetini tek yerde tut
        private ProfileSummary? _profileSummary;

        // son oynanan oyunları tut
        private List<ProfileRecentPlayItem> _profileRecentPlays = new();

        // indirme akışını ayrı controller ile yönet
        private readonly DownloadController _downloadController = new();

        // sahte oyun baslatma akisini ayri tut
        private readonly LaunchController _launchController = new();

        // kurulum ekraninda ayni acilista sabit hero tut
        private readonly Random _installHeroRandom = new();
        private BitmapImage? _currentInstallHeroPreview;

        // indirmeleri arka planda ilerlet
        private readonly DispatcherTimer _downloadQueueTimer = new();

        // aynı anda ikinci tick girmesin
        private bool _isDownloadTickRunning;

        // detaydan geri dönüşte kütüphane akışını koru
        private bool _isLibraryViewActive;

        // indirmeler ekranından gelinen akışı koru
        private bool _isDownloadsViewActive;

        // ayrı kurulum ekranında olup olmadığını tut
        private bool _isInstallViewActive;

        // seçili ödeme yöntemini sakla
        private string _selectedPaymentMethod = "visa";

        private void InitializeCommerceState()
        {
            // ilk verileri açılışta yükle
            RefreshCommerceState(false);
        }

        private void InitializeDownloadState()
        {
            // kalıcı indirme kuyruğunu timer ile canlı tut
            RefreshDownloadState(false);
            _downloadQueueTimer.Start();
        }

        private void RefreshCommerceState(bool showErrors = true)
        {
            // tüm ticaret yüzeylerini aynı anda yenile
            try
            {
                if (UserSession.IsGuest)
                {
                    // misafir için tüm listeleri temizle
                    _cartItems = new List<CartGameItem>();
                    _libraryItems = new List<LibraryGameItem>();
                    _walletTransactions = new List<WalletTransactionItem>();
                    _ownedGameIds = new HashSet<int>();
                    _cartGameIds = new HashSet<int>();
                    _wishlistGameIds = new HashSet<int>();
                    _wishlistItems = new List<WishlistGameItem>();
                    _profileSummary = null;
            _profileRecentPlays = new List<ProfileRecentPlayItem>();
                    _downloadStates = new Dictionary<int, DownloadStateItem>();
                    _downloadItems = new List<DownloadQueueItem>();
                }
                else
                {
                    // güncel bakiyeyi oturuma yaz
                    decimal balance = _commerceController.GetBalance(UserSession.UserId);
                    UserSession.UpdateBalance(balance);

                    // sayfalarda kullanılacak listeleri çek
                    _cartItems = _commerceController.GetCartItems(UserSession.UserId).ToList();
                    _libraryItems = _commerceController.GetLibraryGames(UserSession.UserId).ToList();
                    _walletTransactions = _commerceController.GetRecentTransactions(UserSession.UserId).ToList();

                    // hızlı kontrol setlerini doldur
                    _ownedGameIds = _commerceController.GetOwnedGameIds(UserSession.UserId);
                    _cartGameIds = _commerceController.GetCartGameIds(UserSession.UserId);
                    _wishlistGameIds = _wishlistController.GetWishlistGameIds(UserSession.UserId);
                    _wishlistItems = _wishlistController.GetWishlistItems(UserSession.UserId).ToList();
                    _profileSummary = _profileController.GetProfileSummary(UserSession.UserId);
                    _profileRecentPlays = _profileController.GetRecentPlays(UserSession.UserId).ToList();
                    UserSession.UpdateProfile(
                        _profileSummary.ProfileImagePath,
                        _profileSummary.BannerImagePath,
                        _profileSummary.Bio);
                }

                // indirme durumunu da ana veriyle beraber yükle
                RefreshDownloadState(showErrors);
                RefreshCartPopup();
                RefreshWalletPage();
                RefreshProfilePage();
                RefreshWishlistPage();
                RefreshFriendsBadge();
            }
            catch (Exception ex)
            {
                // hata ekrana kontrollü yansın
                if (showErrors)
                {
                    CustomError.ShowDialog($"Ticaret verileri yüklenemedi: {ex.Message}", "Sistem Hatası", owner: this);
                }
            }
        }

        private void RefreshDownloadState(bool showErrors = true)
        {
            // kurulum ve indirme durumlarını tek yerde yenile
            try
            {
                if (UserSession.IsGuest)
                {
                    _downloadStates = new Dictionary<int, DownloadStateItem>();
                    _downloadItems = new List<DownloadQueueItem>();
                }
                else
                {
                    _downloadStates = new Dictionary<int, DownloadStateItem>(_downloadController.GetDownloadStates(UserSession.UserId));
                    _downloadItems = _downloadController.GetDownloadQueue(UserSession.UserId).ToList();
                }

                ApplyStoreOwnershipState();
                ApplyDetailOwnershipState();
                RefreshLibraryPanel();
                RefreshDownloadsPanel();

                if (_isInstallViewActive)
                {
                    RefreshInstallViewSurface();
                }
            }
            catch (Exception ex)
            {
                // indirme hatasını ayrı yakala
                if (showErrors)
                {
                    CustomError.ShowDialog($"İndirme verileri yüklenemedi: {ex.Message}", "Sistem Hatası", owner: this);
                }
            }
        }

        private void ApplyStoreOwnershipState()
        {
            // store kartlarında sahiplik bilgisini yansıt
            foreach (StoreGameCardItem item in _storeItems)
            {
                item.IsOwned = _ownedGameIds.Contains(item.GameId);
                item.IsInCart = !item.IsOwned && _cartGameIds.Contains(item.GameId);

                // kart alt durumunu netleştir
                if (item.IsOwned)
                {
                    item.StatusText = "Kütüphanende bulunuyor";
                }
                else
                {
                    item.StatusText = item.IsInCart ? "Sepette" : string.Empty;
                }
            }

            // items controlü taze veriyle bağla
            icStoreGames.ItemsSource = null;
            icStoreGames.ItemsSource = _storeItems;

            // yerleşimi yeni genişlikle kur
            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
        }

        private void ApplyDetailOwnershipState()
        {
            // detay ekranında tek buton dili kullan
            if (_currentDetail == null)
            {
                return;
            }

            // seçili oyunun sahiplik bilgisini güncelle
            _currentDetail.IsOwned = _ownedGameIds.Contains(_currentDetail.GameId);
            _currentDetail.IsInCart = !_currentDetail.IsOwned && _cartGameIds.Contains(_currentDetail.GameId);
            _currentDetail.IsInWishlist = !_currentDetail.IsOwned && _wishlistGameIds.Contains(_currentDetail.GameId);

            // durum metnini varsayılan olarak gizle
            txtDetailOwnershipState.Visibility = Visibility.Collapsed;

            if (UserSession.IsGuest)
            {
                // misafire açık yönlendirme göster
                txtDetailOwnershipState.Text = "Satın almak için giriş yap";
                txtDetailOwnershipState.Foreground = CreateBrush("#8F98A5");
                txtDetailOwnershipState.Visibility = Visibility.Visible;
                btnDetailAddToCart.Content = BuildDetailPrimaryActionContent("Sepete Ekle", "\uE7BF");
                btnDetailAddToCart.Background = CreateBrush("#FFFFFF");
                btnDetailAddToCart.BorderBrush = CreateBrush("#FFFFFF");
                btnDetailAddToCart.Foreground = CreateBrush("#0A0A0C");
                btnDetailAddToCart.IsEnabled = true;
                btnDetailWishlist.Content = BuildDetailPrimaryActionContent("İstek Listesine Ekle", "\uE734");
                btnDetailWishlist.IsEnabled = true;
                HideDetailInstallPanel();
                return;
            }

            if (_currentDetail.IsOwned)
            {
                // sahip olunan oyunda kutuphaneye yonlendir
                txtDetailOwnershipState.Text = "Bu oyun kütüphanende bulunuyor";
                txtDetailOwnershipState.Foreground = CreateBrush(OwnedStatusAccentHex);
                txtDetailOwnershipState.Visibility = Visibility.Visible;
                btnDetailAddToCart.Content = BuildDetailPrimaryActionContent("Kütüphaneye Git", "\uE8F1");
                btnDetailAddToCart.Background = CreateBrush("#31C653");
                btnDetailAddToCart.BorderBrush = CreateBrush("#31C653");
                btnDetailAddToCart.Foreground = CreateBrush("#FFFFFF");
                btnDetailAddToCart.IsEnabled = true;
                btnDetailWishlist.Content = BuildDetailPrimaryActionContent("Kütüphanende", "\uE8F1");
                btnDetailWishlist.IsEnabled = false;
                return;
            }

            if (_currentDetail.IsInCart)
            {
                // sepette olan oyunda tekrar ekleme kapansın
                txtDetailOwnershipState.Text = "Bu oyun sepette bekliyor";
                txtDetailOwnershipState.Foreground = CreateBrush("#F5D174");
                txtDetailOwnershipState.Visibility = Visibility.Visible;
                btnDetailAddToCart.Content = BuildDetailPrimaryActionContent("Sepette", "\uE7BF");
                btnDetailAddToCart.Background = CreateBrush("#111114");
                btnDetailAddToCart.BorderBrush = CreateBrush("#1C1C22");
                btnDetailAddToCart.Foreground = CreateBrush("#FFFFFF");
                btnDetailAddToCart.IsEnabled = false;
                btnDetailWishlist.Content = _currentDetail.IsInWishlist
                    ? BuildDetailPrimaryActionContent("İstek Listesinden Çıkar", "\uE8D9")
                    : BuildDetailPrimaryActionContent("İstek Listesine Ekle", "\uE734");
                btnDetailWishlist.IsEnabled = true;
                HideDetailInstallPanel();
                return;
            }

            // satın alınabilir durumda butonu aç
            btnDetailAddToCart.Content = BuildDetailPrimaryActionContent("Sepete Ekle", "\uE7BF");
            btnDetailAddToCart.Background = CreateBrush("#FFFFFF");
            btnDetailAddToCart.BorderBrush = CreateBrush("#FFFFFF");
            btnDetailAddToCart.Foreground = CreateBrush("#0A0A0C");
            btnDetailAddToCart.IsEnabled = true;
            btnDetailWishlist.Content = _currentDetail.IsInWishlist
                ? BuildDetailPrimaryActionContent("İstek Listesinden Çıkar", "\uE8D9")
                : BuildDetailPrimaryActionContent("İstek Listesine Ekle", "\uE734");
            btnDetailWishlist.IsEnabled = true;
            HideDetailInstallPanel();
        }

        private void RefreshCartPopup()
        {
            // sepet içeriğini yeniden bağla
            icCartItems.ItemsSource = null;
            icCartItems.ItemsSource = _cartItems;

            // toplam tutarı tek yerde hesapla
            bool hasItems = _cartItems.Count > 0;
            decimal totalAmount = _cartItems.Sum(item => item.PriceAmount);

            // üst bilgi metnini duruma göre değiştir
            txtCartSummary.Text = UserSession.IsGuest
                ? "Sepet giriş yaptıktan sonra aktif olur"
                : hasItems
                    ? $"{_cartItems.Count} oyun satın almaya hazır"
                    : "Henüz sepetine oyun eklemedin";

            // toplam tutarı göster
            txtCartTotal.Text = FormatMoney(totalAmount);

            // boş ve dolu durumları ayır
            EmptyCartState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            CartItemsScrollViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;

            // misafir veya boş sepette ödemeyi kapat
            btnCheckoutCart.IsEnabled = !UserSession.IsGuest && hasItems;

            // cart badge sayısını yenile
            bdgCartCount.Visibility = !UserSession.IsGuest && hasItems ? Visibility.Visible : Visibility.Collapsed;
            txtCartCount.Text = _cartItems.Count > 99 ? "99+" : _cartItems.Count.ToString();
        }

        private void RefreshWalletPage()
        {
            // üstteki bakiye pillini güncelle
            txtWalletBalance.Text = UserSession.IsGuest ? "Giriş yap" : FormatMoney(UserSession.Balance);

            // sayfadaki özet bakiyeyi güncelle
            txtWalletPopupBalance.Text = UserSession.IsGuest
                ? "Giriş yapman gerekiyor"
                : FormatMoney(UserSession.Balance);

            // cüzdan açıklama satırını sade tut
            txtWalletPageInfo.Text = UserSession.IsGuest
                ? "Cüzdan ve ödeme yöntemleri girişten sonra aktif olur"
                : "Bakiye yükleme ve işlem geçmişi burada görünür";

            // hareket geçmişini listele
            icWalletTransactions.ItemsSource = null;
            icWalletTransactions.ItemsSource = _walletTransactions;

            // boş durum katmanını ayarla
            bool hasTransactions = _walletTransactions.Count > 0;
            EmptyWalletState.Visibility = hasTransactions ? Visibility.Collapsed : Visibility.Visible;
            WalletTransactionsScrollViewer.Visibility = hasTransactions ? Visibility.Visible : Visibility.Collapsed;

            // seçili kart kartvizitini yenile
            ApplyPaymentMethodSelection();
        }

        private void RefreshLibraryPanel()
        {
            // her kartı güncel indirme durumuyla zenginleştir
            foreach (LibraryGameItem item in _libraryItems)
            {
                ApplyDownloadStateToLibraryItem(item);
            }

            // tur secimini son listeye gore kur
            BuildLibraryCategories();

            // secili ture gore gorunen listeyi al
            List<LibraryGameItem> visibleLibraryItems = GetVisibleLibraryItems();

            // kütüphane verisini yeniden bağla
            icLibraryGames.ItemsSource = null;
            icLibraryGames.ItemsSource = visibleLibraryItems;

            if (UserSession.IsGuest)
            {
                // misafir kütüphane mesajı
                txtLibraryResultInfo.Text = "Kütüphaneyi görmek için giriş yap";
                txtLibraryEmptyTitle.Text = "Kütüphane oturumla açılır";
                txtLibraryEmptyMessage.Text = "Satın aldığın oyunlar burada görünür";
            }
            else if (_libraryItems.Count == 0)
            {
                // ilk satın alma öncesi boş durum
                txtLibraryResultInfo.Text = "Henüz sahip olunan oyun bulunmuyor";
                txtLibraryEmptyTitle.Text = "Kütüphanede oyun görünmüyor";
                txtLibraryEmptyMessage.Text = "Mağazadan satın aldığın oyunlar burada listelenecek";
            }
            else if (visibleLibraryItems.Count == 0)
            {
                // secilen turde sonuc yoksa net bir bos durum goster
                txtLibraryResultInfo.Text = "Seçilen türde oyun bulunmuyor";
                txtLibraryEmptyTitle.Text = "Bu türde oyun görünmüyor";
                txtLibraryEmptyMessage.Text = "Farklı bir tür seçerek kütüphaneni inceleyebilirsin";
            }
            else
            {
                // dolu kütüphane sonucu
                txtLibraryResultInfo.Text = string.Equals(_selectedLibraryCategory, "Tümü", StringComparison.OrdinalIgnoreCase)
                    ? _libraryItems.Count == 1
                        ? "1 oyun kütüphanede bulunuyor"
                        : $"{_libraryItems.Count} oyun kütüphanede bulunuyor"
                    : visibleLibraryItems.Count == 1
                        ? $"1 oyun {_selectedLibraryCategory} türünde görünüyor"
                        : $"{visibleLibraryItems.Count} oyun {_selectedLibraryCategory} türünde görünüyor";
                txtLibraryEmptyTitle.Text = "Kütüphanede oyun görünmüyor";
                txtLibraryEmptyMessage.Text = "Mağazadan satın aldığın oyunlar burada listelenecek";
            }

            // boş kütüphane durumunu ayarla
            EmptyLibraryState.Visibility = visibleLibraryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // soldan akan yerleşimi yeniden hesapla
            Dispatcher.BeginInvoke(new Action(() => UpdateLibraryGridColumns()), DispatcherPriority.Loaded);
        }

        private void ApplyDownloadStateToLibraryItem(LibraryGameItem item)
        {
            // kütüphane kartını kurulum durumuyla güncelle
            DownloadStateItem state = GetInstallSurfaceState(item.GameId);
            item.InstallStatus = state.InstallStatus;
            item.InstallStatusText = state.InstallStatusText;
            item.InstallAccent = state.InstallAccent;
            item.ShowProgress = state.ShowProgress;
            item.ProgressValue = state.ProgressValue;
            item.ProgressText = state.ProgressText;
            item.SizeText = state.SizeText;
            item.PrimaryActionText = state.PrimaryActionText;
            item.SecondaryActionText = state.SecondaryActionText;
            item.ShowSecondaryAction = state.ShowSecondaryAction;
            item.InstallPath = state.InstallPath;
            item.TotalPlayTimeText = BuildPlayTimeText(GetDisplayPlaySeconds(item.GameId));
        }

        private DownloadStateItem GetDownloadStateOrDefault(int gameId)
        {
            // kayıt yoksa varsayılan kurulu değil durumunu ver
            return _downloadStates.TryGetValue(gameId, out DownloadStateItem? state)
                ? state
                : new DownloadStateItem { GameId = gameId };
        }

        private DownloadStateItem GetInstallSurfaceState(int gameId)
        {
            // ham durumu arayuz icin normalize et
            DownloadStateItem source = GetDownloadStateOrDefault(gameId);
            DownloadStateItem state = new()
            {
                GameId = source.GameId,
                InstallStatus = source.InstallStatus,
                InstallStatusText = source.InstallStatusText,
                InstallAccent = source.InstallAccent,
                ShowProgress = source.ShowProgress,
                ProgressValue = source.ProgressValue,
                ProgressText = source.ProgressText,
                SizeText = source.SizeText,
                PrimaryActionText = source.PrimaryActionText,
                SecondaryActionText = source.SecondaryActionText,
                ShowSecondaryAction = source.ShowSecondaryAction,
                InstallPath = source.InstallPath
            };

            switch (state.InstallStatus)
            {
                case "downloading":
                    state.InstallStatusText = "İndiriliyor";
                    state.InstallAccent = "#6FCBFF";
                    state.PrimaryActionText = "DURAKLAT";
                    state.SecondaryActionText = "İptal";
                    state.ShowSecondaryAction = true;
                    break;

                case "queued":
                    state.InstallStatusText = "Sırada";
                    state.InstallAccent = "#F5D174";
                    state.PrimaryActionText = "DURAKLAT";
                    state.SecondaryActionText = "İptal";
                    state.ShowSecondaryAction = true;
                    break;

                case "paused":
                    state.InstallStatusText = "Duraklatıldı";
                    state.InstallAccent = "#F39C54";
                    state.PrimaryActionText = "DEVAM ET";
                    state.SecondaryActionText = "İptal";
                    state.ShowSecondaryAction = true;
                    break;

                case "installed":
                    state.InstallStatusText = "Yüklü";
                    state.InstallAccent = OwnedStatusAccentHex;
                    state.PrimaryActionText = "OYNA";
                    state.SecondaryActionText = "Kaldır";
                    state.ShowSecondaryAction = true;

                    if (_launchController.IsRunning(gameId))
                    {
                        state.InstallStatusText = "Çalışıyor";
                        state.InstallAccent = "#E24D4D";
                        state.PrimaryActionText = "DURDUR";
                        state.SecondaryActionText = string.Empty;
                        state.ShowSecondaryAction = false;
                        state.ShowProgress = false;
                    }

                    break;

                default:
                    state.InstallStatus = "not_installed";
                    state.InstallStatusText = "Yüklü değil";
                    state.InstallAccent = "#8F98A5";
                    state.PrimaryActionText = "YÜKLE";
                    state.SecondaryActionText = string.Empty;
                    state.ShowSecondaryAction = false;
                    state.ShowProgress = false;
                    state.ProgressValue = 0;
                    state.ProgressText = string.Empty;
                    break;
            }

            return state;
        }

        private void ApplyLaunchStateToDownloadItem(DownloadQueueItem item)
        {
            // indirme listesinde oyna durdur dilini koru
            if (item.InstallStatus != "installed")
            {
                return;
            }

            bool isRunning = _launchController.IsRunning(item.GameId);
            item.InstallStatusText = isRunning ? "Çalışıyor" : "Yüklü";
            item.InstallAccent = isRunning ? "#E24D4D" : OwnedStatusAccentHex;
            item.PrimaryActionText = isRunning ? "DURDUR" : "OYNA";
            item.SecondaryActionText = isRunning ? string.Empty : "Kaldır";
            item.ShowSecondaryAction = !isRunning;
            item.ShowProgress = false;
        }

        private string BuildPlayTimeText(int totalPlaySeconds)
        {
            // oynama suresini kisa tut
            if (totalPlaySeconds <= 0)
            {
                return "-";
            }

            TimeSpan playTime = TimeSpan.FromSeconds(totalPlaySeconds);

            if (playTime.TotalHours >= 1)
            {
                return $"{(int)playTime.TotalHours} sa {playTime.Minutes:D2} dk";
            }

            int minutes = Math.Max(1, (int)Math.Round(playTime.TotalMinutes));
            return $"{minutes} dk";
        }

        private int GetStoredPlaySeconds(int gameId)
        {
            // oynama suresini kutuphane kaydindan cek
            if (UserSession.IsGuest)
            {
                return 0;
            }

            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT TotalPlaySeconds
                  FROM UserLibrary
                  WHERE UserId = @UserId
                    AND GameId = @GameId
                  LIMIT 1;",
                new SqlParameter("@UserId", UserSession.UserId),
                new SqlParameter("@GameId", gameId));

            if (result == null || result == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private int GetDisplayPlaySeconds(int gameId)
        {
            // aktif oturumu kayitli sureye ekle
            return GetStoredPlaySeconds(gameId) + _launchController.GetCurrentSessionSeconds(gameId);
        }

        private string ResolveGameTitle(int gameId)
        {
            // oyun adini mevcut gorunumlerden bul
            if (_currentDetail != null && _currentDetail.GameId == gameId)
            {
                return _currentDetail.Title;
            }

            LibraryGameItem? libraryItem = _libraryItems.FirstOrDefault(item => item.GameId == gameId);
            if (libraryItem != null)
            {
                return libraryItem.Title;
            }

            DownloadQueueItem? downloadItem = _downloadItems.FirstOrDefault(item => item.GameId == gameId);
            if (downloadItem != null)
            {
                return downloadItem.Title;
            }

            StoreGameCardItem? storeItem = _storeItems.FirstOrDefault(item => item.GameId == gameId);
            return storeItem?.Title ?? $"Game {gameId}";
        }

        private object BuildInstallPrimaryContent(string label)
        {
            // ana aksiyon ikonlu gorunsun
            string icon = label switch
            {
                "OYNA" => "▶",
                "DURDUR" => "■",
                "DURAKLAT" => "Ⅱ",
                "DEVAM ET" => "↻",
                _ => "↓"
            };

            StackPanel content = new()
            {
                Orientation = Orientation.Horizontal
            };

            content.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });

            content.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            return content;
        }

        private object BuildDetailPrimaryActionContent(string label, string glyph)
        {
            // detay ana aksiyonunu ikonla tek dilde kur
            StackPanel content = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            content.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            content.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            return content;
        }

        private void ApplyInstallPrimaryActionStyle(DownloadStateItem state)
        {
            // ana butonu duruma gore renklendir
            string background = "#FFFFFF";
            string border = "#FFFFFF";
            string foreground = "#0A0A0C";

            switch (state.PrimaryActionText)
            {
                case "OYNA":
                    background = "#31C653";
                    border = "#31C653";
                    foreground = "#FFFFFF";
                    break;

                case "DURDUR":
                    background = "#E34B4B";
                    border = "#E34B4B";
                    foreground = "#FFFFFF";
                    break;

                case "DURAKLAT":
                    background = "#4A8BFF";
                    border = "#4A8BFF";
                    foreground = "#FFFFFF";
                    break;

                case "DEVAM ET":
                    background = "#FFFFFF";
                    border = "#FFFFFF";
                    foreground = "#0A0A0C";
                    break;
            }

            btnInstallPrimaryAction.Background = CreateBrush(background);
            btnInstallPrimaryAction.BorderBrush = CreateBrush(border);
            btnInstallPrimaryAction.Foreground = CreateBrush(foreground);
            btnInstallPrimaryAction.Content = BuildInstallPrimaryContent(state.PrimaryActionText);
        }

        private void ApplyInstallSecondaryActionStyle(DownloadStateItem state)
        {
            // ikincil aksiyonu duruma gore sadeleştir
            btnInstallSecondaryAction.ToolTip = null;
            btnInstallSecondaryAction.Padding = new Thickness(26, 0, 26, 0);
            btnInstallSecondaryAction.Width = 154;
            btnInstallSecondaryAction.Height = 58;
            btnInstallSecondaryAction.Background = CreateBrush("#12161C");
            btnInstallSecondaryAction.BorderBrush = CreateBrush("#2A313D");
            btnInstallSecondaryAction.Foreground = CreateBrush("#FFFFFF");

            if (state.InstallStatus == "installed" && !_launchController.IsRunning(state.GameId))
            {
                btnInstallSecondaryAction.Width = 58;
                btnInstallSecondaryAction.Padding = new Thickness(0);
                btnInstallSecondaryAction.Background = CreateBrush("#12161C");
                btnInstallSecondaryAction.BorderBrush = CreateBrush("#303746");
                btnInstallSecondaryAction.Foreground = CreateBrush("#FFFFFF");
                btnInstallSecondaryAction.Content = new TextBlock
                {
                    Text = "\uE74D",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnInstallSecondaryAction.ToolTip = "Oyunu Kaldır";
                return;
            }

            btnInstallSecondaryAction.Content = state.SecondaryActionText;
        }

        private void ApplyDetailDownloadState()
        {
            // detay sağ kolona kurulum durumunu yansıt
            if (_currentDetail == null || !_currentDetail.IsOwned)
            {
                HideDetailInstallPanel();
                return;
            }

            DownloadStateItem state = GetInstallSurfaceState(_currentDetail.GameId);
            _currentDetail.InstallStatus = state.InstallStatus;
            _currentDetail.InstallStatusText = state.InstallStatusText;
            _currentDetail.InstallAccent = state.InstallAccent;
            _currentDetail.ShowInstallProgress = state.ShowProgress;
            _currentDetail.InstallProgressValue = state.ProgressValue;
            _currentDetail.InstallProgressText = state.ProgressText;
            _currentDetail.PrimaryInstallActionText = state.PrimaryActionText;
            _currentDetail.SecondaryInstallActionText = state.SecondaryActionText;
            _currentDetail.ShowSecondaryInstallAction = state.ShowSecondaryAction;
            _currentDetail.InstallPath = state.InstallPath;

            DetailInstallPanel.Visibility = Visibility.Visible;
            txtDetailInstallState.Text = state.InstallStatusText;
            txtDetailInstallState.Foreground = CreateBrush(state.InstallAccent);
            txtDetailInstallSize.Text = string.IsNullOrWhiteSpace(state.SizeText)
                ? "Kurulum boyutu hazırlanıyor"
                : $"Kurulum boyutu {state.SizeText}";
            txtDetailInstallProgress.Text = state.ShowProgress ? state.ProgressText : string.Empty;
            txtDetailInstallProgress.Visibility = state.ShowProgress ? Visibility.Visible : Visibility.Collapsed;
            pbDetailInstallProgress.Visibility = state.ShowProgress ? Visibility.Visible : Visibility.Collapsed;
            pbDetailInstallProgress.Foreground = CreateBrush(state.InstallAccent);
            pbDetailInstallProgress.Value = state.ProgressValue;
            txtDetailInstallPath.Text = string.Empty;
            txtDetailInstallPath.Visibility = Visibility.Collapsed;
            btnDetailPrimaryInstallAction.Content = state.PrimaryActionText;
            btnDetailSecondaryInstallAction.Content = state.SecondaryActionText;
            btnDetailSecondaryInstallAction.Visibility = state.ShowSecondaryAction ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideDetailInstallPanel()
        {
            // sahiplik yoksa kurulum kutusunu gizle
            DetailInstallPanel.Visibility = Visibility.Collapsed;
            pbDetailInstallProgress.Value = 0;
            txtDetailInstallProgress.Text = string.Empty;
            txtDetailInstallPath.Text = string.Empty;
        }

        private void RefreshDownloadsPanel()
        {
            // indirmeler sayfası verisini yeniden bağla
            foreach (DownloadQueueItem item in _downloadItems)
            {
                ApplyLaunchStateToDownloadItem(item);
            }

            icDownloads.ItemsSource = null;
            icDownloads.ItemsSource = _downloadItems;

            if (UserSession.IsGuest)
            {
                txtDownloadsResultInfo.Text = "İndirmeleri görmek için giriş yap";
                txtDownloadsEmptyTitle.Text = "İndirmeler oturumla açılır";
                txtDownloadsEmptyMessage.Text = "Kurulum başlattığın oyunlar burada görünür";
            }
            else if (_downloadItems.Count == 0)
            {
                txtDownloadsResultInfo.Text = "Henüz kurulum başlatılmadı";
                txtDownloadsEmptyTitle.Text = "İndirme kuyruğu boş";
                txtDownloadsEmptyMessage.Text = "Kütüphanendeki bir oyundan kurulumu başlattığında burada görünür";
            }
            else
            {
                txtDownloadsResultInfo.Text = _downloadItems.Count == 1
                    ? "1 oyun indirme listesinde görünüyor"
                    : $"{_downloadItems.Count} oyun indirme listesinde görünüyor";
                txtDownloadsEmptyTitle.Text = "İndirme kuyruğu boş";
                txtDownloadsEmptyMessage.Text = "Kütüphanendeki bir oyundan kurulumu başlattığında burada görünür";
            }

            EmptyDownloadsState.Visibility = _downloadItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DownloadsItemsScrollViewer.Visibility = _downloadItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowInstallView(StoreGameDetail detail)
        {
            // kütüphane ve indirme için ayrı kurulum ekranı aç
            _currentDetail = detail;
            _isInstallViewActive = true;
            _isWishlistViewActive = false;
            _currentInstallHeroPreview = SelectInstallHeroPreview(detail);

            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Visible;

            SetSidebarSelection(
                _isDownloadsViewActive
                    ? btnDownloadsNav
                    : _isLibraryViewActive
                        ? btnLibraryNav
                        : btnStoreNav);
            RefreshInstallViewSurface();
        }

        private void RefreshInstallViewSurface()
        {
            // ayrı kurulum ekranını seçili oyunla doldur
            if (_currentDetail == null)
            {
                return;
            }

            DownloadStateItem state = GetInstallSurfaceState(_currentDetail.GameId);
            LibraryGameItem? libraryItem = _libraryItems.FirstOrDefault(item => item.GameId == _currentDetail.GameId);
            int totalPlaySeconds = GetDisplayPlaySeconds(_currentDetail.GameId);

            imgInstallHero.Source = _currentInstallHeroPreview ?? _currentDetail.CoverPreview;
            imgInstallCover.Source = _currentDetail.CoverPreview;
            txtInstallTitle.Text = _currentDetail.Title;
            txtInstallDescription.Text = DisplayOrFallback(_currentDetail.Description);
            txtInstallPlaySummary.Text = $"Toplam Oynama süreniz: {BuildPlayTimeText(totalPlaySeconds)}";
            txtInstallDeveloperValue.Text = string.IsNullOrWhiteSpace(libraryItem?.PurchasedAtText)
                ? "-"
                : libraryItem.PurchasedAtText;
            txtInstallPublisherValue.Text = BuildPlayTimeText(totalPlaySeconds);
            txtInstallReleaseValue.Text = DisplayOrFallback(_currentDetail.Publisher);
            txtInstallLanguagesValue.Text = DisplayOrFallback(_currentDetail.SupportedLanguages);
            txtInstallPlatformsValue.Text = _currentDetail.Platforms.Count == 0
                ? "Belirtilmedi"
                : string.Join("  •  ", _currentDetail.Platforms);

            pbInstallProgress.Foreground = CreateBrush(state.InstallAccent);
            pbInstallProgress.Value = state.ProgressValue;
            pbInstallProgress.Visibility = state.ShowProgress ? Visibility.Visible : Visibility.Collapsed;

            txtInstallProgress.Text = state.ProgressText;
            txtInstallProgress.Visibility = state.ShowProgress ? Visibility.Visible : Visibility.Collapsed;

            ApplyInstallPrimaryActionStyle(state);
            ApplyInstallSecondaryActionStyle(state);
            btnInstallSecondaryAction.Visibility = state.ShowSecondaryAction ? Visibility.Visible : Visibility.Collapsed;
        }

        private BitmapImage? SelectInstallHeroPreview(StoreGameDetail detail)
        {
            // her giriste galeriden farkli bir hero sec
            List<BitmapImage> previews = detail.MediaItems
                .Where(item => !item.IsTrailer && item.Preview != null)
                .Select(item => item.Preview!)
                .ToList();

            if (previews.Count == 0)
            {
                return detail.CoverPreview;
            }

            return previews[_installHeroRandom.Next(previews.Count)];
        }

        private void UpdateLibraryGridColumns()
        {
            // kütüphane kartlarını sola akıt
            double viewportWidth = LibraryGamesScrollViewer.ViewportWidth;

            // viewport yoksa gerçek genişliği kullan
            if (viewportWidth <= 0)
            {
                viewportWidth = LibraryGamesScrollViewer.ActualWidth;
            }

            // halen genişlik yoksa çık
            if (viewportWidth <= 0)
            {
                return;
            }

            // veri yoksa paneli serbest bırak
            if (icLibraryGames.Items.Count == 0)
            {
                icLibraryGames.Width = double.NaN;
                return;
            }

            // wrap alanı sola yaslanacak kadar geniş olsun
            icLibraryGames.Width = Math.Max(0, viewportWidth - 4);
        }

        private void ShowLibraryView()
        {
            // kütüphane ekranına geç
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = true;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;

            // sadece ilgili panelleri aç
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Visible;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;

            // sidebar seçimini kütüphane yap
            SetSidebarSelection(btnLibraryNav);
            RefreshLibraryPanel();
        }

        private void ShowWalletView()
        {
            // cüzdan ekranına geç
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;

            // sadece cüzdan panelini aç
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Visible;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;

            // sidebar mevcut store seçimini koru
            SetSidebarSelection(btnStoreNav);
            RefreshWalletPage();
        }

        private void ShowDownloadsView()
        {
            // indirmeler ekranına geç
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = true;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;

            // yalnızca indirme panelini aç
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Visible;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;

            // sidebar seçimini indirmeler yap
            SetSidebarSelection(btnDownloadsNav);
            RefreshDownloadsPanel();
        }

        private async void DownloadQueueTimer_Tick(object? sender, EventArgs e)
        {
            // timer reentry durumunu kapat
            if (_isDownloadTickRunning || UserSession.IsGuest)
            {
                return;
            }

            _isDownloadTickRunning = true;

            try
            {
                // indirme kuyruğunu bir adım ilerlet
                bool changed = await Task.Run(() => _downloadController.ProcessDownloadQueue(UserSession.UserId));
                bool launchChanged = _launchController.SyncExitedGames(UserSession.UserId);

                if (changed || launchChanged)
                {
                    RefreshDownloadState(false);

                    // kapanan oyun profilde hemen gorunsun
                    if (launchChanged)
                    {
                        _profileSummary = _profileController.GetProfileSummary(UserSession.UserId);
                        _profileRecentPlays = _profileController.GetRecentPlays(UserSession.UserId).ToList();

                        UserSession.UpdateProfile(
                            _profileSummary.ProfileImagePath,
                            _profileSummary.BannerImagePath,
                            _profileSummary.Bio);

                        RefreshProfilePage();
                    }
                }
                else if (_isInstallViewActive && _currentDetail != null && _launchController.IsRunning(_currentDetail.GameId))
                {
                    RefreshInstallViewSurface();
                }
            }
            catch (Exception ex)
            {
                // arka plan hatasını tek noktada göster
                CustomError.ShowDialog($"İndirme güncellenemedi: {ex.Message}", "Sistem Hatası", owner: this);
            }
            finally
            {
                _isDownloadTickRunning = false;
            }
        }

        private void DownloadPrimaryActionButton_Click(object sender, RoutedEventArgs e)
        {
            // listedeki oyuna göre temel aksiyonu çalıştır
            if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int gameId))
            {
                return;
            }

            ExecuteDownloadPrimaryAction(gameId);
        }

        private void DownloadSecondaryActionButton_Click(object sender, RoutedEventArgs e)
        {
            // listedeki oyuna göre ikincil aksiyonu çalıştır
            if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int gameId))
            {
                return;
            }

            ExecuteDownloadSecondaryAction(gameId);
        }

        private void DetailPrimaryInstallActionButton_Click(object sender, RoutedEventArgs e)
        {
            // detay ekranındaki ana kurulum aksiyonunu çalıştır
            if (_currentDetail == null)
            {
                return;
            }

            ExecuteDownloadPrimaryAction(_currentDetail.GameId);
        }

        private void DetailSecondaryInstallActionButton_Click(object sender, RoutedEventArgs e)
        {
            // detay ekranındaki ikincil kurulum aksiyonunu çalıştır
            if (_currentDetail == null)
            {
                return;
            }

            ExecuteDownloadSecondaryAction(_currentDetail.GameId);
        }

        private void ExecuteDownloadPrimaryAction(int gameId)
        {
            // seçili duruma göre indirme akışını başlat
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            try
            {
                DownloadStateItem state = GetInstallSurfaceState(gameId);

                switch (state.InstallStatus)
                {
                    case "downloading":
                    case "queued":
                        _downloadController.PauseDownload(UserSession.UserId, gameId);
                        break;

                    case "paused":
                        _downloadController.ResumeDownload(UserSession.UserId, gameId);
                        break;

                    case "installed":
                        if (_launchController.IsRunning(gameId))
                        {
                            _launchController.StopGame(UserSession.UserId, gameId);
                        }
                        else
                        {
                            _launchController.StartGame(
                                UserSession.UserId,
                                gameId,
                                ResolveGameTitle(gameId),
                                state.InstallPath);
                        }

                        break;

                    default:
                        _downloadController.QueueInstall(UserSession.UserId, gameId);
                        break;
                }

                RefreshDownloadState(false);
            }
            catch (Exception ex)
            {
                // kullanıcıya net aksiyon hatası göster
                CustomError.ShowDialog($"Kurulum işlemi tamamlanamadı: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void ExecuteDownloadSecondaryAction(int gameId)
        {
            // seçili duruma göre iptal veya kaldır çalıştır
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            try
            {
                DownloadStateItem state = GetInstallSurfaceState(gameId);

                if (state.InstallStatus == "installed")
                {
                    if (_launchController.IsRunning(gameId))
                    {
                        CustomError.ShowDialog("Oyun çalışırken kaldırılamaz.", "Bilgi", owner: this);
                        return;
                    }

                    if (!CustomConfirm.ShowDialog("Kaldır", "Bu oyunu sisteminizden silmek istediğinize emin misiniz?", "Kaldır", this))
                    {
                        return;
                    }

                    _downloadController.Uninstall(UserSession.UserId, gameId);
                }
                else
                {
                    if (!CustomConfirm.ShowDialog("İptal", "Bu kurulum indirme listesinden çıkarılsın mı?", "İptal", this))
                    {
                        return;
                    }

                    _downloadController.CancelDownload(UserSession.UserId, gameId);
                }

                RefreshDownloadState(false);
            }
            catch (Exception ex)
            {
                // ikincil aksiyon hatasını göster
                CustomError.ShowDialog($"Kurulum değiştirilemedi: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void OpenInstalledFolder(string installPath)
        {
            // kurulu klasörü dosya gezgininde aç
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            {
                CustomError.ShowDialog("Kurulum klasörü bulunamadı.", "Bilgi", owner: this);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // dosya gezgini acilmazsa bilgi ver
                CustomError.ShowDialog($"Kurulum klasörü açılamadı: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void ShowProfileView()
        {
            // profil sayfasini tek akista ac
            StopTrailer();
            popCartMenu.IsOpen = false;
            popFriendsMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;
            _viewedProfileUserId = null;

            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Visible;

            ClearSidebarSelection();
            EnterProfileEditMode(false);
            RefreshProfilePage();
        }

        private void ShowWishlistView()
        {
            // istek listesi sayfasini tam akista ac
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = true;

            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Visible;

            ClearSidebarSelection();
            RefreshWishlistPage();
        }

        private void RefreshWishlistPage()
        {
            // istek listesi sayfasini guncelle
            icWishlistGames.ItemsSource = null;
            icWishlistGames.ItemsSource = _wishlistItems;

            if (UserSession.IsGuest)
            {
                txtWishlistResultInfo.Text = "İstek listesini görmek için giriş yap";
                txtWishlistEmptyTitle.Text = "İstek listesi oturumla açılır";
                txtWishlistEmptyMessage.Text = "Beğendiğin oyunları burada biriktirebilirsin";
            }
            else if (_wishlistItems.Count == 0)
            {
                txtWishlistResultInfo.Text = "Henüz istek listene eklenen oyun yok";
                txtWishlistEmptyTitle.Text = "İstek listesi boş";
                txtWishlistEmptyMessage.Text = "Mağazada yıldız butonu ile oyun ekleyebilirsin";
            }
            else
            {
                txtWishlistResultInfo.Text = _wishlistItems.Count == 1
                    ? "1 oyun istek listesinde görünüyor"
                    : $"{_wishlistItems.Count} oyun istek listesinde görünüyor";
                txtWishlistEmptyTitle.Text = "İstek listesi boş";
                txtWishlistEmptyMessage.Text = "Mağazada yıldız butonu ile oyun ekleyebilirsin";
            }

            EmptyWishlistState.Visibility = _wishlistItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            WishlistGamesScrollViewer.Visibility = _wishlistItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RefreshProfilePage()
        {
            // baska kullanici profili mi kendi mi ayir
            if (_viewedProfileUserId.HasValue && _viewedProfileUserId.Value != UserSession.UserId)
            {
                RenderVisitorProfilePage(_viewedProfileUserId.Value);
                return;
            }

            // kendi profilini goster
            ProfileOwnerActionsPanel.Visibility = Visibility.Visible;
            btnProfileActionsMenu.Visibility = _isProfileEditMode ? Visibility.Collapsed : Visibility.Visible;
            popProfileActionsMenu.IsOpen = false;
            txtProfileRecentActivityHeader.Text = "Son Etkinlikler";
            txtProfileRecentEmptyTitle.Text = "Henüz oynama verisi oluşmadı";
            txtProfileRecentEmptyHint.Text = "Yüklü bir oyunu başlattığında son etkinlikler burada görünür";

            if (_profileSummary == null)
            {
                // oturum ozetini varsayilan degerlerle doldur
                txtProfilePageUsername.Text = UserSession.DisplayName;
                txtProfilePageHandle.Text = UserSession.IsGuest ? "Misafir" : $"@{UserSession.Username}";
                txtProfilePageBio.Text = "Henüz profil açıklaması eklenmedi";
                txtProfilePageBioEdit.Text = string.Empty;
                txtProfileOwnedCount.Text = "0";
                txtProfilePlayTime.Text = "-";
                txtProfileFriendsCount.Text = "0";
                ApplyProfileBackdrop(null, null);
                imgProfileAvatarPage.Source = null;
                txtProfileAvatarInitial.Text = UserSession.GetAvatarLetter();
                txtProfileAvatarInitial.Visibility = Visibility.Visible;
                EmptyRecentPlaysState.Visibility = Visibility.Visible;
                icProfileRecentPlays.ItemsSource = null;
                return;
            }

            // veritabanindan gelen profil alanlarini ekrana bas
            txtProfilePageUsername.Text = _profileSummary.DisplayName;
            txtProfilePageHandle.Text = $"@{_profileSummary.Username}";
            txtProfilePageBio.Text = string.IsNullOrWhiteSpace(_profileSummary.Bio)
                ? "Henüz profil açıklaması eklenmedi"
                : _profileSummary.Bio;
            txtProfilePageBioEdit.Text = _profileSummary.Bio;
            txtProfileOwnedCount.Text = _profileSummary.OwnedGameCount.ToString();
            txtProfilePlayTime.Text = BuildPlayTimeText(_profileSummary.TotalPlaySeconds);
            txtProfileFriendsCount.Text = _profileSummary.FriendsCount.ToString();

            // avatar ve banner onizlemelerini ayni anda guncelle
            ApplyProfileImagePreview(
                imgProfileAvatarPage,
                txtProfileAvatarInitial,
                _profileSummary.AvatarPreview,
                UserSession.GetAvatarLetter());
            ApplyProfileBackdrop(_profileSummary.BannerPreview, _profileSummary.AvatarPreview);

            // son etkinlikleri yeni veriyle yenile
            icProfileRecentPlays.ItemsSource = null;
            icProfileRecentPlays.ItemsSource = _profileRecentPlays;
            EmptyRecentPlaysState.Visibility = _profileRecentPlays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RenderVisitorProfilePage(int targetUserId)
        {
            // ziyaretci modunda profil verisini getir
            ProfileSummary summary;
            List<ProfileRecentPlayItem> recentPlays;

            try
            {
                summary = _profileController.GetProfileSummary(UserSession.UserId, targetUserId);
                recentPlays = _profileController.GetRecentPlays(targetUserId).ToList();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"Profil görüntülenemedi: {ex.Message}", "Sistem Hatası", owner: this);
                _viewedProfileUserId = null;
                ShowProfileView();
                return;
            }

            // kendi duzenleme panelini kapat ve menu butonunu ziyaretci moduna al
            ProfileOwnerActionsPanel.Visibility = Visibility.Collapsed;
            btnProfileActionsMenu.Visibility = Visibility.Visible;
            popProfileActionsMenu.IsOpen = false;
            HideProfileOwnerControls();

            txtProfilePageUsername.Text = summary.DisplayName;
            txtProfilePageHandle.Text = $"@{summary.Username}";
            txtProfilePageBio.Text = string.IsNullOrWhiteSpace(summary.Bio)
                ? "Henüz profil açıklaması eklenmedi"
                : summary.Bio;
            txtProfilePageBioEdit.Text = string.Empty;
            txtProfilePageBio.Visibility = Visibility.Visible;
            txtProfilePageBioEdit.Visibility = Visibility.Collapsed;
            txtProfileOwnedCount.Text = summary.OwnedGameCount.ToString();
            txtProfilePlayTime.Text = BuildPlayTimeText(summary.TotalPlaySeconds);
            txtProfileFriendsCount.Text = summary.FriendsCount.ToString();

            ApplyProfileImagePreview(
                imgProfileAvatarPage,
                txtProfileAvatarInitial,
                summary.AvatarPreview,
                BuildAvatarLetterFromName(summary.DisplayName));
            ApplyProfileBackdrop(summary.BannerPreview, summary.AvatarPreview);

            // ziyaretci icin son etkinlikleri ve kisiye ozel metinleri goster
            string visitorName = !string.IsNullOrWhiteSpace(summary.DisplayName)
                ? summary.DisplayName
                : summary.Username;
            txtProfileRecentActivityHeader.Text = $"{visitorName} - Son Etkinlikler";
            txtProfileRecentEmptyTitle.Text = $"{visitorName} henüz oyun oynamamış";
            txtProfileRecentEmptyHint.Text = "Bu kullanıcı bir oyunu başlattığında etkinlikleri burada görünecek";

            icProfileRecentPlays.ItemsSource = null;
            icProfileRecentPlays.ItemsSource = recentPlays;
            EmptyRecentPlaysState.Visibility = recentPlays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            _visitorRelationship = summary.ViewerRelationship;
        }

        private void HideProfileOwnerControls()
        {
            // ziyaretci modunda duzenleme alanlarini gizle
            btnProfilePageSave.Visibility = Visibility.Collapsed;
            btnProfilePageCancel.Visibility = Visibility.Collapsed;
            btnProfileSelectAvatar.Visibility = Visibility.Collapsed;
            btnProfileSelectBanner.Visibility = Visibility.Collapsed;
        }

        private string BuildAvatarLetterFromName(string name)
        {
            // ziyaretci avatar fallback harfini hesapla
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            return name.Trim().Substring(0, 1).ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        }

        private void ApplyProfileImagePreview(Image image, TextBlock fallbackText, BitmapImage? preview, string fallbackLetter)
        {
            // avatar yoksa harf goster
            if (preview != null)
            {
                image.Source = preview;
                image.Visibility = Visibility.Visible;
                fallbackText.Visibility = Visibility.Collapsed;
                return;
            }

            image.Source = null;
            image.Visibility = Visibility.Collapsed;
            fallbackText.Text = fallbackLetter;
            fallbackText.Visibility = Visibility.Visible;
        }

        private void ApplyProfileBackdrop(BitmapImage? bannerPreview, BitmapImage? avatarPreview)
        {
            // banner varsa onu goster yoksa avatar glow kullan
            imgProfileBanner.Source = bannerPreview;
            imgProfileBanner.Visibility = bannerPreview == null ? Visibility.Collapsed : Visibility.Visible;

            imgProfileHeroGlow.Source = bannerPreview == null ? avatarPreview : null;
            imgProfileHeroGlow.Visibility = bannerPreview == null && avatarPreview != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void EnterProfileEditMode(bool isEditMode)
        {
            // ziyaretci modunda duzenleme acilamaz
            if (_viewedProfileUserId.HasValue && _viewedProfileUserId.Value != UserSession.UserId)
            {
                _isProfileEditMode = false;
                HideProfileOwnerControls();
                txtProfilePageBio.Visibility = Visibility.Visible;
                txtProfilePageBioEdit.Visibility = Visibility.Collapsed;
                return;
            }

            // profil alanini ayni sayfada duzenlenebilir yap
            _isProfileEditMode = isEditMode;
            btnProfileActionsMenu.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
            popProfileActionsMenu.IsOpen = false;
            btnProfilePageSave.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
            btnProfilePageCancel.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
            btnProfileSelectAvatar.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
            btnProfileSelectBanner.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
            txtProfilePageBio.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
            txtProfilePageBioEdit.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;

            if (!isEditMode)
            {
                _pendingAvatarSourcePath = string.Empty;
                _pendingBannerSourcePath = string.Empty;
            }

            RefreshProfilePage();
        }

        private void ProfilePageCancelButton_Click(object sender, RoutedEventArgs e)
        {
            // kaydedilmeyen degisikligi geri al
            EnterProfileEditMode(false);
        }

        private void ProfilePageSaveButton_Click(object sender, RoutedEventArgs e)
        {
            // profil degisikligini kaydet
            if (UserSession.IsGuest)
            {
                return;
            }

            try
            {
                // bio avatar ve banner bilgisini birlikte kaydet
                _profileSummary = _profileController.SaveProfile(
                    UserSession.UserId,
                    txtProfilePageBioEdit.Text,
                    _pendingAvatarSourcePath,
                    _pendingBannerSourcePath);

                // oturumdaki profil ozetini de tazele
                UserSession.UpdateProfile(
                    _profileSummary.ProfileImagePath,
                    _profileSummary.BannerImagePath,
                    _profileSummary.Bio);

                // profil ve istek listesi ekranlarini guncelle
                _profileRecentPlays = _profileController.GetRecentPlays(UserSession.UserId).ToList();
                _wishlistItems = _wishlistController.GetWishlistItems(UserSession.UserId).ToList();
                _wishlistGameIds = _wishlistController.GetWishlistGameIds(UserSession.UserId);

                ConfigureProfileArea();
                EnterProfileEditMode(false);
                RefreshWishlistPage();
                CustomError.ShowDialog("Profil güncellendi.", "Bilgi", owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"Profil kaydedilemedi: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void ProfileSelectAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            // avatar gorselini dosyadan al
            string selectedPath = SelectProfileMediaPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            _pendingAvatarSourcePath = selectedPath;
            BitmapImage? preview = GameAssetManager.LoadBitmap(selectedPath);
            ApplyProfileImagePreview(
                imgProfileAvatarPage,
                txtProfileAvatarInitial,
                preview,
                UserSession.GetAvatarLetter());
            BitmapImage? bannerPreview = !string.IsNullOrWhiteSpace(_pendingBannerSourcePath)
                ? GameAssetManager.LoadBitmap(_pendingBannerSourcePath)
                : _profileSummary?.BannerPreview;
            ApplyProfileBackdrop(bannerPreview, preview);
        }

        private void ProfileSelectBannerButton_Click(object sender, RoutedEventArgs e)
        {
            // banner gorselini dosyadan al
            string selectedPath = SelectProfileMediaPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            _pendingBannerSourcePath = selectedPath;
            BitmapImage? preview = GameAssetManager.LoadBitmap(selectedPath);
            BitmapImage? avatarPreview = !string.IsNullOrWhiteSpace(_pendingAvatarSourcePath)
                ? GameAssetManager.LoadBitmap(_pendingAvatarSourcePath)
                : _profileSummary?.AvatarPreview;
            ApplyProfileBackdrop(preview, avatarPreview);
        }

        private string SelectProfileMediaPath()
        {
            // profil medyasi icin dosya sec
            OpenFileDialog dialog = new()
            {
                Title = "Görsel Seç",
                Filter = "Görsel Dosyaları|*.png;*.jpg;*.jpeg;*.webp",
                Multiselect = false
            };

            return dialog.ShowDialog(this) == true ? dialog.FileName : string.Empty;
        }

        private void WishlistRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // istek listesindeki tek oyunu kaldir
            if (sender is not Button button || button.Tag == null || UserSession.IsGuest)
            {
                return;
            }

            if (!int.TryParse(button.Tag.ToString(), out int gameId))
            {
                return;
            }

            // listeyi ve detay durumunu ayni anda yenile
            _wishlistController.RemoveFromWishlist(UserSession.UserId, gameId);
            RefreshCommerceState(false);
            ApplyDetailOwnershipState();
        }

        private void RecentPlayButton_Click(object sender, RoutedEventArgs e)
        {
            // son oynanan kartindan sahip olunan oyuna git
            OpenOwnedGameFromTag(sender);
        }

        private void OpenOwnedGameFromTag(object sender)
        {
            // sahip olunan oyunu kurulum ekraninda ac
            if (sender is not Button button || button.Tag == null)
            {
                return;
            }

            if (!int.TryParse(button.Tag.ToString(), out int gameId))
            {
                return;
            }

            try
            {
                StoreGameDetail detail = _storeController.GetGameDetail(gameId);
                _isLibraryViewActive = true;
                _isDownloadsViewActive = false;
                ShowInstallView(detail);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"Oyun sayfası açılamadı: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private bool EnsureAuthenticatedForCommerce()
        {
            // misafir ticaret akışlarını kapat
            if (!UserSession.IsGuest)
            {
                return true;
            }

            // açık popup kalmasın
            popCartMenu.IsOpen = false;

            // net yönlendirme göster
            CustomError.ShowDialog("Bu işlem için giriş yapmanız gerekiyor.", "Bilgi", owner: this);
            return false;
        }

        private void WalletMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // header bakiye alanından sayfaya git
            ShowWalletView();
        }

        private void CartMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // sepet popup durumunu tersine çevir
            RefreshCartPopup();
            popCartMenu.IsOpen = !popCartMenu.IsOpen;
        }

        private void WalletQuickAmountButton_Click(object sender, RoutedEventArgs e)
        {
            // hazır tutarı tek tıkla yükle
            if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int amount))
            {
                return;
            }

            AddBalance(amount);
        }

        private void AddWalletBalanceButton_Click(object sender, RoutedEventArgs e)
        {
            // özel girilen tutarı yükle
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            string rawValue = txtWalletCustomAmount.Text?.Trim() ?? string.Empty;

            // sayı ve pozitiflik kontrolü yap
            if (!int.TryParse(rawValue, out int amount) || amount <= 0)
            {
                CustomError.ShowDialog("Geçerli bir bakiye tutarı girin.", "Bilgi", owner: this);
                return;
            }

            AddBalance(amount);
        }

        private void AddBalance(int amount)
        {
            // bakiye yüklemeyi tek akışta yürüt
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            // seçili kart bilgisini özetle
            string paymentTitle = GetSelectedPaymentTitle();

            // kullanıcıdan son onayı al
            if (!CustomConfirm.ShowDialog("Bakiye Yükle", $"{FormatMoney(amount)} seçili kart ile yüklensin mi?", "Yükle", this))
            {
                return;
            }

            try
            {
                // yeni bakiyeyi veritabanından al
                decimal balanceAfter = _commerceController.AddBalance(UserSession.UserId, amount);
                UserSession.UpdateBalance(balanceAfter);

                // formu temiz tut
                txtWalletCustomAmount.Clear();

                // tüm sayfaları tek seferde yenile
                RefreshCommerceState(false);
                ShowWalletView();

                // sonuç bilgisini göster
                CustomError.ShowDialog($"{paymentTitle} ile bakiye güncellendi.", "Bilgi", owner: this);
            }
            catch (Exception ex)
            {
                // hata mesajını ekrana taşı
                CustomError.ShowDialog($"Bakiye yüklenemedi: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void RemoveCartItemButton_Click(object sender, RoutedEventArgs e)
        {
            // seçili oyunu sepetten çıkar
            if (sender is not Button button || button.Tag == null)
            {
                return;
            }

            // misafir çıkarma yapamasın
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            // game id yoksa işlemi durdur
            if (!int.TryParse(button.Tag.ToString(), out int gameId))
            {
                return;
            }

            // kaydı sil ve yüzeyi yenile
            _commerceController.RemoveFromCart(UserSession.UserId, gameId);
            RefreshCommerceState(false);
            ApplyDetailOwnershipState();

            // kullanıcı akışı bozulmasın
            popCartMenu.IsOpen = true;
        }

        private void CheckoutCartButton_Click(object sender, RoutedEventArgs e)
        {
            // sepet satın alma akışını başlat
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            // boş sepette ilerleme
            if (_cartItems.Count == 0)
            {
                CustomError.ShowDialog("Sepette satın alınacak oyun bulunmuyor.", "Bilgi", owner: this);
                return;
            }

            decimal totalAmount = _cartItems.Sum(item => item.PriceAmount);

            // son onayı kullanıcıdan al
            if (!CustomConfirm.ShowDialog("Satın Al", $"{_cartItems.Count} oyunu {FormatMoney(totalAmount)} karşılığında satın almak istiyor musun?", "Satın Al", this))
            {
                return;
            }

            try
            {
                // checkout sonucunu uygula
                CheckoutResult result = _commerceController.CheckoutCart(UserSession.UserId);
                UserSession.UpdateBalance(result.BalanceAfter);

                // tüm commerce ekranlarını yenile
                RefreshCommerceState(false);
                popCartMenu.IsOpen = false;

                // satın alma sonrası kütüphaneye git
                ShowLibraryView();
                CustomError.ShowDialog($"{result.ItemCount} oyun kütüphanene eklendi.", "Bilgi", owner: this);
            }
            catch (Exception ex)
            {
                // checkout hatasını göster
                CustomError.ShowDialog($"Satın alma tamamlanamadı: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void LibraryGamesScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // pencere boyutu değiştiğinde kütüphaneyi yeniden diz
            UpdateLibraryGridColumns();
        }

        private void PaymentMethodButton_Click(object sender, RoutedEventArgs e)
        {
            // seçili kartı değiştir
            if (sender is not Button button || button.Tag is not string methodKey)
            {
                return;
            }

            // seçimi sakla ve yüzeye uygula
            _selectedPaymentMethod = methodKey;
            ApplyPaymentMethodSelection();
        }

        private void ApplyPaymentMethodSelection()
        {
            // buton vurgularını seçime göre güncelle
            ApplyPaymentButtonStyle(btnPaymentVisa, _selectedPaymentMethod == "visa", "#2C66F5");
            ApplyPaymentButtonStyle(btnPaymentMaster, _selectedPaymentMethod == "mastercard", "#FF7043");
            ApplyPaymentButtonStyle(btnPaymentTroy, _selectedPaymentMethod == "troy", "#1DBB73");

            // seçili kart özetini yaz
            txtSelectedPaymentTitle.Text = GetSelectedPaymentTitle();
            txtSelectedPaymentNumber.Text = GetSelectedPaymentNumber();
            txtSelectedPaymentExpiry.Text = GetSelectedPaymentExpiry();
        }

        private void ApplyPaymentButtonStyle(Button button, bool isSelected, string accentColor)
        {
            // seçili kartta daha belirgin kenarlık kullan
            button.Background = isSelected ? CreateBrush("#151519") : CreateBrush("#101014");
            button.BorderBrush = isSelected ? CreateBrush(accentColor) : CreateBrush("#1E1E24");
        }

        private string GetSelectedPaymentTitle()
        {
            // kart adını tek noktadan ver
            return _selectedPaymentMethod switch
            {
                "mastercard" => "MasterCard",
                "troy" => "Troy",
                _ => "Visa"
            };
        }

        private string GetSelectedPaymentNumber()
        {
            // maskeli numarayı tek noktadan ver
            return _selectedPaymentMethod switch
            {
                "mastercard" => "**** **** **** 5454",
                "troy" => "**** **** **** 9792",
                _ => "**** **** **** 4242"
            };
        }

        private string GetSelectedPaymentExpiry()
        {
            // son kullanma özetini tek noktadan ver
            return _selectedPaymentMethod switch
            {
                "mastercard" => "Son Kullanma 09 31",
                "troy" => "Son Kullanma 05 32",
                _ => "Son Kullanma 12 30"
            };
        }

        private Brush CreateBrush(string hex)
        {
            // ortak renk üretimi yap
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private string FormatMoney(decimal amount)
        {
            // para formatını tek noktada sabitle
            return $"₺{amount.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))}";
        }

        // popup listelerini + rozeti yenile
        private void RefreshFriendsPopup()
        {
            if (UserSession.IsGuest)
            {
                UpdateFriendsBadge(0);
                return;
            }

            try
            {
                IReadOnlyList<FriendRequestItem> incoming = _friendshipController.GetIncomingRequests(UserSession.UserId);
                IReadOnlyList<FriendListItem> friends = _friendshipController.GetFriends(UserSession.UserId);

                // popup icin kisa liste
                List<FriendRequestItem> topIncoming = incoming.Take(3).ToList();
                List<FriendListItem> topFriends = friends.Take(5).ToList();

                // gelen istekler
                icFriendsPopupIncoming.ItemsSource = null;
                icFriendsPopupIncoming.ItemsSource = topIncoming;
                txtFriendsPopupIncomingCount.Text = incoming.Count.ToString();
                FriendsPopupIncomingEmpty.Visibility = topIncoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                // arkadas listesi
                icFriendsPopupList.ItemsSource = null;
                icFriendsPopupList.ItemsSource = topFriends;
                FriendsPopupListEmpty.Visibility = topFriends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                UpdateFriendsBadge(incoming.Count);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"Arkadaş bilgileri alınamadı: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        // rozet sayısını tazele
        private void RefreshFriendsBadge()
        {
            if (UserSession.IsGuest)
            {
                UpdateFriendsBadge(0);
                return;
            }

            try
            {
                int count = _friendshipController.GetIncomingRequestCount(UserSession.UserId);
                UpdateFriendsBadge(count);
            }
            catch
            {
                // hata durumunda sıfırla
                UpdateFriendsBadge(0);
            }
        }

        // bildirim rozetini güncelle (9+ sınırı)
        private void UpdateFriendsBadge(int incomingCount)
        {
            if (incomingCount <= 0)
            {
                brdFriendsBadge.Visibility = Visibility.Collapsed;
                return;
            }

            txtFriendsBadgeCount.Text = incomingCount > 9 ? "9+" : incomingCount.ToString();
            brdFriendsBadge.Visibility = Visibility.Visible;
        }

        // enter ile hizli arama tetikle
        private void FriendsQuickSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                PerformQuickFriendsSearch();
            }
        }

        // arama kutusu placeholder ve temizle butonu yönetimi
        private void FriendsQuickSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool empty = string.IsNullOrEmpty(txtFriendsQuickSearch.Text);

            btnFriendsQuickSearchClear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            txtFriendsQuickSearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            // alan temizlenince sonuçları gizle
            if (string.IsNullOrWhiteSpace(txtFriendsQuickSearch.Text))
            {
                FriendsQuickSearchScroll.Visibility = Visibility.Collapsed;
                FriendsQuickSearchEmpty.Visibility = Visibility.Collapsed;
                txtFriendsQuickSearchHint.Text = "Kullanıcı adına göre ara";
            }
        }

        // tam sayfa arama placeholder yonetimi
        private void FriendsPageSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtFriendsPageSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtFriendsPageSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // arama kutusunu sifirla
        private void FriendsQuickSearchClearButton_Click(object sender, RoutedEventArgs e)
        {
            txtFriendsQuickSearch.Text = string.Empty;
            FriendsQuickSearchScroll.Visibility = Visibility.Collapsed;
            FriendsQuickSearchEmpty.Visibility = Visibility.Collapsed;
            txtFriendsQuickSearch.Focus();
        }

        // hızlı arama (max 6 sonuç)
        private void PerformQuickFriendsSearch()
        {
            string query = (txtFriendsQuickSearch.Text ?? string.Empty).Trim();

            // en az 2 karakter
            if (query.Length < 2)
            {
                FriendsQuickSearchScroll.Visibility = Visibility.Collapsed;
                FriendsQuickSearchEmpty.Visibility = Visibility.Visible;
                txtFriendsQuickSearchEmpty.Text = "En az 2 karakter gir";
                return;
            }

            try
            {
                IReadOnlyList<FriendSearchResultItem> results = _friendshipController
                    .SearchUsers(UserSession.UserId, query, 6);

                icFriendsQuickSearch.ItemsSource = null;
                icFriendsQuickSearch.ItemsSource = results;

                // sonuç durumuna göre görünürlük ayarla
                if (results.Count == 0)
                {
                    FriendsQuickSearchScroll.Visibility = Visibility.Collapsed;
                    FriendsQuickSearchEmpty.Visibility = Visibility.Visible;
                    txtFriendsQuickSearchEmpty.Text = "Eşleşen kullanıcı bulunamadı";
                }
                else
                {
                    FriendsQuickSearchScroll.Visibility = Visibility.Visible;
                    FriendsQuickSearchEmpty.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"Arama başarısız: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        // hizli arama karti -> profil sayfasi
        private void FriendsQuickResultProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int userId)
            {
                popFriendsMenu.IsOpen = false;
                OpenUserProfile(userId);
            }
        }

        // popup icinden kabul
        private void FriendsPopupAcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not int requesterId)
            {
                return;
            }

            try
            {
                _friendshipController.AcceptRequest(UserSession.UserId, requesterId);
                RefreshFriendsPopup();

                // ana sayfa açıksa orayı da tazele
                if (FriendsContentPanel.Visibility == Visibility.Visible)
                {
                    LoadFriendsTab(_friendsPageActiveTab);
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        // popup icinden reddet
        private void FriendsPopupRejectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not int requesterId)
            {
                return;
            }

            try
            {
                _friendshipController.RejectRequest(UserSession.UserId, requesterId);
                RefreshFriendsPopup();
                if (FriendsContentPanel.Visibility == Visibility.Visible)
                {
                    LoadFriendsTab(_friendsPageActiveTab);
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void FriendsPopupOpenProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // arkada kartından profil aç
            if (sender is Button button && button.Tag is int userId)
            {
                popFriendsMenu.IsOpen = false;
                OpenUserProfile(userId);
            }
        }

        private void FriendsGoFullPageButton_Click(object sender, RoutedEventArgs e)
        {
            // tam sayfaya geç
            popFriendsMenu.IsOpen = false;
            ShowFriendsView();
        }

        private void ShowFriendsView()
        {
            // arkadaş yönetim sayfasını aç
            if (UserSession.IsGuest)
            {
                CustomError.ShowDialog("Arkadaş özelliklerini kullanmak için giriş yapın.", "Bilgi", owner: this);
                return;
            }

            StopTrailer();
            popCartMenu.IsOpen = false;
            popFriendsMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;
            _viewedProfileUserId = null;

            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Visible;

            ClearSidebarSelection();
            _friendsPageActiveTab = "friends";
            LoadFriendsTab(_friendsPageActiveTab);
        }

        private void FriendsTabButton_Click(object sender, RoutedEventArgs e)
        {
            // aktif sekmeyi belirle
            if (sender is Button button)
            {
                string tag = button.Name switch
                {
                    "btnFriendsTabFriends" => "friends",
                    "btnFriendsTabIncoming" => "incoming",
                    "btnFriendsTabOutgoing" => "outgoing",
                    "btnFriendsTabSearch" => "search",
                    _ => "friends"
                };

                _friendsPageActiveTab = tag;
                LoadFriendsTab(tag);
            }
        }

        private void LoadFriendsTab(string tab)
        {
            // sekmeye göre listeyi yükle
            UpdateFriendsTabVisibility(tab);
            UpdateFriendsTabButtonsAppearance(tab);

            try
            {
                switch (tab)
                {
                    case "friends":
                        RefreshFriendsPageFriends();
                        break;
                    case "incoming":
                        RefreshFriendsPageIncoming();
                        break;
                    case "outgoing":
                        RefreshFriendsPageOutgoing();
                        break;
                    case "search":
                        RefreshFriendsPageSearch();
                        break;
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog($"Arkadaş verileri yüklenemedi: {ex.Message}", "Sistem Hatası", owner: this);
            }
        }

        private void UpdateFriendsTabVisibility(string tab)
        {
            // aktif sekme içeriğini göster
            FriendsTabFriendsHost.Visibility = tab == "friends" ? Visibility.Visible : Visibility.Collapsed;
            FriendsTabIncomingHost.Visibility = tab == "incoming" ? Visibility.Visible : Visibility.Collapsed;
            FriendsTabOutgoingHost.Visibility = tab == "outgoing" ? Visibility.Visible : Visibility.Collapsed;
            FriendsTabSearchHost.Visibility = tab == "search" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateFriendsTabButtonsAppearance(string tab)
        {
            // aktif sekme butonu görünümü
            foreach ((string tag, Button button) in new (string, Button)[]
            {
                ("friends", btnFriendsTabFriends),
                ("incoming", btnFriendsTabIncoming),
                ("outgoing", btnFriendsTabOutgoing),
                ("search", btnFriendsTabSearch)
            })
            {
                button.Tag = string.Equals(tag, tab, StringComparison.Ordinal) ? "active" : tag;
            }
        }

        private void RefreshFriendsPageFriends()
        {
            // arkadaşlarım listesi
            IReadOnlyList<FriendListItem> friends = _friendshipController.GetFriends(UserSession.UserId);
            icFriendsPageFriends.ItemsSource = null;
            icFriendsPageFriends.ItemsSource = friends;
            FriendsTabFriendsEmpty.Visibility = friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshFriendsPageIncoming()
        {
            // gelen istekler listesi
            IReadOnlyList<FriendRequestItem> incoming = _friendshipController.GetIncomingRequests(UserSession.UserId);
            icFriendsPageIncoming.ItemsSource = null;
            icFriendsPageIncoming.ItemsSource = incoming;
            FriendsTabIncomingEmpty.Visibility = incoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateFriendsBadge(incoming.Count);
        }

        private void RefreshFriendsPageOutgoing()
        {
            // gönderilen istekler listesi
            IReadOnlyList<FriendRequestItem> outgoing = _friendshipController.GetOutgoingRequests(UserSession.UserId);
            icFriendsPageOutgoing.ItemsSource = null;
            icFriendsPageOutgoing.ItemsSource = outgoing;
            FriendsTabOutgoingEmpty.Visibility = outgoing.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshFriendsPageSearch()
        {
            // arama sekmesi sonuçları
            if (string.IsNullOrWhiteSpace(_friendsPageLastSearchQuery))
            {
                icFriendsPageSearch.ItemsSource = null;
                FriendsTabSearchEmpty.Visibility = Visibility.Visible;
                txtFriendsTabSearchEmptyTitle.Text = "Aramaya başla";
                txtFriendsTabSearchEmptyMessage.Text = "Yeni arkadaşlar bulmak için kullanıcı adı gir.";
                return;
            }

            IReadOnlyList<FriendSearchResultItem> results = _friendshipController
                .SearchUsers(UserSession.UserId, _friendsPageLastSearchQuery, 40);
            icFriendsPageSearch.ItemsSource = null;
            icFriendsPageSearch.ItemsSource = results;

            if (results.Count == 0)
            {
                FriendsTabSearchEmpty.Visibility = Visibility.Visible;
                txtFriendsTabSearchEmptyTitle.Text = "Sonuç bulunamadı";
                txtFriendsTabSearchEmptyMessage.Text = "Farklı bir kullanıcı adı dene.";
            }
            else
            {
                FriendsTabSearchEmpty.Visibility = Visibility.Collapsed;
            }
        }

        private void FriendsPageSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            // enter ile ara
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteFriendsPageSearch();
            }
        }

        private void FriendsPageSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteFriendsPageSearch();
        }

        private void ExecuteFriendsPageSearch()
        {
            // aramayı çalıştır
            string query = (txtFriendsPageSearch.Text ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                CustomError.ShowDialog("Aramak için en az 2 karakter gir.", "Bilgi", owner: this);
                return;
            }

            _friendsPageLastSearchQuery = query;
            RefreshFriendsPageSearch();
        }

        private void FriendsPageOpenProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // profil aç
            if (sender is Button button && button.Tag is int userId)
            {
                OpenUserProfile(userId);
            }
        }

        private void FriendsPageRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // arkadaşlıktan çıkar
            if (sender is not Button button || button.Tag is not int userId)
            {
                return;
            }

            try
            {
                _friendshipController.RemoveFriend(UserSession.UserId, userId);
                RefreshFriendsPageFriends();
                RefreshFriendsPopup();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void FriendsPageAcceptButton_Click(object sender, RoutedEventArgs e)
        {
            // isteği kabul et
            if (sender is not Button button || button.Tag is not int requesterId)
            {
                return;
            }

            try
            {
                _friendshipController.AcceptRequest(UserSession.UserId, requesterId);
                RefreshFriendsPageIncoming();
                RefreshFriendsPopup();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void FriendsPageRejectButton_Click(object sender, RoutedEventArgs e)
        {
            // gelen istegi sil
            if (sender is not Button button || button.Tag is not int requesterId)
            {
                return;
            }

            try
            {
                _friendshipController.RejectRequest(UserSession.UserId, requesterId);
                RefreshFriendsPageIncoming();
                RefreshFriendsPopup();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void FriendsPageCancelButton_Click(object sender, RoutedEventArgs e)
        {
            // isteği iptal et
            if (sender is not Button button || button.Tag is not int targetUserId)
            {
                return;
            }

            try
            {
                _friendshipController.CancelOutgoingRequest(UserSession.UserId, targetUserId);
                RefreshFriendsPageOutgoing();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void FriendsPageSearchActionButton_Click(object sender, RoutedEventArgs e)
        {
            // arama sonucu aksiyonu
            if (sender is not Button button || button.Tag is not int userId)
            {
                return;
            }

            try
            {
                FriendshipRelationshipStatus status = _friendshipController.GetRelationship(UserSession.UserId, userId);
                switch (status)
                {
                    case FriendshipRelationshipStatus.None:
                        _friendshipController.SendRequest(UserSession.UserId, userId);
                        break;
                    case FriendshipRelationshipStatus.PendingSent:
                        _friendshipController.CancelOutgoingRequest(UserSession.UserId, userId);
                        break;
                    case FriendshipRelationshipStatus.PendingReceived:
                        _friendshipController.AcceptRequest(UserSession.UserId, userId);
                        break;
                    default:
                        return;
                }

                RefreshFriendsPageSearch();
                RefreshFriendsPopup();
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        private void FriendsPageSearchProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // profile git
            if (sender is Button button && button.Tag is int userId)
            {
                OpenUserProfile(userId);
            }
        }

        private void OpenUserProfile(int userId)
        {
            // başka kullanıcı profilini aç (salt-okunur)
            if (userId <= 0)
            {
                return;
            }

            if (userId == UserSession.UserId)
            {
                ShowProfileView();
                return;
            }

            _viewedProfileUserId = userId;

            StopTrailer();
            popCartMenu.IsOpen = false;
            popFriendsMenu.IsOpen = false;
            _isLibraryViewActive = false;
            _isDownloadsViewActive = false;
            _isInstallViewActive = false;
            _isWishlistViewActive = false;

            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            WishlistContentPanel.Visibility = Visibility.Collapsed;
            FriendsContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            InstallContentPanel.Visibility = Visibility.Collapsed;
            ProfileContentPanel.Visibility = Visibility.Visible;

            ClearSidebarSelection();
            RefreshProfilePage();
        }

        // aksiyon menüsünü aç
        private void ProfileActionsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            BuildProfileActionsMenu();
            popProfileActionsMenu.IsOpen = true;
        }

        // menü öğelerini oluştur
        private void BuildProfileActionsMenu()
        {
            ProfileActionsMenuContent.Children.Clear();

            // ziyaretçi kontrolü
            bool isVisitor = _viewedProfileUserId.HasValue && _viewedProfileUserId.Value != UserSession.UserId;
            if (isVisitor)
            {
                switch (_visitorRelationship)
                {
                    // iliski yok -> arkadas ekle
                    case FriendshipRelationshipStatus.None:
                        ProfileActionsMenuContent.Children.Add(CreateProfileMenuItem("\uE8FA", "Arkadaş Ekle", "#82E4B0", ProfileMenuSendRequest_Click));
                        break;

                    // giden istek -> iptal et
                    case FriendshipRelationshipStatus.PendingSent:
                        ProfileActionsMenuContent.Children.Add(CreateProfileMenuItem("\uE711", "İsteği İptal Et", "#E0555F", ProfileMenuCancelRequest_Click));
                        break;

                    // gelen istek -> kabul / reddet
                    case FriendshipRelationshipStatus.PendingReceived:
                        ProfileActionsMenuContent.Children.Add(CreateProfileMenuItem("\uE73E", "İsteği Kabul Et", "#82E4B0", ProfileMenuAcceptRequest_Click));
                        ProfileActionsMenuContent.Children.Add(CreateProfileMenuItem("\uE711", "İsteği Reddet", "#E0555F", ProfileMenuRejectRequest_Click));
                        break;

                    // arkadaşlıktan çıkar
                    case FriendshipRelationshipStatus.Friends:
                        ProfileActionsMenuContent.Children.Add(CreateProfileMenuItem("\uE8BB", "Arkadaşı Kaldır", "#E0555F", ProfileMenuRemoveFriend_Click));
                        break;
                }
                return;
            }

            // kendi profili -> duzenle
            ProfileActionsMenuContent.Children.Add(CreateProfileMenuItem("\uE70F", "Profili Düzenle", "#F4F7FB", ProfileMenuEdit_Click));
        }

        // menü öğesi oluşturucu
        private Button CreateProfileMenuItem(string icon, string text, string accentHex, RoutedEventHandler handler)
        {
            Button button = new Button
            {
                Style = (Style)FindResource("PopupMenuButton"),
                Height = 40,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // ikon ve metin paneli
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };

            // ikon (vurgu renginde)
            TextBlock iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(accentHex)!
            };

            // metin (her zaman beyaz)
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.White
            };

            row.Children.Add(iconBlock);
            row.Children.Add(textBlock);
            button.Content = row;
            button.Click += handler;
            return button;
        }

        // düzenleme moduna geç
        private void ProfileMenuEdit_Click(object sender, RoutedEventArgs e)
        {
            popProfileActionsMenu.IsOpen = false;
            EnterProfileEditMode(true);
        }

        // istek gönder
        private void ProfileMenuSendRequest_Click(object sender, RoutedEventArgs e)
        {
            popProfileActionsMenu.IsOpen = false;
            if (!_viewedProfileUserId.HasValue)
            {
                return;
            }

            int targetUserId = _viewedProfileUserId.Value;
            try
            {
                _friendshipController.SendRequest(UserSession.UserId, targetUserId);
                RefreshProfilePage();
                RefreshFriendsPopup();
                CustomError.ShowDialog("Arkadaşlık isteği gönderildi.", "Arkadaşlık", isSuccess: true, owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        // gönderilen isteği iptal et
        private void ProfileMenuCancelRequest_Click(object sender, RoutedEventArgs e)
        {
            popProfileActionsMenu.IsOpen = false;
            if (!_viewedProfileUserId.HasValue)
            {
                return;
            }

            if (!CustomConfirm.ShowDialog("Arkadaşlık İsteği", "Gönderdiğin arkadaşlık isteğini iptal etmek istediğine emin misin?", "İptal Et", owner: this))
            {
                return;
            }

            int targetUserId = _viewedProfileUserId.Value;
            try
            {
                _friendshipController.CancelOutgoingRequest(UserSession.UserId, targetUserId);
                RefreshProfilePage();
                RefreshFriendsPopup();
                CustomError.ShowDialog("Arkadaşlık isteği iptal edildi.", "Arkadaşlık", isSuccess: true, owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        // gelen istegi kabul et
        private void ProfileMenuAcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            popProfileActionsMenu.IsOpen = false;
            if (!_viewedProfileUserId.HasValue)
            {
                return;
            }

            int targetUserId = _viewedProfileUserId.Value;
            try
            {
                _friendshipController.AcceptRequest(UserSession.UserId, targetUserId);
                RefreshProfilePage();
                RefreshFriendsPopup();
                CustomError.ShowDialog("Artık arkadaşsınız.", "Arkadaşlık", isSuccess: true, owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        // isteği reddet
        private void ProfileMenuRejectRequest_Click(object sender, RoutedEventArgs e)
        {
            popProfileActionsMenu.IsOpen = false;
            if (!_viewedProfileUserId.HasValue)
            {
                return;
            }

            if (!CustomConfirm.ShowDialog("Arkadaşlık İsteği", "Gelen arkadaşlık isteğini reddetmek istediğine emin misin?", "Reddet", owner: this))
            {
                return;
            }

            int targetUserId = _viewedProfileUserId.Value;
            try
            {
                _friendshipController.RejectRequest(UserSession.UserId, targetUserId);
                RefreshProfilePage();
                RefreshFriendsPopup();
                CustomError.ShowDialog("Arkadaşlık isteği reddedildi.", "Arkadaşlık", isSuccess: true, owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }

        // arkadaşlıktan çıkar
        private void ProfileMenuRemoveFriend_Click(object sender, RoutedEventArgs e)
        {
            popProfileActionsMenu.IsOpen = false;
            if (!_viewedProfileUserId.HasValue)
            {
                return;
            }

            if (!CustomConfirm.ShowDialog("Arkadaşı Kaldır", "Bu kişiyi arkadaş listenden kaldırmak istediğine emin misin?", "Kaldır", owner: this))
            {
                return;
            }

            int targetUserId = _viewedProfileUserId.Value;
            try
            {
                _friendshipController.RemoveFriend(UserSession.UserId, targetUserId);
                RefreshProfilePage();
                RefreshFriendsPopup();
                CustomError.ShowDialog("Kişi arkadaş listenden kaldırıldı.", "Arkadaşlık", isSuccess: true, owner: this);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog(ex.Message, "Bilgi", owner: this);
            }
        }
    }
}
