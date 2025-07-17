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
    }
}
