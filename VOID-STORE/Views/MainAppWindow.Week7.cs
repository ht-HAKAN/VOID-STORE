using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VOID_STORE.Controllers;
using VOID_STORE.Models;

namespace VOID_STORE.Views
{
    public partial class MainAppWindow
    {
        // ticaret akislarini tek yerden yonet
        private readonly CommerceController _commerceController = new();

        // anlik sepet verisini tut
        private List<CartGameItem> _cartItems = new();

        // sahip olunan oyunlari tut
        private List<LibraryGameItem> _libraryItems = new();

        // son islem gecmisini tut
        private List<WalletTransactionItem> _walletTransactions = new();

        // sahiplik durumunu hizli sorgula
        private HashSet<int> _ownedGameIds = new();

        // sepetteki oyunlari hizli sorgula
        private HashSet<int> _cartGameIds = new();

        // detaydan geri donuste kutuphane akisini koru
        private bool _isLibraryViewActive;

        // secili odeme yontemini sakla
        private string _selectedPaymentMethod = "visa";

        private void InitializeCommerceState()
        {
            // ilk verileri acilista yukle
            RefreshCommerceState(false);
        }

        private void RefreshCommerceState(bool showErrors = true)
        {
            // tum ticaret yuzeylerini ayni anda yenile
            try
            {
                if (UserSession.IsGuest)
                {
                    // misafir icin tum listeleri temizle
                    _cartItems = new List<CartGameItem>();
                    _libraryItems = new List<LibraryGameItem>();
                    _walletTransactions = new List<WalletTransactionItem>();
                    _ownedGameIds = new HashSet<int>();
                    _cartGameIds = new HashSet<int>();
                }
                else
                {
                    // guncel bakiyeyi oturuma yaz
                    decimal balance = _commerceController.GetBalance(UserSession.UserId);
                    UserSession.UpdateBalance(balance);

                    // sayfalarda kullanilacak listeleri cek
                    _cartItems = _commerceController.GetCartItems(UserSession.UserId).ToList();
                    _libraryItems = _commerceController.GetLibraryGames(UserSession.UserId).ToList();
                    _walletTransactions = _commerceController.GetRecentTransactions(UserSession.UserId).ToList();

                    // hizli kontrol setlerini doldur
                    _ownedGameIds = _commerceController.GetOwnedGameIds(UserSession.UserId);
                    _cartGameIds = _commerceController.GetCartGameIds(UserSession.UserId);
                }

                // store ve detay durumlarini guncelle
                ApplyStoreOwnershipState();
                ApplyDetailOwnershipState();

                // alt sayfalari yeni veriyle yenile
                RefreshLibraryPanel();
                RefreshCartPopup();
                RefreshWalletPage();
            }
            catch (Exception ex)
            {
                // hata ekrana kontrollu yansin
                if (showErrors)
                {
                    CustomError.ShowDialog($"Ticaret verileri y\u00fcklenemedi {ex.Message}", "SISTEM HATASI", owner: this);
                }
            }
        }

        private void ApplyStoreOwnershipState()
        {
            // store kartlarinda sahiplik bilgisini yansit
            foreach (StoreGameCardItem item in _storeItems)
            {
                item.IsOwned = _ownedGameIds.Contains(item.GameId);
                item.IsInCart = !item.IsOwned && _cartGameIds.Contains(item.GameId);

                // kart alt durumunu netlestir
                item.StatusText = item.IsOwned
                    ? $"K\u00fct\u00fcphanede"
                    : item.IsInCart
                        ? "Sepette"
                        : string.Empty;
            }

            // items controlu taze veriyle bagla
            icStoreGames.ItemsSource = null;
            icStoreGames.ItemsSource = _storeItems;

            // yerlesimi yeni genislikle kur
            Dispatcher.BeginInvoke(new Action(() => UpdateStoreGridColumns()), DispatcherPriority.Loaded);
        }

