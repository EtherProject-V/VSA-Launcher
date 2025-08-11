using System.Diagnostics;
using System.Text.RegularExpressions;
using VSA_launcher.OSCServer;

namespace VSA_launcher
{
    /// <summary>
    /// 画像ファイルのメタデータ処理を担当するクラス
    /// </summary>
    public class MetadataProcessor
    {
        private readonly VRChatLogParser _logParser;
        private readonly FileWatcherService _fileWatcher;
        private readonly Action<string, string> _updateStatusAction;
        private readonly OscDataStore _oscDataStore;

        public MetadataProcessor(VRChatLogParser logParser, FileWatcherService fileWatcher, Action<string, string> updateStatusAction, OscDataStore oscDataStore)
        {
            _logParser = logParser;
            _fileWatcher = fileWatcher;
            _updateStatusAction = updateStatusAction;
            _oscDataStore = oscDataStore;
        }

        /// <summary>
        /// メタデータを付与してファイルを保存
        /// </summary>
        public bool SaveWithMetadata(string sourceFilePath, string destinationPath)
        {
            try
            {
                // 一時ファイルパスを作成
                string tempFilePath = Path.Combine(
                    Path.GetTempPath(), 
                    $"vsa_tmp_{Guid.NewGuid()}{Path.GetExtension(sourceFilePath)}");
                
                // まず元ファイルを一時ファイルにコピー（長めに待機）VRChat側が長めにファイルロックをかけているため
                if (!CopyFileWithRetry(sourceFilePath, tempFilePath, 20, 1000))
                {
                    _updateStatusAction("エラー", $"ファイルコピー失敗: {Path.GetFileName(sourceFilePath)}");
                    return false;
                }
                
                // ログパーサーから最新情報を取得
                _logParser.ParseLatestLog();

                // ユーザー名（撮影者）の情報を確保
                string username = "Unknown User";

                // メタデータの作成
                var metadata = new Dictionary<string, string>
                {
                    { "WorldName", _logParser.CurrentWorldName ?? "Unknown" },
                    { "WorldID", _logParser.CurrentWorldId ?? "Unknown" },
                    { "Capture-User", username },  // example.jsonの形式に合わせて変更
                    { "CaptureTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffK") } // ISO 8601形式に変更（タイムゾーン込み）
                };

                metadata["instans-Usernames"] = _logParser.GetFriendsString(); // example.jsonの形式に合わせて変更

                // OSC関連のメタデータを追加
                metadata["VirtualLens2_Aperture"] = _oscDataStore.VirtualLens2_Aperture.ToString();
                metadata["VirtualLens2_FocalLength"] = _oscDataStore.VirtualLens2_FocalLength.ToString();
                metadata["VirtualLens2_Exposure"] = _oscDataStore.VirtualLens2_Exposure.ToString();
                metadata["Integral_Aperture"] = _oscDataStore.Integral_Aperture.ToString();
                metadata["Integral_FocalLength"] = _oscDataStore.Integral_FocalLength.ToString();
                metadata["Integral_Exposure"] = _oscDataStore.Integral_Exposure.ToString();
                metadata["Integral_ShutterSpeed"] = _oscDataStore.Integral_ShutterSpeed.ToString();
                metadata["Integral_BokehShape"] = _oscDataStore.Integral_BokehShape.ToString();
                metadata["IsIntegral"] = _oscDataStore.IsIntegralActive.ToString();
                metadata["IsVirtualLens2"] = _oscDataStore.IsVirtualLens2Active.ToString();

                // カメラがNormalかどうかを判定
                if (!_oscDataStore.IsIntegralActive && !_oscDataStore.IsVirtualLens2Active)
                {
                    metadata["NormalCamera"] = "true";
                }
                else
                {
                    metadata["NormalCamera"] = "false";
                }

                try
                {
                    // 一時ファイルにメタデータを追加して出力先に保存 (最大3回リトライ)
                    bool success = false;
                    int retryCount = 0;
                    const int maxRetries = 3;
                    
                    while (!success && retryCount < maxRetries)
                    {
                        retryCount++;
                        success = PngMetadataManager.AddMetadataToPng(tempFilePath, destinationPath, metadata);
                        
                        if (!success)
                        {
                            _updateStatusAction("リトライ中", $"{Path.GetFileName(sourceFilePath)}: メタデータ追加 ({retryCount}/{maxRetries})");
                            if (retryCount < maxRetries)
                            {
                                Thread.Sleep(500); // 0.5秒待機してリトライ
                            }
                        }
                        else
                        {
                            // メタデータが実際に書き込まれているか検証
                            if (!VerifyMetadata(destinationPath, metadata))
                            {
                                success = false; // 検証失敗時はリトライ
                                _updateStatusAction("検証失敗", $"{Path.GetFileName(sourceFilePath)}: メタデータ検証失敗 ({retryCount}/{maxRetries})");
                                if (retryCount < maxRetries)
                                {
                                    Thread.Sleep(500);
                                }
                            }
                        }
                    }
                    
                    if (!success)
                    {
                        _updateStatusAction("メタデータ追加エラー", $"{Path.GetFileName(sourceFilePath)}: {maxRetries}回リトライ後、メタデータなしでコピーします");
                        return SimpleCopy(tempFilePath, destinationPath);
                    }
                    
                    // 一時ファイルを削除
                    try { File.Delete(tempFilePath); } catch { }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"メタデータ追加エラー: {ex.Message}");
                    _updateStatusAction("メタデータ追加エラー", $"{Path.GetFileName(sourceFilePath)}: メタデータなしでコピーします");
                    
                    // メタデータ追加に失敗した場合は単純コピー
                    return SimpleCopy(tempFilePath, destinationPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"メタデータ処理中の致命的なエラー: {ex}");
                // シンプルなコピー操作に切り替え
                return SimpleCopy(sourceFilePath, destinationPath);
            }
        }

