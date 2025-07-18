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
    /// VRChat標準の9001ポートでリッスンし、1秒ごとに定期的にデータを取得
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
                
                Debug.WriteLine($"VRChat OSC Listener started on port {VRC_RECEIVER_PORT}");
            }
            catch (Exception ex)
            {
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
                    if (_receiver.TryReceive(out OscPacket packet))
                    {
                        ProcessReceivedPacket(packet);
                    }
                    await Task.Delay(10, token); // 10ms間隔でチェック
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は正常終了
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VRChat OSC Listener error: {ex.Message}");
            }
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
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] VRChat OSC - 発見されたアドレス数: {_discoveredAddresses.Count}");
                        Debug.WriteLine($"  Integral Active: {_dataStore.IsIntegralActive}, VirtualLens2 Active: {_dataStore.IsVirtualLens2Active}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は正常終了
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VRChat OSC Periodic Listen error: {ex.Message}");
            }
        }

        private void ProcessReceivedPacket(OscPacket packet)
        {
            if (!(packet is OscMessage message)) return;

            // 新しいアドレスを発見した場合のみログ出力
            bool isNewAddress = _discoveredAddresses.Add(message.Address);
            if (isNewAddress)
            {
                Debug.WriteLine($"新しいOSCアドレス発見: {message.Address}");
            }

            // パラメータの処理
            ProcessOscMessage(message);
        }

        private void ProcessOscMessage(OscMessage message)
        {
            switch (message.Address)
            {
                // Integral Camera Parameters
                case "/avatar/parameters/Integral_Enable":
                    if (message.Count > 0)
                    {
                        bool enabled = message[0] switch
                        {
                            bool boolValue => boolValue,
                            float floatValue => floatValue > 0.5f,
                            int intValue => intValue > 0,
                            _ => false
                        };
                        _dataStore.IsIntegralActive = enabled;
                        Debug.WriteLine($"Integral_Enable updated: {enabled}");
                    }
                    break;
                    
                case "/avatar/parameters/Integral_Aperture":
                    if (message.Count > 0 && message[0] is float aperture)
                    {
                        _dataStore.Integral_Aperture = aperture;
                        Debug.WriteLine($"Integral_Aperture updated: {aperture}");
                    }
                    break;
                    
                case "/avatar/parameters/Integral_Zoom":
                case "/avatar/parameters/Integral_FocalLength":
                    if (message.Count > 0 && message[0] is float focalLength)
                    {
                        _dataStore.Integral_FocalLength = focalLength;
                        Debug.WriteLine($"Integral_FocalLength updated: {focalLength}");
                    }
                    break;
                    
                case "/avatar/parameters/Integral_Exposure":
                    if (message.Count > 0 && message[0] is float exposure)
                    {
                        _dataStore.Integral_Exposure = exposure;
                        Debug.WriteLine($"Integral_Exposure updated: {exposure}");
                    }
                    break;
                    
                case "/avatar/parameters/Integral_ShutterSpeed":
                    if (message.Count > 0 && message[0] is float shutterSpeed)
                    {
                        _dataStore.Integral_ShutterSpeed = shutterSpeed;
                        Debug.WriteLine($"Integral_ShutterSpeed updated: {shutterSpeed}");
                    }
                    break;
                    
                case "/avatar/parameters/Integral_BokehShape":
                    if (message.Count > 0)
                    {
                        // int型またはfloat型から変換
                        int bokehShape = message[0] switch
                        {
                            int intValue => intValue,
                            float floatValue => (int)floatValue,
                            _ => 0
                        };
                        _dataStore.Integral_BokehShape = bokehShape;
                        Debug.WriteLine($"Integral_BokehShape updated: {bokehShape}");
                    }
                    break;

                // VirtualLens2 Camera Parameters
                case "/avatar/parameters/VirtualLens2_Enable":
                    if (message.Count > 0)
                    {
                        bool enabled = message[0] switch
                        {
                            bool boolValue => boolValue,
                            float floatValue => floatValue > 0.5f,
                            int intValue => intValue > 0,
                            _ => false
                        };
                        _dataStore.IsVirtualLens2Active = enabled;
                        Debug.WriteLine($"VirtualLens2_Enable updated: {enabled}");
                    }
                    break;
                    
                case "/avatar/parameters/VirtualLens2_Aperture":
                    if (message.Count > 0 && message[0] is float vl2Aperture)
                    {
                        _dataStore.VirtualLens2_Aperture = vl2Aperture;
                        Debug.WriteLine($"VirtualLens2_Aperture updated: {vl2Aperture}");
                    }
                    break;
                    
                case "/avatar/parameters/VirtualLens2_Zoom":
                case "/avatar/parameters/VirtualLens2_FocalLength":
                    if (message.Count > 0 && message[0] is float vl2FocalLength)
                    {
                        _dataStore.VirtualLens2_FocalLength = vl2FocalLength;
                        Debug.WriteLine($"VirtualLens2_FocalLength updated: {vl2FocalLength}");
                    }
                    break;
                    
                case "/avatar/parameters/VirtualLens2_Exposure":
                    if (message.Count > 0 && message[0] is float vl2Exposure)
                    {
                        _dataStore.VirtualLens2_Exposure = vl2Exposure;
                        Debug.WriteLine($"VirtualLens2_Exposure updated: {vl2Exposure}");
                    }
                    break;

                default:
                    // 未知のメッセージは無視（ログ出力はしない）
                    break;
            }
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
