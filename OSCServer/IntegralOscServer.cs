using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rug.Osc;
using VRC.OSCQuery;
using System.Diagnostics; // Debug.WriteLine を使用するため追加
using System.Net;

namespace VSA_launcher.OSCServer
{
    /// <summary>
    /// Integral カメラ向けのOSC送信サーバー
    /// VRChatへのパラメータ送信とOSCQueryエンドポイントの登録を担当
    /// </summary>
    public class IntegralOscServer : IDisposable
    {
        private const string VRC_IP_ADDRESS = "127.0.0.1";
        private const int VRC_SENDER_PORT = 9000;
        
        private readonly int _port;
        private OscSender? _oscSender;
        private CancellationToken _cancellationToken;
        private OSCQueryService? _oscQueryService;
        private OscDataStore _dataStore;

        public IntegralOscServer(int port, CancellationToken cancellationToken, OscDataStore dataStore, OSCQueryService oscQueryService)
        {
            _port = port;
            _cancellationToken = cancellationToken;
            _dataStore = dataStore;
            _oscQueryService = oscQueryService;

            RegisterOscQueryEndpoints();
        }

        public void Start()
        {
            try
            {
                // VRChatへの送信用のOscSenderを初期化
                IPAddress address = IPAddress.Parse(VRC_IP_ADDRESS);
                _oscSender = new OscSender(address, VRC_SENDER_PORT);
                _oscSender.Connect();

                Debug.WriteLine($"Integral OSC Sender started - Target: {VRC_IP_ADDRESS}:{VRC_SENDER_PORT}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Integral OSC Sender start error: {ex.Message}");
            }
        }

        /// <summary>
        /// VRChatにIntegralカメラのパラメータを送信
        /// </summary>
        /// <param name="parameterName">パラメータ名</param>
        /// <param name="value">送信する値</param>
        public void SendParameter(string parameterName, object value)
        {
            if (_oscSender == null) return;

            try
            {
                string address = $"/avatar/parameters/{parameterName}";
                var message = new OscMessage(address, value);
                _oscSender.Send(message);
                Debug.WriteLine($"Sent Integral parameter: {address} = {value}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send Integral parameter {parameterName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のデータストアの値をVRChatに送信
        /// </summary>
        public void SendCurrentParameters()
        {
            if (!_dataStore.IsIntegralActive) return;

            SendParameter("Integral_Aperture", _dataStore.Integral_Aperture);
            SendParameter("Integral_Zoom", _dataStore.Integral_FocalLength);
            SendParameter("Integral_Exposure", _dataStore.Integral_Exposure);
            SendParameter("Integral_ShutterSpeed", _dataStore.Integral_ShutterSpeed);
            SendParameter("Integral_BokehShape", _dataStore.Integral_BokehShape);
        }

        private void RegisterOscQueryEndpoints()
        {
            if (_oscQueryService == null) return;

            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Aperture", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Zoom", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Exposure", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_ShutterSpeed", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<int>("/avatar/parameters/Integral_BokehShape", Attributes.AccessValues.WriteOnly);
        }

        public void Dispose()
        {
            _oscSender?.Dispose();
        }
    }
}