using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rug.Osc;
using VRC.OSCQuery;
using System.Diagnostics; // Debug.WriteLine を使用するため追加

namespace VSA_launcher.OSCServer
{
    public class IntegralOscServer : IDisposable
    {
        private readonly int _port;
        private OscReceiver? _oscReceiver;
        private Task? _oscWatcher;
        private CancellationToken _cancellationToken;
        private OSCQueryService? _oscQueryService;
        private OscDataStore _dataStore; // _dataStore フィールドの宣言

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
            _oscReceiver = new OscReceiver(_port);
            _oscReceiver.Connect();

            _oscWatcher = new Task(() => OscReceiverWatcher(), _cancellationToken);
            _oscWatcher.Start();

            Debug.WriteLine($"Integral OSC Server started on port {_port}");
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
                Debug.WriteLine($"Integral OSC Receiver error: {ex.Message}");
            }
        }

        private void ProcessOscMessage(OscMessage message)
        {
            switch (message.Address)
            {
                case "/avatar/parameters/Integral_Aperture":
                    if (message.Count > 0 && message[0] is float aperture)
                    {
                        _dataStore.Integral_Aperture = aperture;
                        _dataStore.IsIntegralActive = true;
                        Debug.WriteLine($"Integral_Aperture updated: {aperture}");
                    }
                    break;
                case "/avatar/parameters/Integral_Zoom": // 焦点距離はZoomとして扱われることが多い
                    if (message.Count > 0 && message[0] is float focalLength)
                    {
                        _dataStore.Integral_FocalLength = focalLength;
                        _dataStore.IsIntegralActive = true;
                        Debug.WriteLine($"Integral_FocalLength updated: {focalLength}");
                    }
                    break;
                case "/avatar/parameters/Integral_Exposure":
                    if (message.Count > 0 && message[0] is float exposure)
                    {
                        _dataStore.Integral_Exposure = exposure;
                        _dataStore.IsIntegralActive = true;
                        Debug.WriteLine($"Integral_Exposure updated: {exposure}");
                    }
                    break;
                case "/avatar/parameters/Integral_ShutterSpeed":
                    if (message.Count > 0 && message[0] is float shutterSpeed)
                    {
                        _dataStore.Integral_ShutterSpeed = shutterSpeed;
                        _dataStore.IsIntegralActive = true;
                        Debug.WriteLine($"Integral_ShutterSpeed updated: {shutterSpeed}");
                    }
                    break;
                case "/avatar/parameters/Integral_BokehShape":
                    if (message.Count > 0 && message[0] is int bokehShape)
                    {
                        _dataStore.Integral_BokehShape = bokehShape;
                        _dataStore.IsIntegralActive = true;
                        Debug.WriteLine($"Integral_BokehShape updated: {bokehShape}");
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

            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Aperture", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Zoom", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_Exposure", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<float>("/avatar/parameters/Integral_ShutterSpeed", Attributes.AccessValues.WriteOnly);
            _oscQueryService.AddEndpoint<int>("/avatar/parameters/Integral_BokehShape", Attributes.AccessValues.WriteOnly);
        }

        public void Dispose()
        {
            _oscReceiver?.Dispose();
            _oscWatcher?.Dispose();
        }
    }
}