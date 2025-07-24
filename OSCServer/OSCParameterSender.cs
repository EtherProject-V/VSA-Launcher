using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace VSA_launcher.OSCServer
{
    /// <summary>
    /// OSCパラメータ送信専用クラス
    /// VRChatへのカメラパラメータ送信とデータストア更新を担当
    /// </summary>
    public class OSCParameterSender
    {
        private readonly IntegralOscServer _integralOscServer;
        private readonly VirtualLens2OscServer _virtualLens2OscServer;
        private readonly OscDataStore _oscDataStore;
        private readonly AppSettings _settings;

        public OSCParameterSender(
            IntegralOscServer integralOscServer,
            VirtualLens2OscServer virtualLens2OscServer,
            OscDataStore oscDataStore,
            AppSettings settings)
        {
            _integralOscServer = integralOscServer ?? throw new ArgumentNullException(nameof(integralOscServer));
            _virtualLens2OscServer = virtualLens2OscServer ?? throw new ArgumentNullException(nameof(virtualLens2OscServer));
            _oscDataStore = oscDataStore ?? throw new ArgumentNullException(nameof(oscDataStore));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// カメラパラメータの完全初期化（0→設定値の順番で送信）
        /// </summary>
        public async Task InitializeCameraParameters()
        {
            try
            {
                Console.WriteLine("[OSC送信] カメラパラメータ初期化開始");

                // VirtualLens2とIntegralの両方を並行して初期化
                var virtualLens2Task = InitializeVirtualLens2Parameters();
                var integralTask = InitializeIntegralParameters();
                
                await Task.WhenAll(virtualLens2Task, integralTask);

                Console.WriteLine("[OSC送信] カメラパラメータ初期化完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] カメラパラメータ初期化エラー: {ex.Message}");
                Debug.WriteLine($"OSC初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// VirtualLens2パラメータの初期化（0→設定値）
        /// </summary>
        private async Task InitializeVirtualLens2Parameters()
        {
            try
            {
                Console.WriteLine("[OSC送信] VirtualLens2パラメータ初期化開始");

                // 1. Enableを無効化（0に設定）
                await SendVirtualLens2EnableParameter("VirtualLens2_Enable", false);
                _oscDataStore.IsVirtualLens2Active = false;
                await Task.Delay(100);

                // 2. 全パラメータを0に設定
                await SendVirtualLens2Parameter("VirtualLens2_Aperture", 0.0f);
                await Task.Delay(100);
                await SendVirtualLens2Parameter("VirtualLens2_FocalLength", 0.0f);
                await Task.Delay(100);
                await SendVirtualLens2Parameter("VirtualLens2_Exposure", 0.0f);
                await Task.Delay(100);

                // 3. appsettings.jsonの値を送信（0~100を0~1に変換）
                float aperture = _settings.CameraSettings.VirtualLens2.Aperture / 100.0f;
                float focalLength = _settings.CameraSettings.VirtualLens2.FocalLength / 100.0f;
                float exposure = _settings.CameraSettings.VirtualLens2.Exposure / 100.0f;

                await SendVirtualLens2Parameter("VirtualLens2_Aperture", aperture);
                await Task.Delay(100);
                await SendVirtualLens2Parameter("VirtualLens2_FocalLength", focalLength);
                await Task.Delay(100);
                await SendVirtualLens2Parameter("VirtualLens2_Exposure", exposure);
                await Task.Delay(100);

                // 4. Enable/Disableの切り替え実行（設定値によって決定）
                bool shouldEnable = _settings.CameraSettings.Enabled && _settings.CameraSettings.VirtualLens2.Aperture > 0;
                await SendVirtualLens2EnableParameter("VirtualLens2_Enable", shouldEnable);
                _oscDataStore.IsVirtualLens2Active = shouldEnable;
                
                Console.WriteLine($"[OSC送信] VirtualLens2パラメータ初期化完了 - Enable: {shouldEnable}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VirtualLens2初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Integralパラメータの初期化（0→設定値）
        /// </summary>
        private async Task InitializeIntegralParameters()
        {
            try
            {
                Console.WriteLine("[OSC送信] Integralパラメータ初期化開始");

                // 1. Enableを無効化（0に設定）
                await SendIntegralEnableParameter("Integral_Enable", false);
                _oscDataStore.IsIntegralActive = false;
                await Task.Delay(100);

                // 2. 全パラメータを0に設定
                await SendIntegralParameter("Integral_Aperture", 0.0f);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_FocalLength", 0.0f);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_Exposure", 0.0f);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_ShutterSpeed", 0.0f);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_BokehShape", 0);
                await Task.Delay(100);

                // 3. appsettings.jsonの値を送信（0~100を0~1に変換、BokehShapeは0~10）
                float aperture = _settings.CameraSettings.Integral.Aperture / 100.0f;
                float focalLength = _settings.CameraSettings.Integral.FocalLength / 100.0f;
                float exposure = _settings.CameraSettings.Integral.Exposure / 100.0f;
                float shutterSpeed = _settings.CameraSettings.Integral.ShutterSpeed / 100.0f;
                int bokehShape = (int)(_settings.CameraSettings.Integral.BokehShape / 100.0f * 10);

                await SendIntegralParameter("Integral_Aperture", aperture);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_FocalLength", focalLength);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_Exposure", exposure);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_ShutterSpeed", shutterSpeed);
                await Task.Delay(100);
                await SendIntegralParameter("Integral_BokehShape", bokehShape);
                await Task.Delay(100);

                // 4. Enable/Disableの切り替え実行（設定値によって決定）
                bool shouldEnable = _settings.CameraSettings.Enabled && _settings.CameraSettings.Integral.Aperture > 0;
                await SendIntegralEnableParameter("Integral_Enable", shouldEnable);
                _oscDataStore.IsIntegralActive = shouldEnable;

                Console.WriteLine($"[OSC送信] Integralパラメータ初期化完了 - Enable: {shouldEnable}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] Integral初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// VirtualLens2パラメータを送信し、データストアに反映
        /// </summary>
        private async Task SendVirtualLens2Parameter(string parameterName, float value)
        {
            try
            {
                _virtualLens2OscServer.SendParameter(parameterName, value);
                
                // データストアに反映
                switch (parameterName)
                {
                    case "VirtualLens2_Aperture":
                        _oscDataStore.VirtualLens2_Aperture = value;
                        break;
                    case "VirtualLens2_FocalLength":
                        _oscDataStore.VirtualLens2_FocalLength = value;
                        break;
                    case "VirtualLens2_Exposure":
                        _oscDataStore.VirtualLens2_Exposure = value;
                        break;
                }

                await Task.Delay(50); // 送信間隔を保つ
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VirtualLens2パラメータ送信失敗 {parameterName}: {ex.Message}");
            }
        }

        /// <summary>
        /// VirtualLens2 Enableパラメータを送信し、データストアに反映
        /// </summary>
        private async Task SendVirtualLens2EnableParameter(string parameterName, bool value)
        {
            try
            {
                _virtualLens2OscServer.SendParameter(parameterName, value);
                
                if (parameterName == "VirtualLens2_Enable")
                {
                    _oscDataStore.IsVirtualLens2Active = value;
                }

                await Task.Delay(50); // 送信間隔を保つ
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VirtualLens2 Enableパラメータ送信失敗 {parameterName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Integralパラメータを送信し、データストアに反映
        /// </summary>
        private async Task SendIntegralParameter(string parameterName, object value)
        {
            try
            {
                _integralOscServer.SendParameter(parameterName, value);
                
                // データストアに反映
                switch (parameterName)
                {
                    case "Integral_Aperture":
                        _oscDataStore.Integral_Aperture = (float)value;
                        break;
                    case "Integral_FocalLength":
                        _oscDataStore.Integral_FocalLength = (float)value;
                        break;
                    case "Integral_Exposure":
                        _oscDataStore.Integral_Exposure = (float)value;
                        break;
                    case "Integral_ShutterSpeed":
                        _oscDataStore.Integral_ShutterSpeed = (float)value;
                        break;
                    case "Integral_BokehShape":
                        _oscDataStore.Integral_BokehShape = (int)value;
                        break;
                }

                await Task.Delay(50); // 送信間隔を保つ
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] Integralパラメータ送信失敗 {parameterName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Integral Enableパラメータを送信し、データストアに反映
        /// </summary>
        private async Task SendIntegralEnableParameter(string parameterName, bool value)
        {
            try
            {
                _integralOscServer.SendParameter(parameterName, value);
                
                if (parameterName == "Integral_Enable")
                {
                    _oscDataStore.IsIntegralActive = value;
                }

                await Task.Delay(50); // 送信間隔を保つ
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] Integral Enableパラメータ送信失敗 {parameterName}: {ex.Message}");
            }
        }
    }
}
