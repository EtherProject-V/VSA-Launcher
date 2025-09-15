# ImageControllers Folder Documentation

## Crc32.cs
**役割**: CRC32チェックサムの計算。

### 関数詳細
- **MakeCrcTable()** (private static uint[])
  - 処理: CRC32計算用のテーブルを作成。
  - 期待する値: なし
  - 返す値: uint配列

- **Calculate(uint crc, byte[] buffer)** (private static uint)
  - 処理: CRC32を計算。
  - 期待する値: 初期CRC値、バイト配列
  - 返す値: 計算されたCRC値

- **Hash(uint crc, byte[] buffer)** (public static uint)
  - 処理: CRC32ハッシュを計算。
  - 期待する値: 初期CRC値、バイト配列
  - 返す値: ハッシュ値

## ImageProcessor.cs
**役割**: 画像ファイルの処理・保存を担当。メタデータ処理、フォルダ管理、ファイル名生成を統合。

### 関数詳細
- **ImageProcessor(AppSettings settings, VRChatLogParser logParser, FileWatcherService fileWatcher, Action<string, string> updateStatusAction, OscDataStore oscDataStore)** (コンストラクタ)
  - 処理: ImageProcessorを初期化。
  - 期待する値: AppSettings, VRChatLogParser, FileWatcherService, Actionデリゲート, OscDataStore
  - 返す値: なし

- **ProcessImage(string sourceFilePath)** (public bool)
  - 処理: 画像ファイルを処理して保存先に転送。メタデータ追加、フォルダ作成、名前変更。
  - 期待する値: ソースファイルのパス
  - 返す値: bool - 処理成功時true

(他の関数も存在 - Disposeなど)

## MetadataAnalyzer.cs
**役割**: 画像メタデータの分析。

### 関数詳細
- **ReadMetadataFromImage(string imagePath)** (public static Dictionary<string, string>)
  - 処理: 画像ファイルからメタデータを読み取る。PNGファイルの場合はPngMetadataManagerを使用。
  - 期待する値: 画像ファイルのパス
  - 返す値: メタデータの辞書

## MetadataProcessor.cs
**役割**: メタデータの処理と変更。

### 関数詳細
- **MetadataProcessor(VRChatLogParser logParser, FileWatcherService fileWatcher, Action<string, string> updateStatusAction, OscDataStore oscDataStore)** (コンストラクタ)
  - 処理: MetadataProcessorを初期化。
  - 期待する値: VRChatLogParser, FileWatcherService, Actionデリゲート, OscDataStore
  - 返す値: なし

- **SaveWithMetadata(string sourceFilePath, string destinationPath)** (public bool)
  - 処理: メタデータを付与してファイルを保存。
  - 期待する値: ソースパス、保存先パス
  - 返す値: bool - 成功時true

- **SimpleCopy(string sourceFilePath, string destinationPath)** (public bool)
  - 処理: ファイルを単純コピー。
  - 期待する値: ソースパス、保存先パス
  - 返す値: bool - 成功時true

- **IsProcessedFile(string filePath)** (public bool)
  - 処理: ファイルが処理済みかどうかを確認。
  - 期待する値: ファイルパス
  - 返す値: bool - 処理済み時true

## PngMetadataManager.cs
**役割**: PNGファイルのメタデータ管理。

### 関数詳細
- **AddMetadataToPng(string sourceFilePath, string targetFilePath, Dictionary<string, string> metadata)** (public static bool)
  - 処理: PNGファイルにメタデータを追加。
  - 期待する値: ソースパス、ターゲットパス、メタデータ辞書
  - 返す値: bool - 成功時true

- **ReadMetadataFromPng(string filePath)** (public static Dictionary<string, string>)
  - 処理: PNGファイルからメタデータを読み取り。
  - 期待する値: ファイルパス
  - 返す値: メタデータ辞書

- **ReadMetadata(string filePath)** (public static Dictionary<string, string>)
  - 処理: メタデータを読み取り。
  - 期待する値: ファイルパス
  - 返す値: メタデータ辞書

- **IsProcessedFile(string filePath)** (public static bool)
  - 処理: PNGファイルが処理済みかどうかを確認。
  - 期待する値: ファイルパス
  - 返す値: bool - 処理済み時true

- **WriteMetadata(string filePath, Dictionary<string, string> metadata)** (public static bool)
  - 処理: メタデータを書き込む。
  - 期待する値: ファイルパス、メタデータ辞書
  - 返す値: bool - 成功時true

- **AddVRChatMetadataToPng(string sourceFilePath, string targetFilePath, ...)** (public static bool)
  - 処理: VRChatメタデータをPNGに追加。
  - 期待する値: ソースパス、ターゲットパス、VRChat関連パラメータ
  - 返す値: bool - 成功時true

- **WriteVRChatMetadata(string filePath, ...)** (public static bool)
  - 処理: VRChatメタデータを書き込む。
  - 期待する値: ファイルパス、VRChat関連パラメータ
  - 返す値: bool - 成功時true

- **ExportMetadataToTextFile(string pngFilePath, string? exportPath = null)** (public static string?)
  - 処理: メタデータをテキストファイルにエクスポート。
  - 期待する値: PNGファイルパス、エクスポートパス（オプション）
  - 返す値: エクスポートされたファイルパスまたはnull

## SimplePngMetadataManager.cs
**役割**: 簡易PNGメタデータ管理。PngMetadataManagerに処理を委譲。

### 関数詳細
- **AddMetadataToPng(string sourceFilePath, string targetFilePath, Dictionary<string, string> metadata)** (public static bool)
  - 処理: PngMetadataManagerに委譲してメタデータを追加。
  - 期待する値: ソースパス、ターゲットパス、メタデータ辞書
  - 返す値: bool - 成功時true

- **ReadMetadataFromPng(string filePath)** (public static Dictionary<string, string>)
  - 処理: PngMetadataManagerに委譲してメタデータを読み取り。
  - 期待する値: ファイルパス
  - 返す値: メタデータ辞書

- **ReadMetadata(string filePath)** (public static Dictionary<string, string>)
  - 処理: PngMetadataManagerに委譲してメタデータを読み取り。
  - 期待する値: ファイルパス
  - 返す値: メタデータ辞書

- **IsProcessedFile(string filePath)** (public static bool)
  - 処理: PngMetadataManagerに委譲して処理済み確認。
  - 期待する値: ファイルパス
  - 返す値: bool - 処理済み時true

- **WriteMetadata(string filePath, Dictionary<string, string> metadata)** (public static bool)
  - 処理: PngMetadataManagerに委譲してメタデータを書き込む。
  - 期待する値: ファイルパス、メタデータ辞書
  - 返す値: bool - 成功時true

(他の関数もPngMetadataManagerに委譲)