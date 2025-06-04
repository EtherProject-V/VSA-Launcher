# VSA C# Launcher - 完全なコード解析とアーキテクチャドキュメント

## 概要

VRChat Snap Archive (VSA) Launcherは、VRChatのスクリーンショットを自動的に検出し、メタデータを付与して整理・保存するWindows Formsアプリケーションです。

## アーキテクチャ図

```
┌─────────────────────────────────────────────────────────────────┐
│                    VSA Launcher アーキテクチャ                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐    ┌─────────────────┐    ┌──────────────┐ │
│  │    Program.cs   │    │   Form1.cs      │    │ SystemTray   │ │
│  │  (起動・重複    │    │  (メインUI)     │    │ Icon         │ │
│  │   チェック)     │────│                 │────│              │ │
│  └─────────────────┘    └─────────────────┘    └──────────────┘ │
│                                 │                               │
│  ┌─────────────────┐    ┌─────────────────┐    ┌──────────────┐ │
│  │ SettingsManager │    │ StartupManager  │    │ AppSettings  │ │
│  │ (設定管理)      │────│ (自動起動)      │────│ (設定データ) │ │
│  └─────────────────┘    └─────────────────┘    └──────────────┘ │
│                                                                 │
│  ┌─────────────────┐    ┌─────────────────┐    ┌──────────────┐ │
│  │FileWatcherService│    │VRChatLogParser  │    │ImageProcessor│ │
│  │ (ファイル監視)   │────│ (ログ解析)      │────│ (画像処理)   │ │
│  └─────────────────┘    └─────────────────┘    └──────────────┘ │
│           │                        │                    │       │
│  ┌─────────────────┐    ┌─────────────────┐    ┌──────────────┐ │
│  │ FolderStructure │    │PngMetadataManager│    │FileNameGen   │ │
│  │ Manager         │    │ (メタデータ)     │    │ erator       │ │
│  └─────────────────┘    └─────────────────┘    └──────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## コアコンポーネント分析

### 1. Program.cs - アプリケーション起動制御

#### 主要機能
- **重複起動防止**: Mutexを使用した単一インスタンス保証
- **アプリケーション初期化**: High DPI対応、Visual Styles有効化
- **リソース管理**: 終了時のMutex解放

#### 処理フロー
```csharp
Main() → Mutex作成 → 重複チェック → Application.Run → リソース解放
```

#### 重要なコード
```csharp
// 重複起動チェック用ミューテックス
_mutex = new Mutex(true, appMutexName, out createdNew);

