using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace VSA_launcher
{
    public class VdiInstallManager
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/JunseiOgawa/VDI-solid/releases/latest";
        private readonly HttpClient _httpClient;

        public VdiInstallManager()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VSA-Launcher");
        }

        // VDIがインストールされているかチェック
        public bool CheckVdiInstalled()
        {
            string path = GetVdiExecutablePath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        // VDI実行ファイルのパスを取得
        public string GetVdiExecutablePath()
        {
            // 1. Tauriアプリのデフォルトインストール先（LocalAppData）
            string[] localAppDataPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "vdi-solid", "vdi.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "vdi-solid", "vdi.exe")
            };

            foreach (var localPath in localAppDataPaths)
            {
                if (File.Exists(localPath))
                    return localPath;
            }

            // 2. レジストリチェック（HKEY_LOCAL_MACHINE）
            string path = GetPathFromRegistry(RegistryHive.LocalMachine, @"SOFTWARE\VDI-solid", "InstallPath");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;

            // 3. レジストリチェック（HKEY_CURRENT_USER）
            path = GetPathFromRegistry(RegistryHive.CurrentUser, @"SOFTWARE\VDI-solid", "InstallPath");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;

            // 4. Program Filesスキャン
            string[] programFilesPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VDI-solid", "vdi.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VDI-solid", "vdi.exe")
            };

            foreach (var programPath in programFilesPaths)
            {
                if (File.Exists(programPath))
                    return programPath;
            }

            return string.Empty;
        }

        // レジストリからパスを取得
        private string GetPathFromRegistry(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(valueName);
                        if (value != null)
                        {
                            string installPath = value.ToString();
                            if (!installPath.EndsWith("vdi.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                installPath = Path.Combine(installPath, "vdi.exe");
                            }
                            return installPath;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // レジストリアクセスエラーは無視
            }

            return string.Empty;
        }

        // インストール済みVDIのバージョンを取得
        public string GetInstalledVdiVersion()
        {
            string vdiPath = GetVdiExecutablePath();
            if (string.IsNullOrEmpty(vdiPath))
                return string.Empty;

            try
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(vdiPath);
                return fileVersionInfo.FileVersion ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // GitHub APIから最新バージョン情報を取得
        public async Task<VdiReleaseInfo> GetLatestVdiVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var json = JObject.Parse(response);

                var releaseInfo = new VdiReleaseInfo
                {
                    TagName = json["tag_name"]?.ToString() ?? string.Empty,
                    Name = json["name"]?.ToString() ?? string.Empty,
                    PublishedAt = json["published_at"]?.ToObject<DateTime>() ?? DateTime.MinValue
                };

                // assetsからインストーラーのダウンロードURLを取得
                var assets = json["assets"] as JArray;
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        string name = asset["name"]?.ToString() ?? string.Empty;
                        // Windows用インストーラーを探す（.msi または _x64-setup.exe）
                        if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                        {
                            releaseInfo.DownloadUrl = asset["browser_download_url"]?.ToString() ?? string.Empty;
                            releaseInfo.FileName = name;
                            break;
                        }
                    }
                }

                return releaseInfo;
            }
            catch (HttpRequestException)
            {
                // ネットワークエラー
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"GitHub APIのレスポンス解析に失敗しました: {ex.Message}", ex);
            }
        }

        // VDIインストーラーをダウンロード
        public async Task<string> DownloadVdiInstallerAsync(
            string downloadUrl,
            string fileName,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            try
            {
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1 && progress != null;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalRead += bytesRead;

                            if (canReportProgress)
                            {
                                var progressPercentage = (int)((totalRead * 100) / totalBytes);
                                progress.Report(progressPercentage);
                            }
                        }
                    }
                }

                return tempPath;
            }
            catch (OperationCanceledException)
            {
                // ダウンロードキャンセル
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
            catch (Exception)
            {
                // ダウンロードエラー
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }

        // VDIインストーラーを実行
        public bool LaunchInstaller(string installerPath)
        {
            if (!File.Exists(installerPath))
                return false;

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ダウンロードしたインストーラーを削除
        public void CleanupInstaller(string installerPath)
        {
            try
            {
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                }
            }
            catch (Exception)
            {
                // エラーは無視
            }
        }
    }

    // VDIリリース情報クラス
    public class VdiReleaseInfo
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
