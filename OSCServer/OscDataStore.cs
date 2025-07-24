using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSA_launcher.OSCServer
{
    /// <summary>
    /// カメラの種類
    /// </summary>
    public enum CameraType
    {
        Normal,
        VirtualLens2,
        Integral
    }

    /// <summary>
    /// カメラパラメータの種類
    /// </summary>
    public enum ParameterType
    {
        Aperture,
        FocalLength,
        Exposure,
        ShutterSpeed,  // Integral専用
        BokehShape     // Integral専用
    }

    /// <summary>
    /// OSCパラメータ設定定数
    /// </summary>
    public static class OSCParameterConfig
    {
        public static class VirtualLens2
        {
            public const string ENABLE = "/avatar/parameters/VirtualLens2_Enable";
            public const string APERTURE = "/avatar/parameters/VirtualLens2_Aperture";
            public const string ZOOM = "/avatar/parameters/VirtualLens2_Zoom";
            public const string EXPOSURE = "/avatar/parameters/VirtualLens2_Exposure";
            
            public const float DEFAULT_APERTURE = 0.0f;
            public const float DEFAULT_ZOOM = 0.0f;
            public const float DEFAULT_EXPOSURE = 0.0f;
        }

        public static class Integral
        {
            public const string ENABLE = "/avatar/parameters/Integral_Enable";
            public const string APERTURE = "/avatar/parameters/Integral_Aperture";
            public const string FOCAL_LENGTH = "/avatar/parameters/Integral_FocalLength";
            public const string EXPOSURE = "/avatar/parameters/Integral_Exposure";
            public const string SHUTTER_SPEED = "/avatar/parameters/Integral_ShutterSpeed";
            public const string BOKEH_SHAPE = "/avatar/parameters/Integral_BokehShape";
            
            public const float DEFAULT_APERTURE = 0.0f;
            public const float DEFAULT_FOCAL_LENGTH = 0.0f;
            public const float DEFAULT_EXPOSURE = 0.0f;
            public const float DEFAULT_SHUTTER_SPEED = 0.0f;
            public const int DEFAULT_BOKEH_SHAPE = 0;
        }
    }

    public class OscDataStore : INotifyPropertyChanged
    {
        private readonly Dictionary<CameraType, Dictionary<ParameterType, object>> _parameters;
        private CameraType _activeCameraType = CameraType.Normal;

        public CameraType ActiveCameraType
        {
            get => _activeCameraType;
            set
            {
                if (_activeCameraType != value)
                {
                    var oldValue = _activeCameraType;
                    _activeCameraType = value;
                    OnPropertyChanged();
                    OnParameterChanged(nameof(ActiveCameraType), oldValue, value);
                }
            }
        }

        // 後方互換性のためのプロパティ（既存コードとの互換性維持）
        public float VirtualLens2_Aperture 
        { 
            get => GetParameterValue<float>(CameraType.VirtualLens2, ParameterType.Aperture);
            set => SetParameterValue(CameraType.VirtualLens2, ParameterType.Aperture, value);
        }
        
        public float VirtualLens2_FocalLength 
        { 
            get => GetParameterValue<float>(CameraType.VirtualLens2, ParameterType.FocalLength);
            set => SetParameterValue(CameraType.VirtualLens2, ParameterType.FocalLength, value);
        }
        
        public float VirtualLens2_Exposure 
        { 
            get => GetParameterValue<float>(CameraType.VirtualLens2, ParameterType.Exposure);
            set => SetParameterValue(CameraType.VirtualLens2, ParameterType.Exposure, value);
        }
        
        public bool IsVirtualLens2Active 
        { 
            get => ActiveCameraType == CameraType.VirtualLens2;
            set => ActiveCameraType = value ? CameraType.VirtualLens2 : CameraType.Normal;
        }

        public float Integral_Aperture 
        { 
            get => GetParameterValue<float>(CameraType.Integral, ParameterType.Aperture);
            set => SetParameterValue(CameraType.Integral, ParameterType.Aperture, value);
        }
        
        public float Integral_FocalLength 
        { 
            get => GetParameterValue<float>(CameraType.Integral, ParameterType.FocalLength);
            set => SetParameterValue(CameraType.Integral, ParameterType.FocalLength, value);
        }
        
        public float Integral_Exposure 
        { 
            get => GetParameterValue<float>(CameraType.Integral, ParameterType.Exposure);
            set => SetParameterValue(CameraType.Integral, ParameterType.Exposure, value);
        }
        
        public float Integral_ShutterSpeed 
        { 
            get => GetParameterValue<float>(CameraType.Integral, ParameterType.ShutterSpeed);
            set => SetParameterValue(CameraType.Integral, ParameterType.ShutterSpeed, value);
        }
        
        public int Integral_BokehShape 
        { 
            get => GetParameterValue<int>(CameraType.Integral, ParameterType.BokehShape);
            set => SetParameterValue(CameraType.Integral, ParameterType.BokehShape, value);
        }
        
        public bool IsIntegralActive 
        { 
            get => ActiveCameraType == CameraType.Integral;
            set => ActiveCameraType = value ? CameraType.Integral : CameraType.Normal;
        }

        // コンストラクタ
        public OscDataStore()
        {
            _parameters = InitializeParameters();
        }

        /// <summary>
        /// パラメータの初期化
        /// </summary>
        private Dictionary<CameraType, Dictionary<ParameterType, object>> InitializeParameters()
        {
            var parameters = new Dictionary<CameraType, Dictionary<ParameterType, object>>();

            // VirtualLens2のパラメータ初期化
            parameters[CameraType.VirtualLens2] = new Dictionary<ParameterType, object>
            {
                [ParameterType.Aperture] = OSCParameterConfig.VirtualLens2.DEFAULT_APERTURE,
                [ParameterType.FocalLength] = OSCParameterConfig.VirtualLens2.DEFAULT_ZOOM,
                [ParameterType.Exposure] = OSCParameterConfig.VirtualLens2.DEFAULT_EXPOSURE
            };

            // Integralのパラメータ初期化
            parameters[CameraType.Integral] = new Dictionary<ParameterType, object>
            {
                [ParameterType.Aperture] = OSCParameterConfig.Integral.DEFAULT_APERTURE,
                [ParameterType.FocalLength] = OSCParameterConfig.Integral.DEFAULT_FOCAL_LENGTH,
                [ParameterType.Exposure] = OSCParameterConfig.Integral.DEFAULT_EXPOSURE,
                [ParameterType.ShutterSpeed] = OSCParameterConfig.Integral.DEFAULT_SHUTTER_SPEED,
                [ParameterType.BokehShape] = OSCParameterConfig.Integral.DEFAULT_BOKEH_SHAPE
            };

            return parameters;
        }

        /// <summary>
        /// 指定されたカメラタイプとパラメータタイプの値を取得
        /// </summary>
        public T GetParameterValue<T>(CameraType cameraType, ParameterType parameterType)
        {
            if (_parameters.TryGetValue(cameraType, out var cameraParams) &&
                cameraParams.TryGetValue(parameterType, out var value))
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            return default(T)!;
        }

        /// <summary>
        /// 指定されたカメラタイプとパラメータタイプの値を設定
        /// </summary>
        public bool SetParameterValue<T>(CameraType cameraType, ParameterType parameterType, T value)
        {
            try
            {
                if (_parameters.TryGetValue(cameraType, out var cameraParams))
                {
                    var oldValue = cameraParams.TryGetValue(parameterType, out var currentValue) ? currentValue : null;
                    cameraParams[parameterType] = value!;
                    OnParameterChanged($"{cameraType}_{parameterType}", oldValue, value!);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 現在のカメラモードを取得
        /// </summary>
        /// <returns>カメラモード名</returns>
        public string GetCurrentCameraMode() => ActiveCameraType.ToString();

        /// <summary>
        /// カメラモードの詳細情報を取得（ステータス表示用）
        /// </summary>
        /// <returns>詳細なカメラモード情報</returns>
        public string GetCameraModeStatus()
        {
            if (ActiveCameraType == CameraType.Normal)
                return "カメラ: Normal";

            var aperture = GetParameterValue<float>(ActiveCameraType, ParameterType.Aperture);
            return $"カメラ: {ActiveCameraType} (A:{aperture:F1})";
        }

        /// <summary>
        /// アクティブなカメラがあるかどうか
        /// </summary>
        /// <returns>アクティブなカメラがある場合はtrue</returns>
        public bool HasActiveCamera() => ActiveCameraType != CameraType.Normal;

        /// <summary>
        /// パラメータが変更されたときのイベント
        /// </summary>
        public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

        /// <summary>
        /// プロパティ変更イベント
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// パラメータ変更を通知
        /// </summary>
        private void OnParameterChanged(string parameterName, object? oldValue, object? newValue)
        {
            ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(parameterName, oldValue, newValue));
        }

        /// <summary>
        /// プロパティ変更通知
        /// </summary>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// すべてのパラメータをデフォルト値にリセット
        /// </summary>
        public void ResetToDefaults()
        {
            ActiveCameraType = CameraType.Normal;
            
            // VirtualLens2
            SetParameterValue(CameraType.VirtualLens2, ParameterType.Aperture, OSCParameterConfig.VirtualLens2.DEFAULT_APERTURE);
            SetParameterValue(CameraType.VirtualLens2, ParameterType.FocalLength, OSCParameterConfig.VirtualLens2.DEFAULT_ZOOM);
            SetParameterValue(CameraType.VirtualLens2, ParameterType.Exposure, OSCParameterConfig.VirtualLens2.DEFAULT_EXPOSURE);

            // Integral
            SetParameterValue(CameraType.Integral, ParameterType.Aperture, OSCParameterConfig.Integral.DEFAULT_APERTURE);
            SetParameterValue(CameraType.Integral, ParameterType.FocalLength, OSCParameterConfig.Integral.DEFAULT_FOCAL_LENGTH);
            SetParameterValue(CameraType.Integral, ParameterType.Exposure, OSCParameterConfig.Integral.DEFAULT_EXPOSURE);
            SetParameterValue(CameraType.Integral, ParameterType.ShutterSpeed, OSCParameterConfig.Integral.DEFAULT_SHUTTER_SPEED);
            SetParameterValue(CameraType.Integral, ParameterType.BokehShape, OSCParameterConfig.Integral.DEFAULT_BOKEH_SHAPE);
        }

        /// <summary>
        /// パラメータ名を解析してカメラタイプとパラメータタイプを取得
        /// </summary>
        private (CameraType?, ParameterType?) ParseParameterName(string parameterName)
        {
            CameraType? cameraType = null;
            ParameterType? parameterType = null;

            // カメラタイプの判定
            if (parameterName.Contains("VirtualLens2"))
                cameraType = CameraType.VirtualLens2;
            else if (parameterName.Contains("Integral"))
                cameraType = CameraType.Integral;

            // パラメータタイプの判定
            if (parameterName.Contains("APERTURE"))
                parameterType = ParameterType.Aperture;
            else if (parameterName.Contains("ZOOM") || parameterName.Contains("FOCAL_LENGTH"))
                parameterType = ParameterType.FocalLength;
            else if (parameterName.Contains("EXPOSURE"))
                parameterType = ParameterType.Exposure;
            else if (parameterName.Contains("SHUTTER_SPEED"))
                parameterType = ParameterType.ShutterSpeed;
            else if (parameterName.Contains("BOKEH_SHAPE"))
                parameterType = ParameterType.BokehShape;

            return (cameraType, parameterType);
        }

        /// <summary>
        /// パラメータを名前で取得（既存互換性のため）
        /// </summary>
        public object? GetParameterValue(string parameterName)
        {
            var (cameraType, parameterType) = ParseParameterName(parameterName);
            if (cameraType.HasValue && parameterType.HasValue)
            {
                return GetParameterValue<object>(cameraType.Value, parameterType.Value);
            }
            
            // アクティブ状態の取得
            if (parameterName.Contains("ENABLE"))
            {
                if (parameterName.Contains("VirtualLens2"))
                    return ActiveCameraType == CameraType.VirtualLens2;
                if (parameterName.Contains("Integral"))
                    return ActiveCameraType == CameraType.Integral;
            }
            
            return null;
        }

        /// <summary>
        /// パラメータを名前で設定（既存互換性のため）
        /// </summary>
        public bool SetParameterValue(string parameterName, object value)
        {
            try
            {
                // アクティブ状態の設定
                if (parameterName.Contains("ENABLE"))
                {
                    var isActive = Convert.ToBoolean(value);
                    if (parameterName.Contains("VirtualLens2"))
                    {
                        ActiveCameraType = isActive ? CameraType.VirtualLens2 : CameraType.Normal;
                        return true;
                    }
                    if (parameterName.Contains("Integral"))
                    {
                        ActiveCameraType = isActive ? CameraType.Integral : CameraType.Normal;
                        return true;
                    }
                }

                var (cameraType, parameterType) = ParseParameterName(parameterName);
                if (cameraType.HasValue && parameterType.HasValue)
                {
                    return SetParameterValue(cameraType.Value, parameterType.Value, value);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// パラメータ変更イベントの引数
    /// </summary>
    public class ParameterChangedEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public ParameterChangedEventArgs(string parameterName, object? oldValue, object? newValue)
        {
            ParameterName = parameterName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