        /// <summary>
        /// 書き込まれたメタデータを検証する
        /// </summary>
        private bool VerifyMetadata(string filePath, Dictionary<string, string> expectedMetadata)
        {
            try
            {
                // ファイルが存在するか確認
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"検証エラー: ファイルが見つかりません: {filePath}");
                    return false;
                }
                
                // ファイルへのアクセスを何度か試行
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        // ファイルからメタデータを読み取り（PngMetadataManagerを使用）
                        var actualMetadata = PngMetadataManager.ReadMetadata(filePath);
                        
                        // ワールド情報など重要なメタデータが含まれているかチェック
                        string[] essentialKeys = { "WorldName", "WorldID", "CaptureTime" };
                        foreach (var key in essentialKeys)
                        {
                            if (!actualMetadata.ContainsKey(key) && expectedMetadata.ContainsKey(key))
                            {
                                Debug.WriteLine($"検証エラー: 必須メタデータが欠落しています: {key}");
                                return false;
                            }
                        }
                        
                        Debug.WriteLine($"メタデータ検証成功: {filePath}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // エラーが発生した場合は少し待機して再試行
                        Debug.WriteLine($"検証中のエラー（リトライ {attempt+1}/3）: {ex.Message}");
                        Thread.Sleep(500);
                    }
                }
                
                // すべての試行が失敗
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"メタデータ検証中の致命的なエラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ファイルを強制的にコピー（リトライロジック付き）
        /// </summary>
        private bool CopyFileWithRetry(string sourceFilePath, string destinationPath, int maxAttempts = 10, int waitTimeMs = 500)
        {
            int attempts = 0;
            
            while (attempts < maxAttempts)
            {
                try
                {
                    // ファイルが存在するか確認
                    if (!File.Exists(sourceFilePath))
                    {
                        Thread.Sleep(waitTimeMs);
                        attempts++;
                        continue;
                    }
                    
                    // 出力先フォルダが存在しない場合は作成
                    string? destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }
                    
                    // FileShareオプションを指定して読み取り共有を許可しコピー
                    using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        // バッファサイズを大きく設定し効率的にコピー
                        byte[] buffer = new byte[81920]; // 80KB
                        int bytesRead;
                        
                        while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            destStream.Write(buffer, 0, bytesRead);
                        }
                    }
                    
                    return true; // コピー成功
                }
                catch (IOException)
                {
                    // ファイルアクセスに失敗した場合は待機して再試行
                    attempts++;
                    Debug.WriteLine($"ファイルコピーリトライ中... {attempts}/{maxAttempts}");
                    Thread.Sleep(waitTimeMs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ファイルコピー中の予期せぬエラー: {ex.Message}");
                    return false;
                }
            }
            
            return false; // すべてのリトライが失敗
        }

        /// <summary>
        /// メタデータなしで単純コピー
        /// </summary>
        public bool SimpleCopy(string sourceFilePath, string destinationPath)
        {
            return CopyFileWithRetry(sourceFilePath, destinationPath, 10, 500);
        }

        /// <summary>
        /// ファイルが既に処理済みかチェック
        /// </summary>
        public bool IsProcessedFile(string filePath)
        {
            try
            {
                // PngMetadataManagerを使用
                return PngMetadataManager.IsProcessedFile(filePath);
            }
            catch
            {
                return false;
            }
        }
    }
}