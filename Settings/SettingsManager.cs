using System;
using System.IO;
using Newtonsoft.Json;

namespace VSA_launcher
{
    public static class SettingsManager
    {
        private static readonly string SettingsFileName = "appsettings.json";

        // 設定ファイルの保存場所（管理者権限不要、Windows標準）
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VSA", "launcher");

        private static readonly string SettingsFilePath = Path.Combine(
            SettingsDirectory, SettingsFileName);

        public static AppSettings LoadSettings()
        {
            try
            {
                AppSettings settings;

                // 設定ディレクトリが存在しない場合は作成
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    settings = new AppSettings();
                }

                // カメラ設定の補完
                EnsureCameraSettingsComplete(settings);

                // 設定ファイルを更新（デフォルト値で補完された場合）
                SaveSettings(settings);

                return settings;
            }
            catch (Exception ex)
            {
                // 読み込みエラー時はログに記録
                Console.WriteLine($"設定ファイルの読み込みエラー: {ex.Message}");
                var defaultSettings = new AppSettings();
                EnsureCameraSettingsComplete(defaultSettings);
                return defaultSettings;
            }
        }

        /// <summary>
        /// カメラ設定が不完全な場合にデフォルト値で補完
        /// </summary>
        private static void EnsureCameraSettingsComplete(AppSettings settings)
        {
            // CameraSettingsがnullの場合は新規作成
            if (settings.CameraSettings == null)
            {
                settings.CameraSettings = new CameraSettings();
            }

            // VirtualLens2設定の補完
            if (settings.CameraSettings.VirtualLens2 == null)
            {
                settings.CameraSettings.VirtualLens2 = new VirtualLens2Settings();
            }

            // Integral設定の補完
            if (settings.CameraSettings.Integral == null)
            {
                settings.CameraSettings.Integral = new IntegralSettings();
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                // 設定ディレクトリが存在しない場合は作成
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                // 保存エラー時はログに記録
                Console.WriteLine($"設定ファイルの保存エラー: {ex.Message}");
            }
        }
    }
}