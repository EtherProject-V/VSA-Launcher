using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VSA_launcher.OSCServer;

namespace VSA_launcher.VRC_Game
{
    /// <summary>
    /// VRChat起動監視とカメラ初期化を管理するクラス
    /// フローチャートに従った処理を実行
    /// </summary>
    public class VRChatInitializationManager : IDisposable
    {
        private const int PROCESS_MONITOR_INTERVAL_MS = 10000; // 10秒ごと
        private const int ROOM_JOIN_CHECK_INTERVAL_MS = 30000; // 30秒ごと

        private readonly VRChatLogParser _logParser;
        private readonly OSCParameterSender _oscParameterSender;
        private readonly Action<string, string> _updateStatusAction;

        private System.Threading.Timer? _processMonitorTimer;
        private System.Threading.Timer? _roomJoinCheckTimer;
        private CancellationTokenSource? _cancellationTokenSource;

        private bool _isVRChatRunning = false;
        private bool _isWaitingForRoomJoin = false;
        private DateTime _lastRoomJoinTime = DateTime.MinValue;

        public VRChatInitializationManager(
            VRChatLogParser logParser, 
            OSCParameterSender oscParameterSender,
            Action<string, string> updateStatusAction)
        {
            _logParser = logParser ?? throw new ArgumentNullException(nameof(logParser));
            _oscParameterSender = oscParameterSender ?? throw new ArgumentNullException(nameof(oscParameterSender));
            _updateStatusAction = updateStatusAction ?? throw new ArgumentNullException(nameof(updateStatusAction));
            
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 監視を開始
        /// </summary>
        public void Start()
        {
            Console.WriteLine("[初期化マネージャー] VRChat初期化監視を開始します");
            
            // 初期状態のVRChat起動状態を確認
            _isVRChatRunning = IsVRChatRunning();
            Console.WriteLine($"[初期化マネージャー] 初期VRChat状態: {(_isVRChatRunning ? "起動中" : "停止中")}");
            
            if (_isVRChatRunning)
            {
                // VRChatが既に起動している場合
                CheckInitialVRChatState();
            }
            
            // プロセス監視タイマーを開始（10秒ごと）
            _processMonitorTimer = new System.Threading.Timer(
                ProcessMonitorCallback,
                null,
                PROCESS_MONITOR_INTERVAL_MS, // 最初は10秒後から開始（初期状態確認後）
                PROCESS_MONITOR_INTERVAL_MS
            );
        }

        /// <summary>
        /// 監視を停止
        /// </summary>
        public void Stop()
        {
            Console.WriteLine("[初期化マネージャー] VRChat初期化監視を停止します");
            
            _processMonitorTimer?.Dispose();
            _roomJoinCheckTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// アプリ起動時のVRChat初期状態をチェック
        /// </summary>
        private void CheckInitialVRChatState()
        {
            try
            {
                Console.WriteLine("[初期化マネージャー] 既存VRChat状態の確認を開始します");
                
                // LogParserから現在のルーム参加状態を確認
                DateTime currentRoomJoinTime = GetLatestRoomJoinTime();
                
                if (currentRoomJoinTime != DateTime.MinValue)
                {
                    // 既にワールドに参加している場合は即座に初期化実行
                    Console.WriteLine($"[初期化マネージャー] 既存ワールド参加検知: {currentRoomJoinTime:yyyy-MM-dd HH:mm:ss}");
                    _lastRoomJoinTime = currentRoomJoinTime;
                    
                    _updateStatusAction("既存セッション検知", "VRChat起動済み - 初期化を実行中...");
                    
                    // OSC初期化を実行してセッション監視に移行
                    _ = Task.Run(async () =>
                    {
                        await ExecuteOscCameraInitialization();
                        StartSessionMonitoring();
                    });
                }
                else
                {
                    // ワールドに参加していない場合はルーム参加を監視
                    Console.WriteLine("[初期化マネージャー] VRChat起動済みだがワールド未参加 - ルーム参加を監視します");
                    StartRoomJoinMonitoring();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[初期化マネージャー] 初期状態確認エラー: {ex.Message}");
                // エラーの場合は通常のルーム参加監視に移行
                StartRoomJoinMonitoring();
            }
        }

        /// <summary>
        /// プロセス監視コールバック（10秒ごと）
        /// </summary>
        private void ProcessMonitorCallback(object? state)
        {
            try
            {
                bool currentVRChatStatus = IsVRChatRunning();
                
                if (currentVRChatStatus != _isVRChatRunning)
                {
                    _isVRChatRunning = currentVRChatStatus;
                    Console.WriteLine($"[初期化マネージャー] VRChat状態変更: {(_isVRChatRunning ? "起動" : "停止")}");
                    
                    if (_isVRChatRunning)
                    {
                        // VRChat新規起動検知 - ルーム参加監視を開始
                        StartRoomJoinMonitoring();
                    }
                    else
                    {
                        // VRChat停止検知 - ルーム参加監視を停止
                        StopRoomJoinMonitoring();
                        _updateStatusAction("VRChat停止", "初期化状態をリセットしました");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[初期化マネージャー] プロセス監視エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ルーム参加監視を開始（30秒ごと）
        /// </summary>
        private void StartRoomJoinMonitoring()
        {
            if (_isWaitingForRoomJoin) 
            {
                Console.WriteLine("[初期化マネージャー] ルーム参加監視は既に開始済みです");
                return; // 既に監視中
            }

            Console.WriteLine("[初期化マネージャー] ルーム参加監視を開始します");
            _isWaitingForRoomJoin = true;
            _lastRoomJoinTime = DateTime.MinValue;

            _roomJoinCheckTimer = new System.Threading.Timer(
                RoomJoinCheckCallback,
                null,
                ROOM_JOIN_CHECK_INTERVAL_MS, // 30秒後から開始
                ROOM_JOIN_CHECK_INTERVAL_MS  // 30秒ごと
            );

            _updateStatusAction("VRChat起動検知", "ルーム参加を監視中...");
        }

        /// <summary>
        /// ルーム参加監視を停止
        /// </summary>
        private void StopRoomJoinMonitoring()
        {
            if (!_isWaitingForRoomJoin) return; // 既に停止中

            Console.WriteLine("[初期化マネージャー] ルーム参加監視を停止します");
            _isWaitingForRoomJoin = false;
            
            _roomJoinCheckTimer?.Dispose();
            _roomJoinCheckTimer = null;
        }

        /// <summary>
        /// ルーム参加確認コールバック（30秒ごと）
        /// </summary>
        private void RoomJoinCheckCallback(object? state)
        {
            try
            {
                // VRChatが停止していたら監視を停止
                if (!_isVRChatRunning)
                {
                    StopRoomJoinMonitoring();
                    return;
                }

                // LogParserから最新のルーム参加時間を取得
                DateTime latestRoomJoinTime = GetLatestRoomJoinTime();
                
                if (latestRoomJoinTime > _lastRoomJoinTime && latestRoomJoinTime != DateTime.MinValue)
                {
                    // 新しいルーム参加を検知
                    _lastRoomJoinTime = latestRoomJoinTime;
                    Console.WriteLine($"[初期化マネージャー] ルーム参加検知: {latestRoomJoinTime:yyyy-MM-dd HH:mm:ss}");
                    
                    // OSCカメラ初期化処理を実行
                    _ = Task.Run(async () => await ExecuteOscCameraInitialization());
                    
                    // ルーム参加監視を停止（1回のセッションで1回のみ）
                    StopRoomJoinMonitoring();
                    
                    // セッション監視に切り替え（30秒ごとにVRChat起動状態を確認）
                    StartSessionMonitoring();
                }
                else
                {
                    Console.WriteLine("[初期化マネージャー] ルーム参加待機中...");
                    _updateStatusAction("ルーム参加監視", "Joinroomイベント待機中...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[初期化マネージャー] ルーム参加確認エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// セッション監視を開始（30秒ごとにVRChat起動状態確認）
        /// </summary>
        private void StartSessionMonitoring()
        {
            Console.WriteLine("[初期化マネージャー] セッション監視を開始します");

            // プロセス監視タイマーの間隔を30秒に変更
            _processMonitorTimer?.Dispose();
            _processMonitorTimer = new System.Threading.Timer(
                SessionMonitorCallback,
                null,
                ROOM_JOIN_CHECK_INTERVAL_MS, // 30秒後から開始
                ROOM_JOIN_CHECK_INTERVAL_MS  // 30秒ごと
            );

            _updateStatusAction("初期化完了", "セッション監視中...");
        }

        /// <summary>
        /// セッション監視コールバック（30秒ごと）
        /// </summary>
        private void SessionMonitorCallback(object? state)
        {
            try
            {
                bool currentVRChatStatus = IsVRChatRunning();
                
                if (currentVRChatStatus != _isVRChatRunning)
                {
                    _isVRChatRunning = currentVRChatStatus;
                    Console.WriteLine($"[初期化マネージャー] セッション中VRChat状態変更: {(_isVRChatRunning ? "起動" : "停止")}");
                    
                    if (!_isVRChatRunning)
                    {
                        // VRChat停止検知 - 初期監視モードに戻る
                        RestartInitialMonitoring();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[初期化マネージャー] セッション監視エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 初期監視モードに戻る
        /// </summary>
        private void RestartInitialMonitoring()
        {
            Console.WriteLine("[初期化マネージャー] 初期監視モードに戻ります");
            
            _isWaitingForRoomJoin = false;
            
            // プロセス監視タイマーを10秒間隔に戻す
            _processMonitorTimer?.Dispose();
            _processMonitorTimer = new System.Threading.Timer(
                ProcessMonitorCallback,
                null,
                0, // 即座に開始
                PROCESS_MONITOR_INTERVAL_MS
            );
            
            _updateStatusAction("監視再開", "VRChat起動を監視中...");
        }

        /// <summary>
        /// VRChat.exeが起動しているかチェック
        /// </summary>
        private bool IsVRChatRunning()
        {
            try
            {
                Process[] vrchatProcesses = Process.GetProcessesByName("VRChat");
                return vrchatProcesses.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// LogParserから最新のルーム参加時間を取得
        /// </summary>
        private DateTime GetLatestRoomJoinTime()
        {
            try
            {
                // VRChatLogParserのLastRoomJoinTimeプロパティを使用
                return _logParser.LastRoomJoinTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[初期化マネージャー] ルーム参加時間取得エラー: {ex.Message}");
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// OSCカメラ初期化処理を実行
        /// </summary>
        private async Task ExecuteOscCameraInitialization()
        {
            try
            {
                Console.WriteLine("[初期化マネージャー] OSCカメラ初期化処理を開始します");
                _updateStatusAction("OSC初期化", "カメラパラメータを初期化中...");

                // OSCParameterSenderを使用してカメラパラメータを初期化
                await _oscParameterSender.InitializeCameraParameters();
                
                Console.WriteLine("[初期化マネージャー] OSCカメラ初期化処理が完了しました");
                _updateStatusAction("初期化完了", "カメラパラメータの初期化が完了しました");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[初期化マネージャー] OSC初期化エラー: {ex.Message}");
                _updateStatusAction("初期化エラー", $"エラー: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}
