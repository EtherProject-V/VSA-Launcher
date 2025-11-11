using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VSA_launcher.WebSocket
{
    public class WebSocketMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonProperty("data")]
        public object? Data { get; set; }
    }

    public class VRChatStatusData
    {
        [JsonProperty("is_running")]
        public bool IsRunning { get; set; }
    }

    public class PhotoDetectedData
    {
        [JsonProperty("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [JsonProperty("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        [JsonProperty("world_name")]
        public string WorldName { get; set; } = string.Empty;

        [JsonProperty("capture_time")]
        public DateTime CaptureTime { get; set; }
    }

    public class CompressionSettingsData
    {
        [JsonProperty("auto_compress")]
        public bool AutoCompress { get; set; }

        [JsonProperty("compression_level")]
        public string CompressionLevel { get; set; } = "medium";

        [JsonProperty("original_file_handling")]
        public string OriginalFileHandling { get; set; } = "keep";
    }
}
