// Zen Clip - Akıllı Pano Yöneticisi
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Collections.Generic;

namespace ZenClip
{
    public enum ClipType
    {
        Text,
        Url,
        ColorHex,
        FilePath,
        CodeSnippet
    }

    public class ClipItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public ClipType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsPinned { get; set; }
        public string ColorHex { get; set; }

        public ClipItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Timestamp = DateTime.Now;
        }

        public string GetTimeAgo()
        {
            TimeSpan diff = DateTime.Now - Timestamp;
            if (diff.TotalSeconds < 60) return "Az önce";
            if (diff.TotalMinutes < 60) return string.Format("{0} dk önce", (int)diff.TotalMinutes);
            if (diff.TotalHours < 24) return string.Format("{0} sa önce", (int)diff.TotalHours);
            return Timestamp.ToString("dd.MM.yyyy HH:mm");
        }

        public string GetBadgeText()
        {
            switch (Type)
            {
                case ClipType.Url: return "🔗 Link";
                case ClipType.ColorHex: return "🎨 Renk";
                case ClipType.FilePath: return "📁 Dosya";
                case ClipType.CodeSnippet: return "💻 Kod";
                default: return "📝 Metin";
            }
        }
    }

    public class ClipConfig
    {
        public string AppName { get; set; }
        public string Creator { get; set; }
        public string Copyright { get; set; }
        public int MaxItems { get; set; }
        public bool AutoPasteOnSelect { get; set; }
        public bool AcrylicEnabled { get; set; }
        public bool SoundFeedback { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public List<ClipItem> Items { get; set; }

        public ClipConfig()
        {
            AppName = "Zen Clip - Akıllı Pano Yöneticisi";
            Creator = "Serkan ŞENTÜRK";
            Copyright = "© 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.";
            MaxItems = 150;
            AutoPasteOnSelect = true;
            AcrylicEnabled = true;
            SoundFeedback = true;
            WindowWidth = 440;
            WindowHeight = 540;
            Items = new List<ClipItem>();
        }
    }
}
