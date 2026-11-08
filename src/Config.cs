using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace LovingCoolMini
{
    [DataContract]
    public sealed class AppConfig
    {
        [DataMember] public bool StartMinimized;
        [DataMember] public bool AutoStart;
        [DataMember] public bool ShowDecimal;
        [DataMember] public int RefreshMs;
        [DataMember] public int HeartbeatSeconds;
        [DataMember] public string LastSerial;
        [DataMember] public string TemperatureSource;
        [DataMember] public string GpuId;

        public AppConfig()
        {
            StartMinimized = true;
            AutoStart = true;
            ShowDecimal = true;
            RefreshMs = 1000;
            HeartbeatSeconds = 5;
            LastSerial = string.Empty;
            TemperatureSource = "CPU";
            GpuId = string.Empty;
        }

        public static string AppDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LovingCoolMini");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(AppDir, "config.json"); }
        }

        public static string LogDir
        {
            get { return Path.Combine(AppDir, "logs"); }
        }

        public static AppConfig Load(Logger logger)
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                if (!File.Exists(ConfigPath))
                {
                    return new AppConfig();
                }

                using (FileStream fs = File.OpenRead(ConfigPath))
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppConfig));
                    AppConfig cfg = ser.ReadObject(fs) as AppConfig;
                    if (cfg == null)
                    {
                        return new AppConfig();
                    }
                    cfg.Normalize();
                    return cfg;
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error("读取配置失败，使用默认配置", ex);
                }
                return new AppConfig();
            }
        }

        public void Normalize()
        {
            if (RefreshMs < 500) RefreshMs = 500;
            if (RefreshMs > 5000) RefreshMs = 5000;
            if (HeartbeatSeconds < 2) HeartbeatSeconds = 2;
            if (HeartbeatSeconds > 60) HeartbeatSeconds = 60;
            if (LastSerial == null) LastSerial = string.Empty;
            if (TemperatureSource != "CPU" && TemperatureSource != "GPU")
            {
                TemperatureSource = "CPU";
            }
            if (GpuId == null) GpuId = string.Empty;
        }

        public void Save(Logger logger)
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                string tmp = ConfigPath + ".tmp";
                using (FileStream fs = File.Create(tmp))
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppConfig));
                    ser.WriteObject(fs, this);
                }
                if (File.Exists(ConfigPath))
                {
                    File.Copy(tmp, ConfigPath, true);
                    File.Delete(tmp);
                }
                else
                {
                    File.Move(tmp, ConfigPath);
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error("保存配置失败", ex);
                }
            }
        }
    }
}