        private void ApplyDetailOwnershipState()
        {
            // detay ekraninda tek buton dili kullan
            if (_currentDetail == null)
            {
                return;
            }

            // secili oyunun sahiplik bilgisini guncelle
            _currentDetail.IsOwned = _ownedGameIds.Contains(_currentDetail.GameId);
            _currentDetail.IsInCart = !_currentDetail.IsOwned && _cartGameIds.Contains(_currentDetail.GameId);

            // durum metnini varsayilan olarak gizle
            txtDetailOwnershipState.Visibility = Visibility.Collapsed;

            if (UserSession.IsGuest)
            {
                // misafire acik yonlendirme goster
                txtDetailOwnershipState.Text = $"Sat\u0131n almak i\u00e7in giri\u015f yap";
                txtDetailOwnershipState.Foreground = CreateBrush("#8F98A5");
                txtDetailOwnershipState.Visibility = Visibility.Visible;
                btnDetailAddToCart.Content = "Sepete Ekle";
                btnDetailAddToCart.IsEnabled = true;
                return;
            }

            if (_currentDetail.IsOwned)
            {
                // sahip olunan oyunda satin alma kapansin
                txtDetailOwnershipState.Text = $"Bu oyun k\u00fct\u00fcphanende bulunuyor";
                txtDetailOwnershipState.Foreground = CreateBrush("#82E4B0");
                txtDetailOwnershipState.Visibility = Visibility.Visible;
                btnDetailAddToCart.Content = $"K\u00fct\u00fcphanede";
                btnDetailAddToCart.IsEnabled = false;
                return;
            }

            if (_currentDetail.IsInCart)
            {
                // sepette olan oyunda tekrar ekleme kapansin
                txtDetailOwnershipState.Text = $"Bu oyun sepette bekliyor";
                txtDetailOwnershipState.Foreground = CreateBrush("#F5D174");
                txtDetailOwnershipState.Visibility = Visibility.Visible;
                btnDetailAddToCart.Content = "Sepette";
                btnDetailAddToCart.IsEnabled = false;
                return;
            }

            // satin alinabilir durumda butonu ac
            btnDetailAddToCart.Content = "Sepete Ekle";
            btnDetailAddToCart.IsEnabled = true;
        }

        private void RefreshCartPopup()
        {
            // sepet icerigini yeniden bagla
            icCartItems.ItemsSource = null;
            icCartItems.ItemsSource = _cartItems;

            // toplam tutari tek yerde hesapla
            bool hasItems = _cartItems.Count > 0;
            decimal totalAmount = _cartItems.Sum(item => item.PriceAmount);

            // ust bilgi metnini duruma gore degistir
            txtCartSummary.Text = UserSession.IsGuest
                ? $"Sepet giri\u015f yapt\u0131ktan sonra aktif olur"
                : hasItems
                    ? $"{_cartItems.Count} oyun sat\u0131n almaya haz\u0131r"
                    : $"Hen\u00fcz sepetine oyun eklemedin";

            // toplam tutari goster
            txtCartTotal.Text = FormatMoney(totalAmount);

            // bos ve dolu durumlari ayir
            EmptyCartState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            CartItemsScrollViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;

            // misafir veya bos sepette odemeyi kapat
            btnCheckoutCart.IsEnabled = !UserSession.IsGuest && hasItems;

            // cart badge sayisini yenile
            bdgCartCount.Visibility = !UserSession.IsGuest && hasItems ? Visibility.Visible : Visibility.Collapsed;
            txtCartCount.Text = _cartItems.Count > 99 ? "99+" : _cartItems.Count.ToString();
        }

        private void RefreshWalletPage()
        {
            // ustteki bakiye pillini guncelle
            txtWalletBalance.Text = UserSession.IsGuest ? $"Giri\u015f yap" : FormatMoney(UserSession.Balance);

            // sayfadaki ozet bakiyeyi guncelle
            txtWalletPopupBalance.Text = UserSession.IsGuest
                ? $"Giri\u015f yapman gerekiyor"
                : FormatMoney(UserSession.Balance);

            // cuzdan aciklama satirini sade tut
            txtWalletPageInfo.Text = UserSession.IsGuest
                ? $"C\u00fczdan ve \u00f6deme y\u00f6ntemleri giri\u015ften sonra aktif olur"
                : $"Bakiye y\u00fckleme ve i\u015flem ge\u00e7mi\u015fi burada g\u00f6r\u00fcn\u00fcr";

            // hareket gecmisini listele
            icWalletTransactions.ItemsSource = null;
            icWalletTransactions.ItemsSource = _walletTransactions;

            // bos durum katmanini ayarla
            bool hasTransactions = _walletTransactions.Count > 0;
            EmptyWalletState.Visibility = hasTransactions ? Visibility.Collapsed : Visibility.Visible;
            WalletTransactionsScrollViewer.Visibility = hasTransactions ? Visibility.Visible : Visibility.Collapsed;

            // secili kart kartvizitini yenile
            ApplyPaymentMethodSelection();
        }

