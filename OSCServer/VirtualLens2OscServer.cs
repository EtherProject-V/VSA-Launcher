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
    /// VirtualLens2 カメラ向けのOSC送信サーバー
    /// VRChatへのパラメータ送信とOSCQueryエンドポイントの登録を担当
    /// </summary>
    public class VirtualLens2OscServer : IDisposable
    {
        private const string VRC_IP_ADDRESS = "127.0.0.1";
        private const int VRC_SENDER_PORT = 9000;
        
        private OscSender? _oscSender;
        private CancellationToken _cancellationToken;
        private OSCQueryService? _oscQueryService;
        private OscDataStore _dataStore;

        public VirtualLens2OscServer(int unusedPort, CancellationToken cancellationToken, OscDataStore dataStore, OSCQueryService oscQueryService)
        {
            // unusedPortは互換性のため残すが使用しない（将来的に削除予定）
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

                Console.WriteLine($"[OSC送信] VirtualLens2 OSC Sender started - Target: {VRC_IP_ADDRESS}:{VRC_SENDER_PORT}");
                Debug.WriteLine($"VirtualLens2 OSC Sender started - Target: {VRC_IP_ADDRESS}:{VRC_SENDER_PORT}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VirtualLens2 OSC Sender start error: {ex.Message}");
                Debug.WriteLine($"VirtualLens2 OSC Sender start error: {ex.Message}");
            }
        }

        /// <summary>
        /// VRChatにVirtualLens2カメラのパラメータを送信
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
                Console.WriteLine($"[OSC送信] VirtualLens2: {address} = {value}");
                Debug.WriteLine($"Sent VirtualLens2 parameter: {address} = {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VirtualLens2送信失敗 {parameterName}: {ex.Message}");
                Debug.WriteLine($"Failed to send VirtualLens2 parameter {parameterName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のデータストアの値をVRChatに送信
        /// </summary>
        public void SendCurrentParameters()
        {
            if (!_dataStore.IsVirtualLens2Active) return;

            SendParameter("VirtualLens2_Enable", _dataStore.IsVirtualLens2Active);
            SendParameter("VirtualLens2_Aperture", _dataStore.VirtualLens2_Aperture);
            SendParameter("VirtualLens2_Zoom", _dataStore.VirtualLens2_FocalLength);
            SendParameter("VirtualLens2_Exposure", _dataStore.VirtualLens2_Exposure);
        }

        private void RegisterOscQueryEndpoints()
        {
            if (_oscQueryService == null) return;

            _oscQueryService.AddEndpoint<bool>("/avatar/parameters/VirtualLens2_Enable", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/VirtualLens2_Aperture", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/VirtualLens2_Zoom", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/VirtualLens2_Exposure", Attributes.AccessValues.WriteOnly);
        }

        public void Dispose()
        {
            _oscSender?.Dispose();
        }
    }
}
