// Zen Clip - Akıllı Pano Yöneticisi
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace ZenClip
{
    public class App : System.Windows.Application
    {
        private static Mutex _mutex;
        private NotifyIcon _trayIcon;
        private ClipConfig _config;
        private ClipWindow _clipWindow;
        private HwndSource _msgWindow;
        private const int HOTKEY_ID = 9001;

        [STAThread]
        public static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "ZenClip_SingleInstance_Mutex_SerkanSenturk_2026", out createdNew);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    "Zen Clip zaten arka planda çalışıyor!\nEkranın sağ altındaki bildirim alanından kontrol edebilir veya Ctrl+Shift+V kısayolunu kullanabilirsiniz.\n\nGeliştirici: Serkan ŞENTÜRK",
                    "Zen Clip - Akıllı Pano",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            App app = new App();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load Config
            _config = ConfigManager.LoadConfig();

            // Create hidden message window for Hotkey & Clipboard
            CreateMessageWindow();

            // Setup Clip Window
            _clipWindow = new ClipWindow(
                _config,
                onItemSelected: (item) => { },
                onConfigChanged: () => ConfigManager.SaveConfig(_config)
            );

            // Setup Tray Icon
            SetupTrayIcon();

            // Periodic working set trim for ultra low RAM (~2 MB)
            System.Windows.Threading.DispatcherTimer trimTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            trimTimer.Tick += (s, ev) =>
            {
                try
                {
                    NativeMethods.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
                catch { }
            };
            trimTimer.Start();
        }

        private void CreateMessageWindow()
        {
            HwndSourceParameters p = new HwndSourceParameters("ZenClipMessageWindow")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0
            };
            _msgWindow = new HwndSource(p);
            _msgWindow.AddHook(WndProc);

            IntPtr handle = _msgWindow.Handle;

            // Register HotKey Ctrl + Shift + V (VK_V = 0x56)
            NativeMethods.RegisterHotKey(handle, HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, 0x56);

            // Add Clipboard Listener
            NativeMethods.AddClipboardFormatListener(handle);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                if (wParam.ToInt32() == HOTKEY_ID)
                {
                    OnHotKeyTriggered();
                    handled = true;
                }
            }
            else if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdated();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void OnHotKeyTriggered()
        {
            IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();

            if (_clipWindow.IsVisible)
            {
                _clipWindow.Hide();
            }
            else
            {
                _clipWindow.ShowWithHwnd(foregroundHwnd);
            }
        }

        private void OnClipboardUpdated()
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText()) return;

                string text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                text = text.Trim();

                // If identical to most recent item, just bump timestamp
                if (_config.Items.Count > 0 && _config.Items[0].Text == text)
                {
                    _config.Items[0].Timestamp = DateTime.Now;
                    return;
                }

                // Remove previous duplicate if not pinned
                _config.Items.RemoveAll(i => !i.IsPinned && i.Text == text);

                ClipItem item = new ClipItem
                {
                    Text = text,
                    Timestamp = DateTime.Now,
                    Type = DetectType(text)
                };

                if (item.Type == ClipType.ColorHex)
                {
                    item.ColorHex = ExtractHexColor(text);
                }

                _config.Items.Insert(0, item);

                // Enforce max count
                while (_config.Items.Count > _config.MaxItems)
                {
                    int lastUnpinned = _config.Items.FindLastIndex(i => !i.IsPinned);
                    if (lastUnpinned >= 0)
                        _config.Items.RemoveAt(lastUnpinned);
                    else
                        break;
                }

                ConfigManager.SaveConfig(_config);

                if (_clipWindow != null && _clipWindow.IsVisible)
                {
                    _clipWindow.RefreshList();
                }
            }
            catch { }
        }

        private ClipType DetectType(string text)
        {
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                return ClipType.Url;

            if (Regex.IsMatch(text, @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
                return ClipType.ColorHex;

            if ((text.Length < 260 && (text.Contains(":\\") || text.StartsWith("\\\\"))) && (File.Exists(text) || Directory.Exists(text)))
                return ClipType.FilePath;

            if (text.Contains("{") && text.Contains("}") || text.Contains("function ") || text.Contains("public class ") || text.Contains("def ") || text.Contains("=>") || text.Contains("console.log") || text.Contains("import "))
                return ClipType.CodeSnippet;

            return ClipType.Text;
        }

        private string ExtractHexColor(string text)
        {
            Match m = Regex.Match(text, @"#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})");
            return m.Success ? m.Value : null;
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Zen Clip - Akıllı Pano | Serkan ŞENTÜRK";
            _trayIcon.Icon = LoadAppIcon();
            _trayIcon.Visible = true;

            _trayIcon.DoubleClick += (s, e) => OnHotKeyTriggered();

            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem titleItem = new ToolStripMenuItem("☯️ Zen Clip - Akıllı Pano");
            titleItem.Font = new Font(titleItem.Font, System.Drawing.FontStyle.Bold);
            titleItem.Enabled = false;
            menu.Items.Add(titleItem);

            ToolStripMenuItem authorItem = new ToolStripMenuItem("👤 Geliştirici: Serkan ŞENTÜRK");
            authorItem.Font = new Font(authorItem.Font.FontFamily, 8.5f, System.Drawing.FontStyle.Italic);
            authorItem.Enabled = false;
            menu.Items.Add(authorItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem openItem = new ToolStripMenuItem("📋 Panoyu Aç (Ctrl+Shift+V)");
            openItem.Click += (s, e) => OnHotKeyTriggered();
            menu.Items.Add(openItem);

            ToolStripMenuItem clearItem = new ToolStripMenuItem("🧹 Panoyu Temizle");
            clearItem.Click += (s, e) =>
            {
                _config.Items.RemoveAll(i => !i.IsPinned);
                ConfigManager.SaveConfig(_config);
                if (_clipWindow != null) _clipWindow.RefreshList();
            };
            menu.Items.Add(clearItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem devProfileItem = new ToolStripMenuItem("🌐 Geliştirici Sayfası (GitHub)");
            devProfileItem.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/836982756578") { UseShellExecute = true }); } catch { }
            };
            menu.Items.Add(devProfileItem);

            ToolStripMenuItem projectRepoItem = new ToolStripMenuItem("⭐ Zen Clip GitHub");
            projectRepoItem.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/836982756578/Zen-Clip") { UseShellExecute = true }); } catch { }
            };
            menu.Items.Add(projectRepoItem);

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("ℹ️ Zen Clip Hakkında");
            aboutItem.Click += (s, e) =>
            {
                System.Windows.MessageBox.Show(
                    "Zen Clip - Akıllı Pano Yöneticisi v1.0.0\n\n" +
                    "🌟 Geliştirici: Serkan ŞENTÜRK\n" +
                    "Telif Hakkı: © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.\n\n" +
                    "Kopyaladığınız tüm metinleri, linkleri, renk kodlarını ve dosyaları hafızada tutar.\n" +
                    "Ctrl + Shift + V kısayoluyla istediğiniz an çağırabilirsiniz.\n\n" +
                    "• 100% Bağımsız ve Taşınabilir (Zero-Install Portable)\n" +
                    "• Tek Tıkla Yapıştırma ve Canlı Renk Önizleme\n" +
                    "• Akrilik Şeffaf Tasarım ve Ultra Düşük Bellek (~3 MB)\n\n" +
                    "GitHub: https://github.com/836982756578/Zen-Clip",
                    "Zen Clip Hakkında",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };
            menu.Items.Add(aboutItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("🚪 Çıkış");
            exitItem.Click += (s, e) => ExitApplication();
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
        }

        private Icon LoadAppIcon()
        {
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    return new Icon(icoPath, SystemInformation.SmallIconSize);
                }
                return Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void ExitApplication()
        {
            if (_msgWindow != null)
            {
                NativeMethods.UnregisterHotKey(_msgWindow.Handle, HOTKEY_ID);
                NativeMethods.RemoveClipboardFormatListener(_msgWindow.Handle);
                _msgWindow.Dispose();
                _msgWindow = null;
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }

            Shutdown();
        }
    }
}
