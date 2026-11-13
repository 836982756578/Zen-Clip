// Zen Clip - Akıllı Pano Yöneticisi
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ZenClip
{
    public class ClipWindow : Window
    {
        private readonly ClipConfig _config;
        private readonly Action<ClipItem> _onItemSelected;
        private readonly Action _onConfigChanged;

        private TextBox _searchBox;
        private StackPanel _itemsPanel;
        private ScrollViewer _scrollViewer;
        private TextBlock _statusText;
        private string _activeFilter = "All";
        private IntPtr _lastActiveHwnd = IntPtr.Zero;

        public ClipWindow(ClipConfig config, Action<ClipItem> onItemSelected, Action onConfigChanged)
        {
            _config = config;
            _onItemSelected = onItemSelected;
            _onConfigChanged = onConfigChanged;

            Title = "Zen Clip";
            Width = _config.WindowWidth > 0 ? _config.WindowWidth : 440;
            Height = _config.WindowHeight > 0 ? _config.WindowHeight : 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;

            BuildUI();

            Loaded += (s, e) =>
            {
                if (_config.AcrylicEnabled)
                {
                    NativeMethods.EnableAcrylic(this, 0xD01E2028);
                }
                _searchBox.Focus();
                RefreshList();
            };

            Deactivated += (s, e) =>
            {
                Hide();
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Hide();
                }
            };
        }

        public void ShowWithHwnd(IntPtr targetHwnd)
        {
            _lastActiveHwnd = targetHwnd;

            // Position near cursor or center
            Point cursorPos = GetCursorPos();
            Left = Math.Max(20, Math.Min(SystemParameters.PrimaryScreenWidth - Width - 20, cursorPos.X - (Width / 2)));
            Top = Math.Max(20, Math.Min(SystemParameters.PrimaryScreenHeight - Height - 20, cursorPos.Y - (Height / 2)));

            Show();
            Activate();
            _searchBox.Text = "";
            _searchBox.Focus();
            RefreshList();
        }

        private Point GetCursorPos()
        {
            try
            {
                System.Drawing.Point p = System.Windows.Forms.Cursor.Position;
                return new Point(p.X, p.Y);
            }
            catch
            {
                return new Point(SystemParameters.PrimaryScreenWidth / 2, SystemParameters.PrimaryScreenHeight / 2);
            }
        }

        private void BuildUI()
        {
            Border rootBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 22, 24, 32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 26,
                    ShadowDepth = 6,
                    Opacity = 0.65
                }
            };

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter Chips
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Cards List
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. Header (Icon + Title + Drag Area + Close)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock logo = new TextBlock
            {
                Text = "☯️",
                FontSize = 18,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(logo, 0);

            TextBlock title = new TextBlock
            {
                Text = "Zen Clip - Akıllı Pano",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);

            headerGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            Button closeBtn = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 185, 200)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => Hide();
            Grid.SetColumn(closeBtn, 2);

            headerGrid.Children.Add(logo);
            headerGrid.Children.Add(title);
            headerGrid.Children.Add(closeBtn);
            Grid.SetRow(headerGrid, 0);
            mainGrid.Children.Add(headerGrid);

            // 2. Search Box
            Border searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8)
            };

            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(searchIcon, 0);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (s, e) => RefreshList();
            Grid.SetColumn(_searchBox, 1);

            searchGrid.Children.Add(searchIcon);
            searchGrid.Children.Add(_searchBox);
            searchBorder.Child = searchGrid;
            Grid.SetRow(searchBorder, 1);
            mainGrid.Children.Add(searchBorder);

            // 3. Filter Chips Row
            WrapPanel chipsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            AddFilterChip(chipsPanel, "Tümü", "All", true);
            AddFilterChip(chipsPanel, "⭐ Sabit", "Pinned", false);
            AddFilterChip(chipsPanel, "📝 Metin", "Text", false);
            AddFilterChip(chipsPanel, "🔗 Link", "Url", false);
            AddFilterChip(chipsPanel, "🎨 Renk", "Color", false);
            AddFilterChip(chipsPanel, "📁 Dosya", "File", false);
            Grid.SetRow(chipsPanel, 2);
            mainGrid.Children.Add(chipsPanel);

            // 4. Cards Scrollable List
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _itemsPanel = new StackPanel();
            _scrollViewer.Content = _itemsPanel;
            Grid.SetRow(_scrollViewer, 3);
            mainGrid.Children.Add(_scrollViewer);

            // 5. Footer (Status info + Clear button + About)
            Grid footerGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusText = new TextBlock
            {
                Text = "Kısayol: Ctrl+Shift+V  •  Serkan ŞENTÜRK",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 160)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_statusText, 0);

            Button clearBtn = new Button
            {
                Content = "🧹 Temizle",
                FontSize = 10.5,
                Padding = new Thickness(8, 3, 8, 3),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 150, 150)),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            clearBtn.Click += (s, e) =>
            {
                MessageBoxResult res = MessageBox.Show(
                    "Sabitlenmemiş tüm pano geçmişini silmek istediğinize emin misiniz?",
                    "Zen Clip - Geçmişi Temizle",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    _config.Items.RemoveAll(i => !i.IsPinned);
                    _onConfigChanged();
                    RefreshList();
                }
            };
            Grid.SetColumn(clearBtn, 1);

            Button aboutBtn = new Button
            {
                Content = "ℹ️",
                FontSize = 11,
                Width = 24,
                Height = 24,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            aboutBtn.Click += (s, e) =>
            {
                MessageBox.Show(
                    "Zen Clip - Akıllı Pano Yöneticisi v1.0.0\n\n" +
                    "🌟 Geliştirici: Serkan ŞENTÜRK\n" +
                    "Telif Hakkı: © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.\n\n" +
                    "• Kısayol: Ctrl + Shift + V\n" +
                    "• Otomatik Link, Renk Kodu, Kod ve Metin Algılama\n" +
                    "• Sabitleme ve Canlı Arama Desteği\n" +
                    "• Ultra Hafif ve Taşınabilir (Zero-Install Portable)\n\n" +
                    "GitHub: https://github.com/836982756578/Zen-Clip",
                    "Zen Clip Hakkında",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };
            Grid.SetColumn(aboutBtn, 2);

            footerGrid.Children.Add(_statusText);
            footerGrid.Children.Add(clearBtn);
            footerGrid.Children.Add(aboutBtn);
            Grid.SetRow(footerGrid, 4);
            mainGrid.Children.Add(footerGrid);

            rootBorder.Child = mainGrid;
            Content = rootBorder;
        }

        private void AddFilterChip(WrapPanel panel, string label, string filterKey, bool isDefault)
        {
            Button btn = new Button
            {
                Content = label,
                FontSize = 11,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 5, 4),
                Foreground = isDefault ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 175, 190)),
                Background = isDefault ? new SolidColorBrush(Color.FromRgb(40, 95, 200)) : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            btn.Click += (s, e) =>
            {
                _activeFilter = filterKey;
                foreach (UIElement child in panel.Children)
                {
                    Button b = child as Button;
                    if (b != null)
                    {
                        b.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                        b.Foreground = new SolidColorBrush(Color.FromRgb(170, 175, 190));
                    }
                }
                btn.Background = new SolidColorBrush(Color.FromRgb(40, 95, 200));
                btn.Foreground = Brushes.White;
                RefreshList();
            };

            panel.Children.Add(btn);
        }

        public void RefreshList()
        {
            _itemsPanel.Children.Clear();

            string query = (_searchBox.Text ?? "").Trim().ToLower();

            List<ClipItem> filtered = new List<ClipItem>();
            foreach (ClipItem item in _config.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.Text)) continue;

                // Category filter
                if (_activeFilter == "Pinned" && !item.IsPinned) continue;
                if (_activeFilter == "Text" && item.Type != ClipType.Text && item.Type != ClipType.CodeSnippet) continue;
                if (_activeFilter == "Url" && item.Type != ClipType.Url) continue;
                if (_activeFilter == "Color" && item.Type != ClipType.ColorHex) continue;
                if (_activeFilter == "File" && item.Type != ClipType.FilePath) continue;

                // Search query filter
                if (!string.IsNullOrEmpty(query) && !item.Text.ToLower().Contains(query))
                    continue;

                filtered.Add(item);
            }

            if (filtered.Count == 0)
            {
                TextBlock emptyNotice = new TextBlock
                {
                    Text = string.IsNullOrEmpty(query) ? "Pano geçmişiniz henüz boş.\nBir şeyler kopyaladığınızda burada görünecektir." : "Aramanızla eşleşen öğe bulunamadı.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 160)),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 40)
                };
                _itemsPanel.Children.Add(emptyNotice);
                _statusText.Text = "0 öğe";
                return;
            }

            _statusText.Text = string.Format("{0} öğe listeleniyor", filtered.Count);

            foreach (ClipItem item in filtered)
            {
                Border card = CreateClipCard(item);
                _itemsPanel.Children.Add(card);
            }
        }

        private Border CreateClipCard(ClipItem item)
        {
            Border card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                BorderBrush = item.IsPinned ? new SolidColorBrush(Color.FromRgb(255, 195, 0)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                card.BorderBrush = item.IsPinned ? new SolidColorBrush(Color.FromRgb(255, 215, 0)) : new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            };

            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
                card.BorderBrush = item.IsPinned ? new SolidColorBrush(Color.FromRgb(255, 195, 0)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            };

            Grid cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Card Top Row (Badge + Color Swatch + Time Ago + Pin + Delete)
            Grid topRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock badge = new TextBlock
            {
                Text = item.GetBadgeText(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 195, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(badge, 0);

            // Color Swatch Box if ColorHex
            if (item.Type == ClipType.ColorHex && !string.IsNullOrEmpty(item.ColorHex))
            {
                try
                {
                    Color c = (Color)ColorConverter.ConvertFromString(item.ColorHex);
                    Border swatch = new Border
                    {
                        Width = 14,
                        Height = 14,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(c),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(swatch, 1);
                    topRow.Children.Add(swatch);
                }
                catch { }
            }

            TextBlock timeAgo = new TextBlock
            {
                Text = item.GetTimeAgo(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 135, 150)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeAgo, 2);

            // Pin Button ⭐
            Button pinBtn = new Button
            {
                Content = item.IsPinned ? "★" : "☆",
                FontSize = 12,
                Foreground = item.IsPinned ? new SolidColorBrush(Color.FromRgb(255, 215, 0)) : new SolidColorBrush(Color.FromRgb(140, 145, 160)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0)
            };
            pinBtn.Click += (s, e) =>
            {
                e.Handled = true;
                item.IsPinned = !item.IsPinned;
                _onConfigChanged();
                RefreshList();
            };
            Grid.SetColumn(pinBtn, 3);

            // Delete Button ✕
            Button delBtn = new Button
            {
                Content = "✕",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 130, 130)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            delBtn.Click += (s, e) =>
            {
                e.Handled = true;
                _config.Items.Remove(item);
                _onConfigChanged();
                RefreshList();
            };
            Grid.SetColumn(delBtn, 4);

            topRow.Children.Add(badge);
            topRow.Children.Add(timeAgo);
            topRow.Children.Add(pinBtn);
            topRow.Children.Add(delBtn);
            Grid.SetRow(topRow, 0);
            cardGrid.Children.Add(topRow);

            // Card Text Content (Max 3 lines, ellipsis)
            string displayPreview = item.Text;
            if (displayPreview.Length > 200)
            {
                displayPreview = displayPreview.Substring(0, 200) + "...";
            }

            TextBlock textContent = new TextBlock
            {
                Text = displayPreview,
                FontSize = 12,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 48,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = item.Type == ClipType.CodeSnippet ? new FontFamily("Consolas, Courier New") : new FontFamily("Segoe UI")
            };
            Grid.SetRow(textContent, 1);
            cardGrid.Children.Add(textContent);

            card.Child = cardGrid;

            // Card Click Event -> Paste / Copy
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 1)
                {
                    Hide();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(item.Text);

                            if (_config.AutoPasteOnSelect && _lastActiveHwnd != IntPtr.Zero)
                            {
                                NativeMethods.SetForegroundWindow(_lastActiveHwnd);
                                System.Threading.Thread.Sleep(50);
                                NativeMethods.SimulateCtrlV();
                            }
                        }
                        catch { }
                    }), DispatcherPriority.Background);
                }
            };

            return card;
        }
    }
}
