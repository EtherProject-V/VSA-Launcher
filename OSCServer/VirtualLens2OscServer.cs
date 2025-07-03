using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rug.Osc;
using VRC.OSCQuery;
using System.Diagnostics; // Debug.WriteLine を使用するため追加

namespace VSA_launcher.OSCServer
{
    public class VirtualLens2OscServer : IDisposable
    {
        private readonly int _port;
        private OscReceiver? _oscReceiver;
        private Task? _oscWatcher;
        private CancellationToken _cancellationToken;
        private OSCQueryService? _oscQueryService;
        private OscDataStore _dataStore; // _dataStore フィールドの宣言

        public VirtualLens2OscServer(int port, CancellationToken cancellationToken, OscDataStore dataStore, OSCQueryService oscQueryService)
        {
            _port = port;
            _cancellationToken = cancellationToken;
            _dataStore = dataStore;
            _oscQueryService = oscQueryService;

            RegisterOscQueryEndpoints();
        }

        public void Start()
        {
            _oscReceiver = new OscReceiver(_port);
            _oscReceiver.Connect();

            _oscWatcher = new Task(() => OscReceiverWatcher(), _cancellationToken);
            _oscWatcher.Start();

            Debug.WriteLine($"VirtualLens2 OSC Server started on port {_port}");
        }

        private void OscReceiverWatcher()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    if (_oscReceiver is not null && _oscReceiver.TryReceive(out var packet))
                    {
                        if (packet is OscMessage message)
                        {
                            ProcessOscMessage(message);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1); // CPU使用率を抑える
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VirtualLens2 OSC Receiver error: {ex.Message}");
            }
        }

        private void ProcessOscMessage(OscMessage message)
        {
            switch (message.Address)
            {
                case "/avatar/parameters/VirtualLens2_Enable":
                    if (message.Count > 0 && message[0] is bool enabled)
                    {
                        _dataStore.IsVirtualLens2Active = enabled;
                        Debug.WriteLine($"VirtualLens2_Enable updated: {enabled}");
                    }
                    break;
                case "/avatar/parameters/VirtualLens2_Aperture":
                    if (message.Count > 0 && message[0] is float aperture)
                    {
                        _dataStore.VirtualLens2_Aperture = aperture;
                        Debug.WriteLine($"VirtualLens2_Aperture updated: {aperture}");
                    }
                    break;
                case "/avatar/parameters/VirtualLens2_Zoom": 
                    if (message.Count > 0 && message[0] is float focalLength)
                    {
                        _dataStore.VirtualLens2_FocalLength = focalLength;
                        Debug.WriteLine($"VirtualLens2_FocalLength updated: {focalLength}");
                    }
                    break;
                case "/avatar/parameters/VirtualLens2_Exposure":
                    if (message.Count > 0 && message[0] is float exposure)
                    {
                        _dataStore.VirtualLens2_Exposure = exposure;
                        Debug.WriteLine($"VirtualLens2_Exposure updated: {exposure}");
                    }
                    break;
                default:
                    // 未知のメッセージは無視
                    break;
            }
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
            _oscReceiver?.Dispose();
            _oscWatcher?.Dispose();
        }
    }
}