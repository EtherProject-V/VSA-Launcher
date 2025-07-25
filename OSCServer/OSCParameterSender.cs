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
        private readonly OscManager _oscManager;
        private readonly OscDataStore _oscDataStore;
        private readonly AppSettings _settings;

        public OSCParameterSender(
            OscManager oscManager,
            OscDataStore oscDataStore,
            AppSettings settings)
        {
            _oscManager = oscManager ?? throw new ArgumentNullException(nameof(oscManager));
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
        /// カメラパラメータのリセット（0に設定）
        /// </summary>
        public async Task ResetCameraParameters(CameraType cameraType)
        {
            try
            {
                Console.WriteLine($"[OSC送信] {cameraType}パラメータリセット開始");

                switch (cameraType)
                {
                    case CameraType.VirtualLens2:
                        _oscManager.SendParameter(CameraType.VirtualLens2, "Enable", false);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.VirtualLens2, "Aperture", 0.0f);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.VirtualLens2, "Zoom", 0.0f);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.VirtualLens2, "Exposure", 0.0f);
                        break;

                    case CameraType.Integral:
                        _oscManager.SendParameter(CameraType.Integral, "Enable", false);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.Integral, "Aperture", 0.0f);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.Integral, "Zoom", 0.0f);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.Integral, "Exposure", 0.0f);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.Integral, "ShutterSpeed", 0.0f);
                        await Task.Delay(100);
                        _oscManager.SendParameter(CameraType.Integral, "BokehShape", 0);
                        break;
                }

                Console.WriteLine($"[OSC送信] {cameraType}パラメータリセット完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] {cameraType}パラメータリセットエラー: {ex.Message}");
                Debug.WriteLine($"{cameraType}パラメータリセットエラー: {ex.Message}");
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

                // 1. リセット処理
                await ResetCameraParameters(CameraType.VirtualLens2);
                await Task.Delay(500);

                // 2. appsettings.jsonの値を送信（0~100を0~1に変換）
                float aperture = _settings.CameraSettings.VirtualLens2.Aperture / 100.0f;
                float focalLength = _settings.CameraSettings.VirtualLens2.FocalLength / 100.0f;
                float exposure = _settings.CameraSettings.VirtualLens2.Exposure / 100.0f;

                _oscManager.SendParameter(CameraType.VirtualLens2, "Aperture", aperture);
                await Task.Delay(100);
                _oscManager.SendParameter(CameraType.VirtualLens2, "Zoom", focalLength);
                await Task.Delay(100);
                _oscManager.SendParameter(CameraType.VirtualLens2, "Exposure", exposure);

                Console.WriteLine("[OSC送信] VirtualLens2パラメータ初期化完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] VirtualLens2初期化エラー: {ex.Message}");
                Debug.WriteLine($"VirtualLens2初期化エラー: {ex.Message}");
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

                // 1. リセット処理
                await ResetCameraParameters(CameraType.Integral);
                await Task.Delay(500);

                // 2. appsettings.jsonの値を送信（0~100を0~1に変換）
                float aperture = _settings.CameraSettings.Integral.Aperture / 100.0f;
                float focalLength = _settings.CameraSettings.Integral.FocalLength / 100.0f;
                float exposure = _settings.CameraSettings.Integral.Exposure / 100.0f;
                float shutterSpeed = _settings.CameraSettings.Integral.ShutterSpeed / 100.0f;
                int bokehShape = _settings.CameraSettings.Integral.BokehShape;

                _oscManager.SendParameter(CameraType.Integral, "Aperture", aperture);
                await Task.Delay(100);
                _oscManager.SendParameter(CameraType.Integral, "Zoom", focalLength);
                await Task.Delay(100);
                _oscManager.SendParameter(CameraType.Integral, "Exposure", exposure);
                await Task.Delay(100);
                _oscManager.SendParameter(CameraType.Integral, "ShutterSpeed", shutterSpeed);
                await Task.Delay(100);
                _oscManager.SendParameter(CameraType.Integral, "BokehShape", bokehShape);

                Console.WriteLine("[OSC送信] Integralパラメータ初期化完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] Integral初期化エラー: {ex.Message}");
                Debug.WriteLine($"Integral初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// VirtualLens2パラメータを手動で送信
        /// </summary>
        public void SendVirtualLens2Parameter(string parameterName, object value)
        {
            if (_settings.LauncherSettings.OSCSettings.Enabled)
            {
                string shortName = parameterName.Replace("VirtualLens2_", "");
                _oscManager.SendParameter(CameraType.VirtualLens2, shortName, value);
                
                // データストアも更新
                switch (shortName.ToLower())
                {
                    case "aperture":
                        if (value is float apertureValue) _oscDataStore.VirtualLens2_Aperture = apertureValue;
                        break;
                    case "zoom":
                    case "focallength":
                        if (value is float zoomValue) _oscDataStore.VirtualLens2_FocalLength = zoomValue;
                        break;
                    case "exposure":
                        if (value is float exposureValue) _oscDataStore.VirtualLens2_Exposure = exposureValue;
                        break;
                }
            }
        }

        /// <summary>
        /// Integralパラメータを手動で送信
        /// </summary>
        public void SendIntegralParameter(string parameterName, object value)
        {
            if (_settings.LauncherSettings.OSCSettings.Enabled)
            {
                string shortName = parameterName.Replace("Integral_", "");
                _oscManager.SendParameter(CameraType.Integral, shortName, value);
                
                // データストアも更新
                switch (shortName.ToLower())
                {
                    case "aperture":
                        if (value is float apertureValue) _oscDataStore.Integral_Aperture = apertureValue;
                        break;
                    case "zoom":
                    case "focallength":
                        if (value is float zoomValue) _oscDataStore.Integral_FocalLength = zoomValue;
                        break;
                    case "exposure":
                        if (value is float exposureValue) _oscDataStore.Integral_Exposure = exposureValue;
                        break;
                    case "shutterspeed":
                        if (value is float shutterValue) _oscDataStore.Integral_ShutterSpeed = shutterValue;
                        break;
                    case "bokehshape":
                        if (value is int bokehValue) _oscDataStore.Integral_BokehShape = bokehValue;
                        break;
                }
            }
        }

        /// <summary>
        /// カメラ有効化パラメータを送信
        /// </summary>
        public void SendCameraEnableParameter(CameraType cameraType, bool enabled)
        {
            if (_settings.LauncherSettings.OSCSettings.Enabled)
            {
                _oscManager.SendParameter(cameraType, "Enable", enabled);
                
                // データストアも更新
                switch (cameraType)
                {
                    case CameraType.VirtualLens2:
                        _oscDataStore.IsVirtualLens2Active = enabled;
                        break;
                    case CameraType.Integral:
                        _oscDataStore.IsIntegralActive = enabled;
                        break;
                }
            }
        }

        /// <summary>
        /// 現在アクティブなカメラのパラメータをすべて送信
        /// </summary>
        public void SendCurrentActiveParameters()
        {
            if (_settings.LauncherSettings.OSCSettings.Enabled)
            {
                _oscManager.SendCurrentActiveParameters();
            }
        }
    }
}
