using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSA_launcher.VRC_Game;
using VSA_launcher.OSCServer;

namespace VSA_launcher.devModule
{
    /// <summary>
    /// 開発モード用: 2秒間隔でメタデータを収集し、指定のRichTextBoxへリアルタイム表示するバックグラウンド監視。
    /// メイン処理からは分離した別クラス・別スレッド(Task)で動作します。
    /// </summary>
    public sealed class DevMetadataMonitor : IDisposable
    {
        private readonly RichTextBox _target;
        private readonly VRChatLogParser _logParser;
        private readonly OscDataStore _oscDataStore;
        private readonly Func<bool> _isDevEnabled;
        private readonly Control _uiContext; // BeginInvoke用

        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        public DevMetadataMonitor(Control uiContext, RichTextBox target, VRChatLogParser logParser, OscDataStore oscDataStore, Func<bool> isDevEnabled)
        {
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _logParser = logParser ?? throw new ArgumentNullException(nameof(logParser));
            _oscDataStore = oscDataStore ?? throw new ArgumentNullException(nameof(oscDataStore));
            _isDevEnabled = isDevEnabled ?? throw new ArgumentNullException(nameof(isDevEnabled));
        }

        public void Start()
        {
            if (_loopTask != null && !_loopTask.IsCompleted) return;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _loopTask?.Wait(1000);
            }
            catch { /* ignore */ }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
            }
        }

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_isDevEnabled())
                    {
                        // 必要なら最新ログを更新
                        try { _logParser.ParseLatestLog(); } catch { }

                        var dict = CollectMetadataSnapshot();
                        var text = BuildDisplayText(dict);

                        if (!_target.IsDisposed)
                        {
                            // UIスレッドで更新
                            if (_uiContext.IsHandleCreated)
                            {
                                _uiContext.BeginInvoke(new Action(() =>
                                {
                                    // チェックが外れた間に切り替わった場合の二重チェック
                                    if (_isDevEnabled())
                                    {
                                        _target.Text = text;
                                    }
                                }));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // デバッグ用に例外は握りつぶし: UIに出すのもアリだが煩雑なのでDebug出力に留める
                    System.Diagnostics.Debug.WriteLine($"[DevMetadataMonitor] Error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private Dictionary<string, string> CollectMetadataSnapshot()
        {
            var metadata = new Dictionary<string, string>();

            // VRChatログ由来
            metadata["WorldName"] = _logParser?.CurrentWorldName ?? "Unknown";
            metadata["WorldID"] = _logParser?.CurrentWorldId ?? "Unknown";
            metadata["Capture-User"] = _logParser?.Username ?? "Unknown User";
            metadata["Friends"] = _logParser?.GetFriendsString() ?? string.Empty;

            // 時刻（サンプル用）
            metadata["Now"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffK");

            // OSC関連（必要なもののみ）
            metadata["IsIntegral"] = _oscDataStore?.IsIntegralActive.ToString() ?? "false";
            metadata["IsVirtualLens2"] = _oscDataStore?.IsVirtualLens2Active.ToString() ?? "false";
            metadata["VirtualLens2_Aperture"] = _oscDataStore?.VirtualLens2_Aperture.ToString() ?? string.Empty;
            metadata["VirtualLens2_FocalLength"] = _oscDataStore?.VirtualLens2_FocalLength.ToString() ?? string.Empty;
            metadata["VirtualLens2_Exposure"] = _oscDataStore?.VirtualLens2_Exposure.ToString() ?? string.Empty;
            metadata["Integral_Aperture"] = _oscDataStore?.Integral_Aperture.ToString() ?? string.Empty;
            metadata["Integral_FocalLength"] = _oscDataStore?.Integral_FocalLength.ToString() ?? string.Empty;
            metadata["Integral_Exposure"] = _oscDataStore?.Integral_Exposure.ToString() ?? string.Empty;
            metadata["Integral_ShutterSpeed"] = _oscDataStore?.Integral_ShutterSpeed.ToString() ?? string.Empty;
            metadata["Integral_BokehShape"] = _oscDataStore?.Integral_BokehShape.ToString() ?? string.Empty;

            return metadata;
        }

        private static string BuildDisplayText(Dictionary<string, string> metadata)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Dev Metadata Snapshot ===");
            foreach (var kv in metadata)
            {
                sb.AppendLine($"{kv.Key}: {kv.Value}");
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
