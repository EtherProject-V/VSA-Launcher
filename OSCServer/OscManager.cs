using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Rug.Osc;
using VRC.OSCQuery;
using System.Diagnostics;

namespace VSA_launcher.OSCServer
{
    /// <summary>
    /// 統合OSCマネージャークラス
    /// VirtualLens2とIntegralの両方のカメラパラメータ送信を一元管理
    /// </summary>
    public class OscManager : IDisposable
    {
        private const string VRC_IP_ADDRESS = "127.0.0.1";
        private const int VRC_SENDER_PORT = 9000;
        
        private OscSender? _oscSender;
        private CancellationToken _cancellationToken;
        private OSCQueryService? _oscQueryService;
        private OscDataStore _dataStore;

        public OscManager(CancellationToken cancellationToken, OscDataStore dataStore, OSCQueryService oscQueryService)
        {
            _cancellationToken = cancellationToken;
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _oscQueryService = oscQueryService;

            RegisterOscQueryEndpoints();
        }

        /// <summary>
        /// OSC送信機能を開始
        /// </summary>
        public void Start()
        {
            try
            {
                // VRChatへの送信用のOscSenderを初期化
                IPAddress address = IPAddress.Parse(VRC_IP_ADDRESS);
                _oscSender = new OscSender(address, VRC_SENDER_PORT);
                _oscSender.Connect();

                Console.WriteLine($"[OSC送信] OSC Manager started - Target: {VRC_IP_ADDRESS}:{VRC_SENDER_PORT}");
                Debug.WriteLine($"OSC Manager started - Target: {VRC_IP_ADDRESS}:{VRC_SENDER_PORT}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] OSC Manager start error: {ex.Message}");
                Debug.WriteLine($"OSC Manager start error: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定されたカメラタイプのパラメータを送信
        /// </summary>
        /// <param name="cameraType">カメラタイプ</param>
        /// <param name="parameterName">パラメータ名</param>
        /// <param name="value">送信する値</param>
        public void SendParameter(CameraType cameraType, string parameterName, object value)
        {
            if (_oscSender == null) return;

            try
            {
                string fullParameterName = GetFullParameterName(cameraType, parameterName);
                string address = $"/avatar/parameters/{fullParameterName}";
                var message = new OscMessage(address, value);
                _oscSender.Send(message);
                
                Console.WriteLine($"[OSC送信] {cameraType}: {address} = {value}");
                Debug.WriteLine($"Sent {cameraType} parameter: {address} = {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] {cameraType}送信失敗 {parameterName}: {ex.Message}");
                Debug.WriteLine($"Failed to send {cameraType} parameter {parameterName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在アクティブなカメラのパラメータをすべて送信
        /// </summary>
        public void SendCurrentActiveParameters()
        {
            switch (_dataStore.ActiveCameraType)
            {
                case CameraType.VirtualLens2:
                    SendVirtualLens2Parameters();
                    break;
                case CameraType.Integral:
                    SendIntegralParameters();
                    break;
                case CameraType.Normal:
                    // Normalモードでは何も送信しない
                    break;
            }
        }

        /// <summary>
        /// VirtualLens2のパラメータをすべて送信
        /// </summary>
        public void SendVirtualLens2Parameters()
        {
            if (!_dataStore.IsVirtualLens2Active) return;

            SendParameter(CameraType.VirtualLens2, "Enable", _dataStore.IsVirtualLens2Active);
            SendParameter(CameraType.VirtualLens2, "Aperture", _dataStore.VirtualLens2_Aperture);
            SendParameter(CameraType.VirtualLens2, "Zoom", _dataStore.VirtualLens2_FocalLength);
            SendParameter(CameraType.VirtualLens2, "Exposure", _dataStore.VirtualLens2_Exposure);
        }

        /// <summary>
        /// Integralのパラメータをすべて送信
        /// </summary>
        public void SendIntegralParameters()
        {
            if (!_dataStore.IsIntegralActive) return;

            SendParameter(CameraType.Integral, "Enable", _dataStore.IsIntegralActive);
            SendParameter(CameraType.Integral, "Aperture", _dataStore.Integral_Aperture);
            SendParameter(CameraType.Integral, "Zoom", _dataStore.Integral_FocalLength);
            SendParameter(CameraType.Integral, "Exposure", _dataStore.Integral_Exposure);
            SendParameter(CameraType.Integral, "ShutterSpeed", _dataStore.Integral_ShutterSpeed);
            SendParameter(CameraType.Integral, "BokehShape", _dataStore.Integral_BokehShape);
        }

        /// <summary>
        /// 特定のカメラタイプのすべてのパラメータをリセット（0に設定）
        /// </summary>
        /// <param name="cameraType">リセットするカメラタイプ</param>
        public async Task ResetCameraParameters(CameraType cameraType)
        {
            try
            {
                Console.WriteLine($"[OSC送信] {cameraType}パラメータリセット開始");

                switch (cameraType)
                {
                    case CameraType.VirtualLens2:
                        SendParameter(CameraType.VirtualLens2, "Enable", false);
                        await Task.Delay(100);
                        SendParameter(CameraType.VirtualLens2, "Aperture", 0.0f);
                        await Task.Delay(100);
                        SendParameter(CameraType.VirtualLens2, "Zoom", 0.0f);
                        await Task.Delay(100);
                        SendParameter(CameraType.VirtualLens2, "Exposure", 0.0f);
                        break;

                    case CameraType.Integral:
                        SendParameter(CameraType.Integral, "Enable", false);
                        await Task.Delay(100);
                        SendParameter(CameraType.Integral, "Aperture", 0.0f);
                        await Task.Delay(100);
                        SendParameter(CameraType.Integral, "Zoom", 0.0f);
                        await Task.Delay(100);
                        SendParameter(CameraType.Integral, "Exposure", 0.0f);
                        await Task.Delay(100);
                        SendParameter(CameraType.Integral, "ShutterSpeed", 0.0f);
                        await Task.Delay(100);
                        SendParameter(CameraType.Integral, "BokehShape", 0);
                        break;
                }

                Console.WriteLine($"[OSC送信] {cameraType}パラメータリセット完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] {cameraType}パラメータリセットエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// カメラタイプとパラメータ名から完全なパラメータ名を生成
        /// </summary>
        /// <param name="cameraType">カメラタイプ</param>
        /// <param name="parameterName">パラメータ名</param>
        /// <returns>完全なパラメータ名</returns>
        private string GetFullParameterName(CameraType cameraType, string parameterName)
        {
            return cameraType switch
            {
                CameraType.VirtualLens2 => $"VirtualLens2_{parameterName}",
                CameraType.Integral => $"Integral_{parameterName}",
                _ => parameterName
            };
        }

        /// <summary>
        /// OSCQueryエンドポイントを登録
        /// </summary>
        private void RegisterOscQueryEndpoints()
        {
            if (_oscQueryService == null) return;

            // VirtualLens2エンドポイント
            _oscQueryService.AddEndpoint<bool>("/avatar/parameters/VirtualLens2_Enable", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/VirtualLens2_Aperture", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/VirtualLens2_Zoom", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/VirtualLens2_Exposure", Attributes.AccessValues.WriteOnly);

            // Integralエンドポイント
            _oscQueryService.AddEndpoint<bool>("/avatar/parameters/Integral_Enable", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Aperture", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Zoom", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Exposure", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_ShutterSpeed", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<int>("/avatar/parameters/Integral_BokehShape", Attributes.AccessValues.WriteOnly);
        }

        /// <summary>
        /// データストアの変更イベントを監視してパラメータを自動送信
        /// </summary>
        public void StartParameterMonitoring()
        {
            _dataStore.ParameterChanged += OnParameterChanged;
        }

        /// <summary>
        /// データストアの変更イベントを停止
        /// </summary>
        public void StopParameterMonitoring()
        {
            _dataStore.ParameterChanged -= OnParameterChanged;
        }

        /// <summary>
        /// パラメータ変更イベントハンドラ
        /// </summary>
        private void OnParameterChanged(object? sender, ParameterChangedEventArgs e)
        {
            try
            {
                // アクティブカメラタイプの変更時は全パラメータ送信
                if (e.ParameterName == nameof(_dataStore.ActiveCameraType))
                {
                    SendCurrentActiveParameters();
                    return;
                }

                // 個別パラメータの変更時
                if (e.ParameterName.Contains("VirtualLens2") && _dataStore.IsVirtualLens2Active)
                {
                    SendVirtualLens2Parameters();
                }
                else if (e.ParameterName.Contains("Integral") && _dataStore.IsIntegralActive)
                {
                    SendIntegralParameters();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] パラメータ変更イベント処理エラー: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopParameterMonitoring();
            _oscSender?.Dispose();
        }
    }
}
