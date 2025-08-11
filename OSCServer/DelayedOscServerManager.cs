using System;
using System.Threading;
using System.Threading.Tasks;
using VSA_launcher.VRC_Game;

namespace VSA_launcher.OSCServer
{
    /// <summary>
    /// VRChat起動検知に基づくOSCサーバーの遅延起動管理クラス
    /// </summary>
    public class DelayedOscServerManager : IDisposable
    {
        private const int GAME_STARTUP_DELAY_MS = 20000; // ゲーム起動検知後20秒
        private const int ALREADY_RUNNING_DELAY_MS = 5000; // 既に起動済みの場合5秒
        
        private readonly VRChatLogParser _logParser;
        private readonly Action _startOscServerCallback;
        private readonly Action _stopOscServerCallback;
        private readonly Action<string, string> _updateStatusAction;
        
        private System.Threading.Timer? _delayedStartTimer;
        private bool _oscServerStarted = false;
        private bool _disposed = false;

        public DelayedOscServerManager(
            VRChatLogParser logParser,
            Action startOscServerCallback,
            Action stopOscServerCallback,
            Action<string, string> updateStatusAction)
        {
            _logParser = logParser ?? throw new ArgumentNullException(nameof(logParser));
            _startOscServerCallback = startOscServerCallback ?? throw new ArgumentNullException(nameof(startOscServerCallback));
            _stopOscServerCallback = stopOscServerCallback ?? throw new ArgumentNullException(nameof(stopOscServerCallback));
            _updateStatusAction = updateStatusAction ?? throw new ArgumentNullException(nameof(updateStatusAction));
        }

        /// <summary>
        /// VRChat起動検知時の処理
        /// </summary>
        /// <param name="isAlreadyRunning">アプリ起動時に既にVRChatが起動していたかどうか</param>
        public void OnVRChatStartupDetected(bool isAlreadyRunning = false)
        {
            if (_oscServerStarted)
            {
                Console.WriteLine("[DelayedOscServerManager] OSCサーバーは既に起動済みです");
                return;
            }

            // 既存のタイマーをキャンセル
            _delayedStartTimer?.Dispose();

            int delayMs = isAlreadyRunning ? ALREADY_RUNNING_DELAY_MS : GAME_STARTUP_DELAY_MS;
            string delaySeconds = (delayMs / 1000).ToString();
            
            Console.WriteLine($"[DelayedOscServerManager] VRChat起動検知 - {delaySeconds}秒後にOSCサーバーを起動します (既存起動: {isAlreadyRunning})");
            
            _updateStatusAction("VRChat起動検知", $"OSCサーバーを{delaySeconds}秒後に起動予定");

            _delayedStartTimer = new System.Threading.Timer(DelayedStartCallback, null, delayMs, Timeout.Infinite);
        }

        /// <summary>
        /// VRChat停止検知時の処理
        /// </summary>
        public void OnVRChatShutdownDetected()
        {
            Console.WriteLine("[DelayedOscServerManager] VRChat停止検知 - OSCサーバーを停止します");
            
            // 遅延起動タイマーをキャンセル
            _delayedStartTimer?.Dispose();
            _delayedStartTimer = null;
            
            // OSCサーバーが起動済みの場合は停止
            if (_oscServerStarted)
            {
                try
                {
                    _stopOscServerCallback();
                    _oscServerStarted = false;
                    _updateStatusAction("VRChat停止検知", "OSCサーバーを停止しました");
                    Console.WriteLine("[DelayedOscServerManager] OSCサーバーを停止しました");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DelayedOscServerManager] OSCサーバー停止エラー: {ex.Message}");
                    _updateStatusAction("OSCサーバー停止エラー", ex.Message);
                }
            }
            else
            {
                _updateStatusAction("VRChat停止検知", "OSCサーバー起動予定をキャンセルしました");
            }
        }

        /// <summary>
        /// 遅延起動のコールバック
        /// </summary>
        private void DelayedStartCallback(object? state)
        {
            try
            {
                // VRChatがまだ起動しているかチェック
                if (!IsVRChatRunning())
                {
                    Console.WriteLine("[DelayedOscServerManager] VRChatが停止済みのため、OSCサーバー起動をキャンセルします");
                    _updateStatusAction("起動キャンセル", "VRChat停止によりOSCサーバー起動をキャンセル");
                    return;
                }

                Console.WriteLine("[DelayedOscServerManager] OSCサーバーを起動します");
                _updateStatusAction("OSCサーバー起動", "VRChat連携サーバーを開始中...");

                _startOscServerCallback();
                _oscServerStarted = true;

                Console.WriteLine("[DelayedOscServerManager] OSCサーバーが正常に起動しました");
                _updateStatusAction("OSCサーバー起動完了", "VRChat連携準備完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DelayedOscServerManager] OSCサーバー起動エラー: {ex.Message}");
                _updateStatusAction("OSCサーバー起動エラー", ex.Message);
            }
            finally
            {
                _delayedStartTimer?.Dispose();
                _delayedStartTimer = null;
            }
        }

        /// <summary>
        /// VRChatが起動しているかチェック
        /// </summary>
        private bool IsVRChatRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("VRChat");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// OSCサーバーの現在の状態を取得
        /// </summary>
        public bool IsOscServerStarted => _oscServerStarted;

        /// <summary>
        /// 手動でOSCサーバーを起動（デバッグ用）
        /// </summary>
        public void ForceStartOscServer()
        {
            if (!_oscServerStarted)
            {
                Console.WriteLine("[DelayedOscServerManager] 手動OSCサーバー起動");
                OnVRChatStartupDetected(true); // 既存起動として扱う（5秒遅延）
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _delayedStartTimer?.Dispose();
            if (_oscServerStarted)
            {
                _stopOscServerCallback();
            }
            
            _disposed = true;
        }
    }
}