        private void RefreshLibraryPanel()
        {
            // kutuphane verisini yeniden bagla
            icLibraryGames.ItemsSource = null;
            icLibraryGames.ItemsSource = _libraryItems;

            if (UserSession.IsGuest)
            {
                // misafir kutuphane mesaji
                txtLibraryResultInfo.Text = $"K\u00fct\u00fcphaneyi g\u00f6rmek i\u00e7in giri\u015f yap";
                txtLibraryEmptyTitle.Text = $"K\u00fct\u00fcphane oturumla a\u00e7\u0131l\u0131r";
                txtLibraryEmptyMessage.Text = $"Sat\u0131n ald\u0131\u011f\u0131n oyunlar burada g\u00f6r\u00fcn\u00fcr";
            }
            else if (_libraryItems.Count == 0)
            {
                // ilk satin alma oncesi bos durum
                txtLibraryResultInfo.Text = $"Hen\u00fcz sahip olunan oyun bulunmuyor";
                txtLibraryEmptyTitle.Text = $"K\u00fct\u00fcphanede oyun g\u00f6r\u00fcnm\u00fcyor";
                txtLibraryEmptyMessage.Text = $"Ma\u011fazadan sat\u0131n ald\u0131\u011f\u0131n oyunlar burada listelenecek";
            }
            else
            {
                // dolu kutuphane sonucu
                txtLibraryResultInfo.Text = _libraryItems.Count == 1
                    ? $"1 oyun k\u00fct\u00fcphanede bulunuyor"
                    : $"{_libraryItems.Count} oyun k\u00fct\u00fcphanede bulunuyor";
                txtLibraryEmptyTitle.Text = $"K\u00fct\u00fcphanede oyun g\u00f6r\u00fcnm\u00fcyor";
                txtLibraryEmptyMessage.Text = $"Ma\u011fazadan sat\u0131n ald\u0131\u011f\u0131n oyunlar burada listelenecek";
            }

            // bos kutuphane durumunu ayarla
            EmptyLibraryState.Visibility = _libraryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // soldan akan yerlesimi yeniden hesapla
            Dispatcher.BeginInvoke(new Action(() => UpdateLibraryGridColumns()), DispatcherPriority.Loaded);
        }

        private void UpdateLibraryGridColumns()
        {
            // kutuphane kartlarini sola akit
            double viewportWidth = LibraryGamesScrollViewer.ViewportWidth;

            // viewport yoksa gercek genisligi kullan
            if (viewportWidth <= 0)
            {
                viewportWidth = LibraryGamesScrollViewer.ActualWidth;
            }

            // halen genislik yoksa cik
            if (viewportWidth <= 0)
            {
                return;
            }

            // veri yoksa paneli serbest birak
            if (_libraryItems.Count == 0)
            {
                icLibraryGames.Width = double.NaN;
                return;
            }

            // wrap alani sola yaslanacak kadar genis olsun
            icLibraryGames.Width = Math.Max(0, viewportWidth - 4);
        }

        private void ShowLibraryView()
        {
            // kutuphane ekranina gec
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = true;

            // sadece ilgili panelleri ac
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Visible;
            WalletContentPanel.Visibility = Visibility.Collapsed;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;

            // sidebar secimini kutuphane yap
            SetSidebarSelection(btnLibraryNav);
            RefreshLibraryPanel();
        }

