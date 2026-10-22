// Zen Clip - Akıllı Pano Yöneticisi
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ZenClip
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zenclip_config.json");

        public static ClipConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    ClipConfig config = serializer.Deserialize<ClipConfig>(json);
                    if (config != null)
                    {
                        if (config.Items == null) config.Items = new System.Collections.Generic.List<ClipItem>();
                        return config;
                    }
                }
            }
            catch { }

            ClipConfig def = new ClipConfig();
            SaveConfig(def);
            return def;
        }

        public static void SaveConfig(ClipConfig config)
        {
            try
            {
                if (config == null) return;
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string json = serializer.Serialize(config);
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
            catch { }
        }
    }
}
