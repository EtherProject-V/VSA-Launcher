namespace VSA_launcher.OSCServer
{
    public class OscDataStore
    {
        // VirtualLens2 のデータ
        public float VirtualLens2_Aperture { get; set; } = 0.0f;
        public float VirtualLens2_FocalLength { get; set; } = 0.0f;
        public float VirtualLens2_Exposure { get; set; } = 0.0f;
        public bool IsVirtualLens2Active { get; set; } = false; // VirtualLens2がアクティブかどうか

        // Integral のデータ
        public float Integral_Aperture { get; set; } = 0.0f;
        public float Integral_FocalLength { get; set; } = 0.0f;
        public float Integral_Exposure { get; set; } = 0.0f;
        public float Integral_ShutterSpeed { get; set; } = 0.0f;
        public int Integral_BokehShape { get; set; } = 0;
        public bool IsIntegralActive { get; set; } = false; // Integralがアクティブかどうか

        // コンストラクタ
        public OscDataStore()
        {
            // 初期化
        }

        /// <summary>
        /// 現在のカメラモードを取得
        /// </summary>
        /// <returns>カメラモード名</returns>
        public string GetCurrentCameraMode()
        {
            if (IsIntegralActive)
            {
                return "Integral";
            }
            else if (IsVirtualLens2Active)
            {
                return "VirtualLens2";
            }
            else
            {
                return "Normal";
            }
        }

        /// <summary>
        /// カメラモードの詳細情報を取得（ステータス表示用）
        /// </summary>
        /// <returns>詳細なカメラモード情報</returns>
        public string GetCameraModeStatus()
        {
            if (IsIntegralActive)
            {
                return $"カメラ: Integral (A:{Integral_Aperture:F1})";
            }
            else if (IsVirtualLens2Active)
            {
                return $"カメラ: VirtualLens2 (A:{VirtualLens2_Aperture:F1})";
            }
            else
            {
                return "カメラ: Normal";
            }
        }

        /// <summary>
        /// アクティブなカメラがあるかどうか
        /// </summary>
        /// <returns>アクティブなカメラがある場合はtrue</returns>
        public bool HasActiveCamera()
        {
            return IsIntegralActive || IsVirtualLens2Active;
        }

        /// <summary>
        /// パラメータが変更されたときのイベント
        /// </summary>
        public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

        /// <summary>
        /// パラメータ変更を通知
        /// </summary>
        private void OnParameterChanged(string parameterName, object? oldValue, object newValue)
        {
            ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(parameterName, oldValue, newValue));
        }

        /// <summary>
        /// すべてのパラメータをデフォルト値にリセット
        /// </summary>
        public void ResetToDefaults()
        {
            // VirtualLens2
            VirtualLens2_Aperture = OSCParameterConfig.VirtualLens2.DEFAULT_APERTURE;
            VirtualLens2_FocalLength = OSCParameterConfig.VirtualLens2.DEFAULT_ZOOM;
            VirtualLens2_Exposure = OSCParameterConfig.VirtualLens2.DEFAULT_EXPOSURE;
            IsVirtualLens2Active = false;

            // Integral
            Integral_Aperture = OSCParameterConfig.Integral.DEFAULT_APERTURE;
            Integral_FocalLength = OSCParameterConfig.Integral.DEFAULT_FOCAL_LENGTH;
            Integral_Exposure = OSCParameterConfig.Integral.DEFAULT_EXPOSURE;
            Integral_ShutterSpeed = OSCParameterConfig.Integral.DEFAULT_SHUTTER_SPEED;
            Integral_BokehShape = OSCParameterConfig.Integral.DEFAULT_BOKEH_SHAPE;
            IsIntegralActive = false;
        }

        /// <summary>
        /// パラメータを名前で取得
        /// </summary>
        public object? GetParameterValue(string parameterName)
        {
            return parameterName switch
            {
                OSCParameterConfig.VirtualLens2.ENABLE => IsVirtualLens2Active,
                OSCParameterConfig.VirtualLens2.APERTURE => VirtualLens2_Aperture,
                OSCParameterConfig.VirtualLens2.ZOOM => VirtualLens2_FocalLength,
                OSCParameterConfig.VirtualLens2.EXPOSURE => VirtualLens2_Exposure,
                
                OSCParameterConfig.Integral.ENABLE => IsIntegralActive,
                OSCParameterConfig.Integral.APERTURE => Integral_Aperture,
                OSCParameterConfig.Integral.FOCAL_LENGTH => Integral_FocalLength,
                OSCParameterConfig.Integral.EXPOSURE => Integral_Exposure,
                OSCParameterConfig.Integral.SHUTTER_SPEED => Integral_ShutterSpeed,
                OSCParameterConfig.Integral.BOKEH_SHAPE => Integral_BokehShape,
                
                _ => null
            };
        }

        /// <summary>
        /// パラメータを名前で設定
        /// </summary>
        public bool SetParameterValue(string parameterName, object value)
        {
            try
            {
                var oldValue = GetParameterValue(parameterName);
                
                switch (parameterName)
                {
                    case OSCParameterConfig.VirtualLens2.ENABLE:
                        IsVirtualLens2Active = Convert.ToBoolean(value);
                        break;
                    case OSCParameterConfig.VirtualLens2.APERTURE:
                        VirtualLens2_Aperture = Convert.ToSingle(value);
                        break;
                    case OSCParameterConfig.VirtualLens2.ZOOM:
                        VirtualLens2_FocalLength = Convert.ToSingle(value);
                        break;
                    case OSCParameterConfig.VirtualLens2.EXPOSURE:
                        VirtualLens2_Exposure = Convert.ToSingle(value);
                        break;
                    
                    case OSCParameterConfig.Integral.ENABLE:
                        IsIntegralActive = Convert.ToBoolean(value);
                        break;
                    case OSCParameterConfig.Integral.APERTURE:
                        Integral_Aperture = Convert.ToSingle(value);
                        break;
                    case OSCParameterConfig.Integral.FOCAL_LENGTH:
                        Integral_FocalLength = Convert.ToSingle(value);
                        break;
                    case OSCParameterConfig.Integral.EXPOSURE:
                        Integral_Exposure = Convert.ToSingle(value);
                        break;
                    case OSCParameterConfig.Integral.SHUTTER_SPEED:
                        Integral_ShutterSpeed = Convert.ToSingle(value);
                        break;
                    case OSCParameterConfig.Integral.BOKEH_SHAPE:
                        Integral_BokehShape = Convert.ToInt32(value);
                        break;
                    
                    default:
                        return false;
                }
                
                OnParameterChanged(parameterName, oldValue, value);
                return true;
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