        private void ShowWalletView()
        {
            // cuzdan ekranina gec
            StopTrailer();
            popCartMenu.IsOpen = false;
            _isLibraryViewActive = false;

            // sadece cuzdan panelini ac
            StoreHeaderPanel.Visibility = Visibility.Visible;
            StoreContentPanel.Visibility = Visibility.Collapsed;
            LibraryContentPanel.Visibility = Visibility.Collapsed;
            WalletContentPanel.Visibility = Visibility.Visible;
            DetailHeaderPanel.Visibility = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Collapsed;

            // sidebar mevcut store secimini koru
            SetSidebarSelection(btnStoreNav);
            RefreshWalletPage();
        }

        private bool EnsureAuthenticatedForCommerce()
        {
            // misafir ticaret akislarini kapat
            if (!UserSession.IsGuest)
            {
                return true;
            }

            // acik popup kalmasin
            popCartMenu.IsOpen = false;

            // net yonlendirme goster
            CustomError.ShowDialog($"Bu i\u015flem i\u00e7in giri\u015f yapman\u0131z gerekiyor", "BILGI", owner: this);
            return false;
        }

        private void WalletMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // header bakiye alanindan sayfaya git
            ShowWalletView();
        }

        private void CartMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // sepet popup durumunu tersine cevir
            RefreshCartPopup();
            popCartMenu.IsOpen = !popCartMenu.IsOpen;
        }

        private void WalletQuickAmountButton_Click(object sender, RoutedEventArgs e)
        {
            // hazir tutari tek tikla yukle
            if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int amount))
            {
                return;
            }

            AddBalance(amount);
        }

        private void AddWalletBalanceButton_Click(object sender, RoutedEventArgs e)
        {
            // ozel girilen tutari yukle
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            string rawValue = txtWalletCustomAmount.Text?.Trim() ?? string.Empty;

            // sayi ve pozitiflik kontrolu yap
            if (!int.TryParse(rawValue, out int amount) || amount <= 0)
            {
                CustomError.ShowDialog($"Ge\u00e7erli bir bakiye tutar\u0131 girin", "BILGI", owner: this);
                return;
            }

            AddBalance(amount);
        }

        private void AddBalance(int amount)
        {
            // bakiye yuklemeyi tek akista yurut
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            // secili kart bilgisini ozetle
            string paymentTitle = GetSelectedPaymentTitle();

            // kullanicidan son onayi al
            if (!CustomConfirm.ShowDialog($"Bakiye Y\u00fckle", $"{FormatMoney(amount)} se\u00e7ili kart ile y\u00fcklensin mi", $"Y\u00fckle", this))
            {
                return;
            }

            try
            {
                // yeni bakiyeyi veritabanindan al
                decimal balanceAfter = _commerceController.AddBalance(UserSession.UserId, amount);
                UserSession.UpdateBalance(balanceAfter);

                // formu temiz tut
                txtWalletCustomAmount.Clear();

                // tum sayfalari tek seferde yenile
                RefreshCommerceState(false);
                ShowWalletView();

                // sonuc bilgisini goster
                CustomError.ShowDialog($"{paymentTitle} ile bakiye g\u00fcncellendi", "BILGI", owner: this);
            }
            catch (Exception ex)
            {
                // hata mesajini ekrana tas
                CustomError.ShowDialog($"Bakiye y\u00fcklenemedi {ex.Message}", "SISTEM HATASI", owner: this);
            }
        }

        private void RemoveCartItemButton_Click(object sender, RoutedEventArgs e)
        {
            // secili oyunu sepetten cikar
            if (sender is not Button button || button.Tag == null)
            {
                return;
            }

            // misafir cikarma yapamasin
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            // game id yoksa islemi durdur
            if (!int.TryParse(button.Tag.ToString(), out int gameId))
            {
                return;
            }

            // kaydi sil ve yuzeyi yenile
            _commerceController.RemoveFromCart(UserSession.UserId, gameId);
            RefreshCommerceState(false);
            ApplyDetailOwnershipState();

            // kullanici akisi bozulmasin
            popCartMenu.IsOpen = true;
        }

        private void CheckoutCartButton_Click(object sender, RoutedEventArgs e)
        {
            // sepet satin alma akisini baslat
            if (!EnsureAuthenticatedForCommerce())
            {
                return;
            }

            // bos sepette ilerleme
            if (_cartItems.Count == 0)
            {
                CustomError.ShowDialog($"Sepette sat\u0131n al\u0131nacak oyun bulunmuyor", "BILGI", owner: this);
                return;
            }

            decimal totalAmount = _cartItems.Sum(item => item.PriceAmount);

            // son onayi kullanicidan al
            if (!CustomConfirm.ShowDialog($"Sat\u0131n Al", $"{_cartItems.Count} oyunu {FormatMoney(totalAmount)} kar\u015f\u0131l\u0131\u011f\u0131nda sat\u0131n almak istiyor musun", $"Sat\u0131n Al", this))
            {
                return;
            }

            try
            {
                // checkout sonucunu uygula
                CheckoutResult result = _commerceController.CheckoutCart(UserSession.UserId);
                UserSession.UpdateBalance(result.BalanceAfter);

                // tum commerce ekranlarini yenile
                RefreshCommerceState(false);
                popCartMenu.IsOpen = false;

                // satin alma sonrasi kutuphaneye git
                ShowLibraryView();
                CustomError.ShowDialog($"{result.ItemCount} oyun k\u00fct\u00fcphanene eklendi", "BILGI", owner: this);
            }
            catch (Exception ex)
            {
                // checkout hatasini goster
                CustomError.ShowDialog($"Sat\u0131n alma tamamlanamad\u0131 {ex.Message}", "SISTEM HATASI", owner: this);
            }
        }

        private void LibraryGamesScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // pencere boyutu degistiginde kutuphaneyi yeniden diz
            UpdateLibraryGridColumns();
        }

        private void PaymentMethodButton_Click(object sender, RoutedEventArgs e)
        {
            // secili karti degistir
            if (sender is not Button button || button.Tag is not string methodKey)
            {
                return;
            }

            // secimi sakla ve yuzeye uygula
            _selectedPaymentMethod = methodKey;
            ApplyPaymentMethodSelection();
        }

        private void ApplyPaymentMethodSelection()
        {
            // buton vurgularini secime gore guncelle
            ApplyPaymentButtonStyle(btnPaymentVisa, _selectedPaymentMethod == "visa", "#2C66F5");
            ApplyPaymentButtonStyle(btnPaymentMaster, _selectedPaymentMethod == "mastercard", "#FF7043");
            ApplyPaymentButtonStyle(btnPaymentTroy, _selectedPaymentMethod == "troy", "#1DBB73");

            // secili kart ozetini yaz
            txtSelectedPaymentTitle.Text = GetSelectedPaymentTitle();
            txtSelectedPaymentNumber.Text = GetSelectedPaymentNumber();
            txtSelectedPaymentExpiry.Text = GetSelectedPaymentExpiry();
        }

        private void ApplyPaymentButtonStyle(Button button, bool isSelected, string accentColor)
        {
            // secili kartta daha belirgin kenarlik kullan
            button.Background = isSelected ? CreateBrush("#151519") : CreateBrush("#101014");
            button.BorderBrush = isSelected ? CreateBrush(accentColor) : CreateBrush("#1E1E24");
        }

        private string GetSelectedPaymentTitle()
        {
            // kart adini tek noktadan ver
            return _selectedPaymentMethod switch
            {
                "mastercard" => "MasterCard",
                "troy" => "Troy",
                _ => "Visa"
            };
        }

        private string GetSelectedPaymentNumber()
        {
            // maskeli numarayi tek noktadan ver
            return _selectedPaymentMethod switch
            {
                "mastercard" => "**** **** **** 5454",
                "troy" => "**** **** **** 9792",
                _ => "**** **** **** 4242"
            };
        }

        private string GetSelectedPaymentExpiry()
        {
            // son kullanma ozetini tek noktadan ver
            return _selectedPaymentMethod switch
            {
                "mastercard" => "Son Kullanma 09 31",
                "troy" => "Son Kullanma 05 32",
                _ => "Son Kullanma 12 30"
            };
        }

        private Brush CreateBrush(string hex)
        {
            // ortak renk uretimi yap
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private string FormatMoney(decimal amount)
        {
            // para formatini tek noktada sabitle
            return $"\u20BA{amount.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))}";
        }
    }
}