if (!createdNew)
{
    MessageBox.Show("SnapArchive Launcher は既に起動しています。");
    return; // アプリケーション終了
}
```

### 2. Form1.cs (VSA_launcher) - メインUIとオーケストレーション

#### プライベートフィールド
```csharp
private FolderBrowserDialog screenshotFolderBrowser;     // スクリーンショットフォルダ選択
private FolderBrowserDialog outputFolderBrowser;        // 出力フォルダ選択
private SystemTrayIcon _systemTrayIcon;                 // システムトレイ管理
private AppSettings _settings;                          // アプリケーション設定
private FileWatcherService _fileWatcher;                // ファイル監視サービス
private VRChatLogParser _logParser;                     // VRChatログ解析
private ImageProcessor _imageProcessor;                 // 画像処理エンジン
private FolderStructureManager _folderManager;          // フォルダ構造管理
private FileNameGenerator _fileNameGenerator;           // ファイル名生成
```

#### コンストラクタ初期化フロー
```csharp
1. InitializeComponent()                    // UIコンポーネント初期化
2. SettingsManager.LoadSettings()           // 設定ファイル読み込み
3. SystemTrayIcon初期化                     // システムトレイ設定
4. FileWatcherService初期化                 // ファイル監視開始
5. イベントハンドラ登録                      // UI要素にイベント登録
6. VRChatLogParser初期化                    // ログ解析開始
7. タイマー設定                             // 定期更新処理
```

#### 主要メソッド

##### `LaunchMainApplication()`
```csharp
// Electronアプリケーションの起動
// パス探索 → プロセス起動 → 状態更新
```

##### `StartWatching()`
```csharp
// ファイル監視開始
// パス検証 → FileWatcherService.StartWatching() → 状態更新
```

##### `ProcessFile(string sourceFilePath)`
```csharp
// 個別ファイル処理
// メタデータ付与 → ファイルコピー → 進捗更新
```

#### UIイベントハンドラ群

##### ファイル・フォルダ選択
- `screenShotFile_button_Click()`: スクリーンショットフォルダ選択
- `outPut_button_Click()`: 出力フォルダ選択

##### 設定変更
- `metadataEnabled_CheckedChanged()`: メタデータ有効/無効切り替え
- `monthCompression_CheckedChanged()`: 月間圧縮設定
- `radioButton_CheckedChanged()`: フォルダ構造設定（月/週/日）

##### スタートアップ管理
- `startUp_checkBox_CheckedChanged()`: Windows自動起動設定

### 3. StartupManager.cs - Windows自動起動管理

#### 機能概要
Windowsレジストリを操作してアプリケーションの自動起動を制御

#### レジストリ操作
```csharp
// レジストリキー
private const string RUN_LOCATION = @"Software\Microsoft\Windows\CurrentVersion\Run";
private const string APP_NAME = "VrcSnapArchiveKai";
```

#### 主要メソッド

##### `RegisterInStartup()`
```csharp
1. レジストリキーを開く (Registry.CurrentUser)
2. 実行可能ファイルパスを取得
3. .dll → .exe パス変換（必要に応じて）
4. レジストリに登録
```

##### `RemoveFromStartup()`
```csharp
1. レジストリキーを開く
2. APP_NAME エントリを削除
```

##### `IsRegisteredInStartup()`
```csharp
1. レジストリから値を読み取り
2. 登録状態を確認して返す
```

### 4. Settings関連 - 設定管理システム

#### AppSettings.cs - 設定データ構造
```csharp
public class AppSettings
{
    public string ScreenshotPath { get; set; }            // スクリーンショットパス
    public string OutputPath { get; set; }               // 出力パス
    public FolderStructureSettings FolderStructure;      // フォルダ構造設定
    public FileRenaming FileRenaming;                    // ファイル名変更設定
    public Metadata Metadata;                           // メタデータ設定
    public Compression Compression;                      // 圧縮設定
    public Performance Performance;                      // パフォーマンス設定
    public LauncherSettings LauncherSettings;           // ランチャー設定
}
```

#### SettingsManager.cs - 設定永続化
```csharp
LoadSettings() → appsettings.json読み込み → AppSettingsオブジェクト生成
SaveSettings() → AppSettingsシリアライゼ → appsettings.json書き込み
```

### 5. FileWatcherService.cs - ファイル監視エンジン

#### 主要機能
- VRChatスクリーンショットフォルダの監視
- 新規ファイル自動検出
- 処理済みファイルのスキップ
- 月別フォルダ対応

#### アーキテクチャ
```csharp
FileSystemWatcher[] → OnFileCreated → ProcessNewFile → ProcessFile
```

#### ファイル処理フロー
```csharp
1. OnFileCreated(FileSystemEventArgs)           // ファイル作成検出
2. Task.Run(() => ProcessNewFile(filePath))     // 非同期処理開始
3. WaitForFileAccess(filePath)                  // ファイルロック解除待機
4. IsPngFile(filePath)                          // PNG形式チェック
5. IsProcessedFile(filePath)                    // 処理済みチェック
6. GetTargetPath(filePath)                      // 出力先パス計算
7. ProcessFile(sourceFilePath, destinationPath) // メタデータ付与・コピー
```

#### 重要なメソッド

##### `ProcessNewFile(string filePath)`
```csharp
// 新規ファイル処理のメインフロー
await WaitForFileAccess(filePath);              // ファイルアクセス待機
if (!IsPngFile(filePath)) return;               // PNG以外はスキップ
if (IsProcessedFile(filePath)) return;          // 処理済みはスキップ
RaiseFileDetected(filePath);                    // 検出イベント発火
string destinationPath = GetTargetPath(filePath); // 出力先決定
ProcessFile(filePath, destinationPath);         // 実際の処理実行
```

##### `ProcessFile(string sourceFilePath, string destinationPath)`
```csharp
// メタデータ付与とファイルコピー
_logParser.ParseLatestLog();                    // 最新ログ解析
var metadata = new Dictionary<string, string>   // メタデータ構築
{
    { "WorldName", _logParser.CurrentWorldName },
    { "WorldID", _logParser.CurrentWorldId },
    { "Usernames", string.Join(".", _logParser.CurrentFriends) },
    { "User", _logParser.Username },
    { "CaptureTime", DateTime.Now.ToString() }
};
PngMetadataManager.AddMetadataToPng(...);       // メタデータ追加
```

##### `WaitForFileAccess(string filePath)`
```csharp
// VRChatファイル書き込み完了待機
for (int i = 0; i < maxAttempts; i++)
{
    try
    {
        using (var stream = new FileStream(..., FileAccess.Read, FileShare.ReadWrite))
        {
            return; // アクセス成功
        }
    }
    catch (IOException)
    {
        await Task.Delay(500); // 500ms待機してリトライ
    }
}
```

### 6. VRChatLogParser.cs - ログ解析エンジン

#### 主要機能
- VRChatログファイルの自動発見
- ワールド情報の抽出
- フレンドリストの取得
- ユーザー名の特定

#### ログファイル探索
```csharp
private static readonly string[] LOG_PATH_CANDIDATES = new string[]
{
    "%USERPROFILE%\\AppData\\LocalLow\\VRChat\\VRChat",
    "%APPDATA%\\..\\LocalLow\\VRChat\\VRChat"
};
```

#### 正規表現パターン
```csharp
// ワールド参加パターン
private readonly Regex _worldEntryPattern = new Regex(@"Entering Room: (.*?)(?:\n|$)");

