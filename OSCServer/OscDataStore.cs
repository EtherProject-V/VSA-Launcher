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
    }
}
