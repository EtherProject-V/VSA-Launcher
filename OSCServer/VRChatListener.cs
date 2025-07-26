using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Rug.Osc;
using System.Diagnostics;

namespace VSA_launcher.OSCServer
{
    /// <summary>
    /// VRChatからのOSCパラメータを受信するリスナークラス
    /// VRChat標準の9001ポートでリッスンし、10ミリ秒ごとにメッセージをチェックし、1秒ごとにステータスを出力
    /// </summary>
    public class VRChatListener : IDisposable
    {
        private const string VRC_IP_ADDRESS = "127.0.0.1";
        private const int VRC_RECEIVER_PORT = 9001;
        
        private OscReceiver? _receiver;
        private CancellationTokenSource? _listenerCancelTokenSource;
        private Task? _listenerTask;
        private Task? _periodicListenTask;
        private OscDataStore _dataStore;
        
        // 受信したユニークなアドレスを保存するためのコレクション（デバッグ用）
        private readonly HashSet<string> _discoveredAddresses = new HashSet<string>();

        /// <summary>
        /// OSCメッセージ受信通知イベント（開発モード用）
        /// </summary>
        public event Action<string, object?>? MessageReceived;

        /// <summary>
        /// OSCログ出力用イベント（ログモード用）
        /// </summary>
        public event Action<string>? LogMessageReceived;

        public VRChatListener(OscDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public void Start()
        {
            try
            {
                _listenerCancelTokenSource = new CancellationTokenSource();
                
                // メインリスナータスクを開始
                _listenerTask = Task.Run(() => ListenLoop(_listenerCancelTokenSource.Token));
                
                // 1秒ごとの定期リッスンタスクを開始
                _periodicListenTask = Task.Run(() => PeriodicListenLoop(_listenerCancelTokenSource.Token));
                
                Console.WriteLine($"[OSC受信] VRChat OSC Listener started on port {VRC_RECEIVER_PORT}");
                Debug.WriteLine($"VRChat OSC Listener started on port {VRC_RECEIVER_PORT}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VRChat OSC Listener start error: {ex.Message}");
                Debug.WriteLine($"VRChat OSC Listener start error: {ex.Message}");
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            try
            {
                _receiver = new OscReceiver(VRC_RECEIVER_PORT);
                _receiver.Connect();

                while (!token.IsCancellationRequested)
                {
                    if (_receiver.TryReceive(out OscPacket packet) && packet is OscMessage)
                    {
                        // 受信したすべてのメッセージを処理
                        ProcessReceivedPacket(packet);
                    }
                    await Task.Delay(10, token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _receiver?.Dispose();
            }
        }

        private async Task PeriodicListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 1秒待機
                    await Task.Delay(1000, token);
                    
                    // 定期的なステータス出力（デバッグ用）
                    if (_discoveredAddresses.Count > 0)
                    {
                        Console.WriteLine($"[OSCステータス] [{DateTime.Now:HH:mm:ss}] 発見されたアドレス数: {_discoveredAddresses.Count}");
                        Console.WriteLine($"[OSCステータス] Integral Active: {_dataStore.IsIntegralActive}, VirtualLens2 Active: {_dataStore.IsVirtualLens2Active}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は正常終了
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] PeriodicListenLoop error: {ex.Message}");
            }
        }

        private void ProcessReceivedPacket(OscPacket packet)
        {
            if (!(packet is OscMessage message)) return;

            // 取り込み対象のパラメータのみコンソールログに出力
            if (IsTargetParameter(message.Address))
            {
                Console.WriteLine($"[OSC受信] {DateTime.Now:HH:mm:ss.fff} - {message.Address}");
                if (message.Count > 0)
                {
                    Console.WriteLine($"           値: {message[0]}");
                }

                // 開発モード用の受信通知イベント
                MessageReceived?.Invoke(message.Address, message.Count > 0 ? message[0] : null);

                // ログモード用のイベント（軽量化のため文字列を事前構築）
                string logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message.Address}";
                if (message.Count > 0)
                {
                    logMessage += $" = {message[0]}";
                }
                LogMessageReceived?.Invoke(logMessage);
            }

            // 新しいアドレスを発見した場合のみデバッグログ出力
            bool isNewAddress = _discoveredAddresses.Add(message.Address);
            if (isNewAddress)
            {
                Debug.WriteLine($"新しいOSCアドレス発見: {message.Address}");
            }

            // データストア更新処理は削除 - UI主導の設定管理に変更
        }

        /// <summary>
        /// 取り込み対象のパラメータかどうかを判定
        /// </summary>
        /// <param name="address">OSCアドレス</param>
        /// <returns>取り込み対象の場合true</returns>
        private bool IsTargetParameter(string address)
        {
            return address switch
            {
                // Integral Camera Parameters
                "/avatar/parameters/Integral_Enable" => true,
                "/avatar/parameters/Integral_Aperture" => true,
                "/avatar/parameters/Integral_Zoom" => true,
                "/avatar/parameters/Integral_FocalLength" => true,
                "/avatar/parameters/Integral_Exposure" => true,
                "/avatar/parameters/Integral_ShutterSpeed" => true,
                "/avatar/parameters/Integral_BokehShape" => true,
                
                // VirtualLens2 Camera Parameters
                "/avatar/parameters/VirtualLens2_Enable" => true,
                "/avatar/parameters/VirtualLens2_Aperture" => true,
                "/avatar/parameters/VirtualLens2_Zoom" => true,
                "/avatar/parameters/VirtualLens2_FocalLength" => true,
                "/avatar/parameters/VirtualLens2_Exposure" => true,
                
                _ => false
            };
        }

        public void Stop()
        {
            try
            {
                _listenerCancelTokenSource?.Cancel();
                
                // タスクの完了を待機
                _listenerTask?.Wait(TimeSpan.FromSeconds(2));
                _periodicListenTask?.Wait(TimeSpan.FromSeconds(2));
                
                Debug.WriteLine("VRChat OSC Listener stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VRChat OSC Listener stop error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            
            _receiver?.Dispose();
            _listenerCancelTokenSource?.Dispose();
            _listenerTask?.Dispose();
            _periodicListenTask?.Dispose();
        }
    }
}