// ワールドIDパターン  
private readonly Regex _worldIdPattern = new Regex(@"wrld_[0-9a-fA-F\-]+");

// インスタンス参加パターン
private readonly Regex _instanceJoinPattern = new Regex(@"\[(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*?\] \[Behaviour\] Joining (.*?) (wrld_.*?:)");
```

#### ログ解析フロー
```csharp
1. ParseLatestLog()                             // 最新ログファイル検索
2. GetLatestLogFile()                           // 最新ファイル特定
3. AnalyzeLogContent(logContent)                // ログ内容解析
4. ExtractWorldInfo(line)                       // ワールド情報抽出
5. UpdateFriendsList(logContent)                // フレンドリスト更新
```

#### 自動更新機能
```csharp
// 2秒間隔での自動更新
_autoUpdateTimer = new System.Threading.Timer(
    callback: _ => Task.Run(() => AutoUpdateLog()),
    dueTime: AUTO_UPDATE_INTERVAL,
    period: AUTO_UPDATE_INTERVAL
);
```

### 7. PngMetadataManager.cs - PNG メタデータ管理

#### 機能概要
PNGファイルのtEXtチャンクを操作してメタデータの読み書きを行う

#### メタデータ形式
```csharp
// メインメタデータ（JSON形式）
"VSA_Metadata": "{\"WorldName\":\"...\",\"WorldID\":\"...\",\"Usernames\":\"...\"}"

// 個別メタデータ（冗長化）
"WorldName": "ワールド名"
"WorldID": "wrld_xxxxxxxxxx"
"User": "ユーザー名"
"Usernames": "friend1.friend2.friend3"
"CaptureTime": "2024-12-06 14:30:00"
```

#### 主要メソッド

##### `AddMetadataToPng(string sourceFilePath, string targetFilePath, Dictionary<string, string> metadata)`
```csharp
1. byte[] pngData = File.ReadAllBytes(sourceFilePath)      // PNGファイル読み込み
2. IsPngFile(pngData)                                      // PNGシグネチャ検証
3. CreateTextChunkData(key, value)                         // tEXtチャンク作成
4. InsertTextChunks(pngData, textChunks)                   // チャンク挿入
5. File.WriteAllBytes(targetFilePath, modifiedPngData)     // 変更済みファイル書き込み
```

##### `ReadMetadataFromPng(string filePath)`
```csharp
1. ファイル読み込み
2. PNGチャンク解析
3. tEXtチャンク抽出
4. Dictionary<string, string>として返却
```

##### `IsProcessedFile(string filePath)`
```csharp
// 処理済みマーカー（"VSACheck": "true"）の確認
var metadata = ReadMetadataFromPng(filePath);
return metadata.ContainsKey(PROCESSED_KEY) && metadata[PROCESSED_KEY] == "true";
```

### 8. ImageProcessor.cs - 画像処理オーケストレーター

#### 役割
FileWatcherServiceから呼び出され、画像ファイルの総合的な処理を管理

#### 依存関係
```csharp
private readonly FolderStructureManager _folderManager;     // フォルダ構造管理
private readonly FileNameGenerator _fileNameGenerator;      // ファイル名生成
private readonly MetadataProcessor _metadataProcessor;      // メタデータ処理
```

#### 処理フロー
```csharp
ProcessImage(sourceFilePath)
├─ IsProcessedFile(sourceFilePath)              // 処理済みチェック
├─ GetDestinationFolder(sourceFilePath)         // 出力先フォルダ決定
├─ GenerateFileName(sourceFilePath)             // ファイル名生成
└─ SaveWithMetadata(sourceFilePath, destPath)   // メタデータ付き保存
```

### 9. SystemTrayIcon.cs - システムトレイ管理

#### 機能
- トレイアイコンの表示・管理
- コンテキストメニューの提供
- メインウィンドウの表示・非表示制御

#### コンテキストメニュー
```csharp
- "メインアプリケーションを起動": LaunchMainApplication()
- "設定": ShowSettings()  
- "終了": Application.Exit()
```

## データフロー全体図

```
┌──────────────┐    ┌─────────────────┐    ┌──────────────────┐
│  VRChat      │    │  ファイル        │    │ VRChatログ       │
│ スクリーン    │───→│  監視システム    │←───│ 解析システム      │
│ ショット撮影  │    │                 │    │                  │
└──────────────┘    └─────────────────┘    └──────────────────┘
                             │                        │
                             ▼                        ▼
                    ┌─────────────────┐    ┌──────────────────┐
                    │  ファイル処理    │    │ メタデータ生成    │
                    │                 │◄───│                  │
                    └─────────────────┘    └──────────────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ PNGメタデータ    │
                    │ 追加・保存      │
                    └─────────────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ 整理された       │
                    │ 出力フォルダ     │
                    └─────────────────┘
