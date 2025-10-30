using System;
using System.Diagnostics;
using System.IO;

namespace VSA_launcher
{
    public class VdiLauncher
    {
        private readonly VdiInstallManager _installManager;
        private readonly AppSettings _settings;

        public VdiLauncher(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _installManager = new VdiInstallManager();
        }

        // VDIでファイルを開く
        public bool LaunchVdi(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath) || !File.Exists(photoPath))
            {
                throw new ArgumentException("有効な写真パスを指定してください", nameof(photoPath));
            }

            string vdiPath = _settings.VdiSettings.VdiExecutablePath;

            // VDIパスが設定されていない、またはファイルが存在しない場合は検出を試みる
            if (string.IsNullOrEmpty(vdiPath) || !File.Exists(vdiPath))
            {
                vdiPath = _installManager.GetVdiExecutablePath();

                if (string.IsNullOrEmpty(vdiPath))
                {
                    return false; // VDIが見つからない
                }

                // 設定に保存
                _settings.VdiSettings.VdiExecutablePath = vdiPath;
            }

            // コマンドライン引数を構築
            string windowMode = GetWindowMode();
            string closeWindowMode = _settings.VdiSettings.CloseOtherWindows ? "TRUE" : "FALSE";

            // コマンドライン引数フォーマット: vdi.exe [image-path] [window-mode] [closewindow-mode]
            string arguments = $"\"{photoPath}\" {windowMode} {closeWindowMode}";

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = vdiPath,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VDI起動エラー: {ex.Message}");
                return false;
            }
        }

        // ウィンドウモードを取得
        private string GetWindowMode()
        {
            string mode = _settings.VdiSettings.DefaultWindowMode;

            if (mode.Equals("FullScreen", StringComparison.OrdinalIgnoreCase))
            {
                return "FullScreen";
            }
            else if (mode.Equals("Custom", StringComparison.OrdinalIgnoreCase) ||
                     mode.Equals("CustomResolution", StringComparison.OrdinalIgnoreCase))
            {
                // カスタム解像度を返す（例: 1920x1080）
                string resolution = _settings.VdiSettings.CustomResolution;
                if (IsValidResolution(resolution))
                {
                    return resolution;
                }
                else
                {
                    // 無効な解像度の場合はデフォルトを返す
                    return "1920x1080";
                }
            }
            else
            {
                // その他の場合はフルスクリーン
                return "FullScreen";
            }
        }

        // 解像度フォーマットの検証（例: 1920x1080）
        private bool IsValidResolution(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution))
                return false;

            var parts = resolution.Split('x', 'X');
            if (parts.Length != 2)
                return false;

            return int.TryParse(parts[0], out int width) &&
                   int.TryParse(parts[1], out int height) &&
                   width > 0 && height > 0;
        }

        // VDIが起動可能かチェック
        public bool CanLaunchVdi()
        {
            return _installManager.CheckVdiInstalled();
        }

        // VDIのインストール状態を取得
        public VdiStatus GetVdiStatus()
        {
            return new VdiStatus
            {
                IsInstalled = _installManager.CheckVdiInstalled(),
                ExecutablePath = _installManager.GetVdiExecutablePath(),
                Version = _installManager.GetInstalledVdiVersion()
            };
        }
    }

    // VDI状態クラス
    public class VdiStatus
    {
        public bool IsInstalled { get; set; }
        public string ExecutablePath { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}
