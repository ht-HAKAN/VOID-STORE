using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class MainAppWindow : Window
    {
        private static readonly Brush DefaultCardBorderBrush = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x1D));
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
        private StoreGameDetail? _currentDetail;
        private Button? _activeSidebarButton;
        private int _currentPage = 1;
        private string _selectedCategory = "Tümü";
        private readonly DispatcherTimer _trailerProgressTimer;
        private readonly DispatcherTimer _trailerOverlayTimer;
        private List<StoreGameCardItem> _storeItems = new();
        private bool _isTrailerSeekActive;
        private bool _isTrailerPlaying;
        private bool _isTrailerProgressUpdating;
        private bool _isTrailerMuted;
        private bool _isTrailerPlayerReady;
        private bool _isTrailerVolumeUpdating;
        private int _storeGridColumns = 4;
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

            EnsureSession();
            ConfigureProfileArea();
            BuildCategories();
            UpdateSearchPlaceholder();
            ShowStoreView();
            LoadStorePage();
            UpdateWindowGlyph();
            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
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
                            ? messageElement.GetString() ?? "Fragman videosu acilamadi."
                            : "Fragman videosu acilamadi.";
                        _trailerProgressTimer.Stop();
                        _isTrailerPlaying = false;
                        CustomError.ShowDialog("Fragman videosu acilamadi: " + message, "SISTEM HATASI", owner: this);
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
            // oturum bilgisi yoksa misafir olarak baslat
            if (string.IsNullOrWhiteSpace(UserSession.DisplayName))
            {
                UserSession.SetGuest();
            }
        }

        private void ConfigureProfileArea()
        {
            // profil dairesindeki gosterimi hazirla
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

            if (!string.IsNullOrWhiteSpace(UserSession.ProfileImagePath) && File.Exists(UserSession.ProfileImagePath))
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(UserSession.ProfileImagePath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                imgProfile.Source = image;
                imgProfile.Visibility = Visibility.Visible;
                txtProfileInitial.Visibility = Visibility.Collapsed;
            }
            else
            {
                imgProfile.Source = null;
                imgProfile.Visibility = Visibility.Collapsed;
                txtProfileInitial.Visibility = Visibility.Visible;
            }
        }

        private void BuildCategories()
        {
            // kategori secim satirini doldur
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
            // kategori chiplerini yenile
            icCategories.ItemsSource = null;
            icCategories.ItemsSource = _categories;
        }

        private void LoadStorePage()
        {
            // magazadaki oyunlari sayfalama ile getir
            StoreGamePageResult result = _storeController.GetGames(txtSearch.Text, _selectedCategory, _currentPage);
            _currentPage = result.CurrentPage;

            _storeItems = result.Items.ToList();
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
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Visible;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            SetSidebarSelection(btnStoreNav);
            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
        }

        private void UpdateStoreGridColumns()
        {
            // kartlari esit tutup seridi ortala
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
                _storeGridColumns = 1;
                icStoreGames.Width = double.NaN;
                return;
            }

            const double storeCardOuterWidth = 254;
            int maxColumns = Math.Max(1, (int)Math.Floor(viewportWidth / storeCardOuterWidth));
            maxColumns = Math.Min(maxColumns, _storeItems.Count);

            int bestColumns = 1;
            int bestRows = int.MaxValue;
            double bestFillRatio = -1;

            for (int candidate = 1; candidate <= maxColumns; candidate++)
            {
                int rowCount = (int)Math.Ceiling(_storeItems.Count / (double)candidate);
                int lastRowCount = _storeItems.Count % candidate;

                if (lastRowCount == 0)
                {
                    lastRowCount = candidate;
                }

                double fillRatio = lastRowCount / (double)candidate;

                if (rowCount < bestRows || (rowCount == bestRows && fillRatio > bestFillRatio))
                {
                    bestColumns = candidate;
                    bestRows = rowCount;
                    bestFillRatio = fillRatio;
                }
            }

            _storeGridColumns = bestColumns;
            icStoreGames.Width = _storeGridColumns * storeCardOuterWidth;
        }

        private void ShowDetailView(StoreGameDetail detail)
        {
            // secilen oyunun detaylarini goster
            detail.MediaItems = detail.MediaItems
                .OrderByDescending(item => item.IsTrailer)
                .ToList();

            _currentDetail = detail;

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
            DetailHeaderPanel.Visibility = Visibility.Visible;
            DetailContentPanel.Visibility = Visibility.Visible;
            SetSidebarSelection(btnStoreNav);
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
                ShowDetailView(detail);
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Oyun sayfası açılamadı: " + ex.Message, "SISTEM HATASI", owner: this);
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
            // detaydan magazaya don
            StopTrailer();
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
                    CustomError.ShowDialog("Fragman videosu bulunamadı.", "SISTEM HATASI", owner: this);
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
                CustomError.ShowDialog("Fragman videosu açılamadı: " + ex.Message, "SISTEM HATASI", owner: this);
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

        private void StoreNavButton_Click(object sender, RoutedEventArgs e)
        {
            // magazayi tekrar goster
            ShowStoreView();
        }

        private void LibraryNavButton_Click(object sender, RoutedEventArgs e)
        {
            // kutuphane bolumu simdilik gorsel kabuk olarak durur
            SetSidebarSelection(btnStoreNav);
            CustomError.ShowDialog("Kütüphane bölümü henüz hazır değil.", "BILGI", owner: this);
        }

        private void DownloadsNavButton_Click(object sender, RoutedEventArgs e)
        {
            // indirme bolumu simdilik gorsel kabuk olarak durur
            SetSidebarSelection(btnStoreNav);
            CustomError.ShowDialog("İndirmeler bölümü henüz hazır değil.", "BILGI", owner: this);
        }

        private void FriendsButton_Click(object sender, RoutedEventArgs e)
        {
            // arkadas alani simdilik kabuk olarak durur
            CustomError.ShowDialog("Arkadaş alanı henüz hazır değil.", "BILGI", owner: this);
        }

        private void HeaderShellButton_Click(object sender, RoutedEventArgs e)
        {
            // ust menudeki alanlar simdilik yonlendirme amacli kalir
            CustomError.ShowDialog("Bu bölüm henüz hazır değil.", "BILGI", owner: this);
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
                CustomError.ShowDialog("Kayıt olma bölümü henüz hazır değil.", "BILGI", owner: this);
                return;
            }

            CustomError.ShowDialog("Profil düzenleme bölümü henüz hazır değil.", "BILGI", owner: this);
        }

        private void ProfileSecondaryAction_Click(object sender, RoutedEventArgs e)
        {
            // misafir ve oturum acik durumlarina gore ikinci menuyu yonet
            popProfileMenu.IsOpen = false;

            if (UserSession.IsGuest)
            {
                CustomError.ShowDialog("Giriş ekranı henüz hazır değil.", "BILGI", owner: this);
                return;
            }

            CustomError.ShowDialog("Çıkış menüsü henüz hazır değil.", "BILGI", owner: this);
        }

        private void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            // sepet akisi hafta sonrasi icin hazir kalir
            CustomError.ShowDialog("Sepet bölümü henüz hazır değil.", "BILGI", owner: this);
        }

        private void AddToWishlistButton_Click(object sender, RoutedEventArgs e)
        {
            // istek listesi akisi hafta sonrasi icin hazir kalir
            CustomError.ShowDialog("İstek listesi bölümü henüz hazır değil.", "BILGI", owner: this);
        }
    }
}