```

## 設定ファイル構造 (appsettings.json)

```json
{
  "ScreenshotPath": "C:\\Users\\[User]\\Pictures\\VRChat",
  "OutputPath": "C:\\Users\\[User]\\Pictures\\VSA_Output",
  "FolderStructure": {
    "Enabled": true,
    "Type": "month"  // "month", "week", "day"
  },
  "FileRenaming": {
    "Enabled": true,
    "Format": "yyyy-MM-dd-HHmm-seq"
  },
  "Metadata": {
    "Enabled": true,
    "AddWorldName": true,
    "AddDateTime": true
  },
  "Compression": {
    "AutoCompress": true,
    "CompressionLevel": "medium",
    "OriginalFileHandling": "keep"
  },
  "Performance": {
    "CpuThreshold": 80,
    "MaxConcurrentProcessing": 10
  },
  "LauncherSettings": {
    "WatchingEnabled": true,
    "StartWithWindows": false
  }
}
```

## イベントドリブンアーキテクチャ

### 主要イベント
1. **FileSystemWatcher.Created**: ファイル作成検出
2. **Timer.Tick**: 定期ログ解析・UI更新
3. **UserControl Events**: UIインタラクション
4. **WorldChanged**: ワールド変更通知

### イベントチェーン
```
ファイル作成 → FileDetected → ProcessNewFile → メタデータ付与 → StatusChanged → UI更新
```

## エラーハンドリング

### 例外処理戦略
- **ファイルI/O**: IOException のリトライ機構
- **ログ解析**: パターンマッチ失敗時のデフォルト値
- **メタデータ**: 失敗時の通常コピーフォールバック
- **UI操作**: ユーザーフレンドリーなエラーメッセージ

### ログ出力
```csharp
// デバッグログ
System.Diagnostics.Debug.WriteLine($"[DEBUG] ProcessFile: ワールド名={worldName}");

// エラーログ
Console.WriteLine($"設定ファイルの読み込みエラー: {ex.Message}");
```

## パフォーマンス最適化

### 非同期処理
```csharp
// ファイル処理の非同期実行
Task.Run(() => ProcessNewFile(filePath));

// ログ解析の非同期実行  
Task.Run(() => _logParser.ParseLatestLog());
```

### リソース管理
```csharp
// IDisposableパターンの実装
public void Dispose()
{
    _autoUpdateTimer?.Dispose();
    _watchers.ForEach(w => w.Dispose());
}
```

## セキュリティ考慮事項

### ファイルアクセス
- ユーザーディレクトリのみアクセス
- ファイルパスのサニタイゼーション
- 読み取り専用での安全なファイルアクセス

### レジストリ操作
- HKEY_CURRENT_USER での制限されたアクセス
- 管理者権限不要な実装

## まとめ

VSA Launcherは以下の特徴を持つ堅牢なアーキテクチャで構成されています：

1. **モジュラー設計**: 各コンポーネントが明確な責任を持つ
2. **イベントドリブン**: リアルタイムなファイル監視と処理
3. **非同期処理**: UIブロックを防ぐバックグラウンド処理
4. **設定駆動**: 柔軟な設定によるカスタマイズ対応
5. **エラー回復**: 堅牢なエラーハンドリングとフォールバック機構

このアーキテクチャにより、VRChatユーザーのスクリーンショット管理を自動化し、メタデータ付きの整理されたアーカイブシステムを提供しています。
