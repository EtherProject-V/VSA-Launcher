# VRC SnapArchive Kai - Launcher

VRChatスクリーンショットの自動処理・管理を行うWindows Formsアプリケーション

## 概要

VSA Launcherは、VRChatで撮影したスクリーンショットを自動的に検知し、メタデータの付与、ファイル名の変更、フォルダ構造の整理を行うツールです。OSC（Open Sound Control）を通じてVRChatのカメラパラメータを制御する機能も搭載しています。

## 機能一覧

- **スクリーンショット自動監視**: VRChatのスクリーンショットフォルダを監視し、新しいファイルを自動検出
- **メタデータ自動付与**: ワールド名、撮影者、撮影時刻、インスタンス内ユーザー情報をPNG画像に埋め込み
- **ファイル自動リネーム**: カスタマイズ可能な形式でファイル名を自動変更
- **フォルダ構造自動整理**: 月別/週別/日別でフォルダを自動作成・整理
- **OSC通信**: VRChatのカメラパラメータ（VirtualLens2/Integral）を制御
- **WebSocketサーバー**: 外部アプリケーションとの連携用API
- **VDI起動管理**: VDIソフトウェアの自動起動
- **タスクトレイ常駐**: バックグラウンドで動作
- **Windowsスタートアップ対応**: PC起動時に自動起動

## システム要件

- **OS**: Windows 10/11
- **ランタイム**: .NET 8.0 Runtime
- **オプション**: VRChat（OSC機能を使用する場合）

## インストール

1. リリースページから最新版の`VSA-Launcher.zip`をダウンロード
2. 任意のフォルダに解凍
3. `VSA-launcher.exe`を実行

初回起動時に設定ファイルが自動生成されます。

## 使い方

### 基本的な使用フロー

1. アプリケーションを起動
2. スクリーンショットフォルダ（監視対象）を設定
3. 出力フォルダを設定
4. 「監視開始」をクリック
5. VRChatでスクリーンショットを撮影すると自動処理が開始

### カメラモード選択

3つのカメラモードに対応しています：

| モード | 説明 |
|--------|------|
| Normal | 通常カメラ（デフォルト） |
| Integral | Integralカメラ用（Aperture、FocalLength、Exposure、ShutterSpeed、BokehShape） |
| VirtualLens2 | VirtualLens2用（Aperture、FocalLength、Exposure） |

### OSC機能

OSCを有効にすると、VRChatとの間でカメラパラメータを送受信できます。

- **受信ポート**: 9001（VRChat → VSA Launcher）
- **送信ポート**: 9000（VSA Launcher → VRChat）

VRChatの設定でOSCを有効にする必要があります。

## 設定ファイル

### 保存場所

```
%APPDATA%\VSA\launcher\appsettings.json
```

具体的なパス: `C:\Users\{ユーザー名}\AppData\Roaming\VSA\launcher\appsettings.json`

### 主要設定項目

| カテゴリ | 設定項目 | 説明 | デフォルト値 |
|----------|----------|------|--------------|
| **パス設定** | ScreenshotPath | 監視対象フォルダ | 空文字 |
| | OutputPath | 出力先フォルダ | 空文字 |
| **フォルダ構造** | FolderStructure.Enabled | フォルダ整理を有効化 | true |
| | FolderStructure.Type | 整理タイプ（month/week/day） | "month" |
| **ファイル名** | FileRenaming.Enabled | リネームを有効化 | true |
| | FileRenaming.Format | ファイル名フォーマット | "yyyy-MM-dd-HHmm-seq" |
| **メタデータ** | Metadata.Enabled | メタデータ付与を有効化 | true |
| | Metadata.AddWorldName | ワールド名を追加 | true |
| | Metadata.AddDateTime | 撮影日時を追加 | true |
| **OSC** | LauncherSettings.OSCSettings.Enabled | OSCを有効化 | true |
| | LauncherSettings.OSCSettings.ReceiverPort | 受信ポート | 9001 |
| | LauncherSettings.OSCSettings.SenderPort | 送信ポート | 9000 |
| **カメラ** | CameraSettings.Enabled | カメラ設定を有効化 | false |
| **WebSocket** | WebSocket.Enabled | WebSocketを有効化 | true |
| | WebSocket.StartPort | 開始ポート | 28766 |

### 設定ファイル例

```json
{
  "ScreenshotPath": "C:\\Users\\Username\\Pictures\\VRChat",
  "OutputPath": "C:\\Users\\Username\\Pictures\\VRChat\\Processed",
  "FolderStructure": {
    "Enabled": true,
    "Type": "month"
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
  "LauncherSettings": {
    "WatchingEnabled": true,
    "StartWithWindows": false,
    "OSCSettings": {
      "Enabled": true,
      "ReceiverPort": 9001,
      "SenderPort": 9000
    }
  },
  "CameraSettings": {
    "Enabled": false,
    "VirtualLens2": {
      "Aperture": 0,
      "FocalLength": 44,
      "Exposure": 50
    },
    "Integral": {
      "Aperture": 50,
      "FocalLength": 50,
      "Exposure": 50,
      "ShutterSpeed": 50,
      "BokehShape": 50
    }
  },
  "WebSocket": {
    "Enabled": true,
    "StartPort": 28766,
    "MaxPortAttempts": 10
  }
}
```

## プロジェクト構造

```
VSA-Launcher/
├── Program.cs                 # エントリーポイント（Mutex重複起動防止）
├── Form1.cs                   # メインUI (Windows Forms)
├── Form1.Designer.cs          # UIデザイナーファイル
├── VSA-launcher.csproj        # プロジェクトファイル
├── VSA-launcher.sln           # ソリューションファイル
├── StartupManager.cs          # Windowsスタートアップ管理
├── VRChatLogParser.cs         # VRChatログ解析
├── VdiInstallManager.cs       # VDI導入管理
├── VdiLauncher.cs             # VDI起動管理
├── example.json               # メタデータフォーマット例
│
├── OSCServer/                 # OSC通信モジュール
│   ├── OscManager.cs          # OSCマネージャー
│   ├── OscDataStore.cs        # パラメータデータ保存
│   ├── OSCParameterSender.cs  # パラメータ送信
│   ├── VRChatListener.cs      # VRChatからのメッセージ受信
│   ├── IntegralOscServer.cs   # Integralカメラ用
│   ├── VirtualLens2OscServer.cs # VirtualLens2カメラ用
│   └── DelayedOscServerManager.cs # 遅延起動管理
│
├── FileSystems/               # ファイルシステム管理
│   ├── FileWatcherService.cs  # スクリーンショット監視
│   ├── FolderStructureManager.cs # フォルダ整理
│   ├── FileNameGenerator.cs   # ファイル名生成
│   └── FileHelper.cs          # ファイル操作ユーティリティ
│
├── ImageControllers/          # 画像処理
│   ├── ImageProcessor.cs      # 画像処理メイン
│   ├── MetadataProcessor.cs   # メタデータ処理
│   ├── PngMetadataManager.cs  # PNGメタデータ管理
│   └── Crc32.cs               # CRC32チェックサム
│
├── VRC-Game/                  # VRChat連携
│   ├── VRChatInitializationManager.cs # VRChat初期化管理
│   └── VRChatUserDetector.cs  # ユーザー情報検出
│
├── Settings/                  # 設定管理
│   ├── AppSettings.cs         # 設定定義
│   ├── SettingsManager.cs     # 設定読み書き
│   └── RenameFormatSettings.cs # ファイル名フォーマット設定
│
├── WebSocket/                 # WebSocket通信
│   ├── WebSocketServerManager.cs
│   └── WebSocketMessage.cs
│
├── devModule/                 # 開発者用モジュール
│   └── DevMetadataMonitor.cs  # メタデータ検査ツール
│
└── test/                      # テストスクリプト
    ├── imagemaker.js
    └── package.json
```

## ビルド方法

### 必要なツール

- Visual Studio 2022以降
- .NET 8.0 SDK

### ビルドコマンド

```bash
# 依存関係の復元
dotnet restore

# デバッグビルド
dotnet build

# リリースビルド
dotnet build --configuration Release

# 発行（自己完結型でない）
dotnet publish --configuration Release --runtime win-x86 --self-contained false

# 発行（自己完結型）
dotnet publish --configuration Release --runtime win-x86 --self-contained true
```

### 出力先

- デバッグ: `bin/Debug/net8.0-windows/`
- リリース: `bin/Release/net8.0-windows/`

## 依存ライブラリ

| ライブラリ | バージョン | 用途 |
|-----------|-----------|------|
| Fleck | 1.2.0 | WebSocketサーバー |
| Newtonsoft.Json | 13.0.3 | JSON処理 |
| Rug.Osc | 1.2.5 | OSC通信 |
| SixLabors.ImageSharp | 3.1.11 | 画像処理・メタデータ操作 |
| System.Diagnostics.PerformanceCounter | 9.0.9 | パフォーマンス監視 |
| System.Drawing.Common | 9.0.9 | GDI+描画 |
| VRChat.OSCQuery | 0.0.7 | VRChat OSCQuery対応 |

## 技術仕様

### OSC通信

| ポート | 方向 | 説明 |
|--------|------|------|
| 9000 | 送信 | VSA Launcher → VRChat |
| 9001 | 受信 | VRChat → VSA Launcher |

### WebSocket

- **ポート範囲**: 28766〜28775（空きポートを自動選択）
- **プロトコル**: ws://localhost:{port}

### メタデータフォーマット

PNG画像のtEXtチャンクに以下の情報を埋め込みます：

```json
{
  "WorldName": "ワールド名",
  "Capture-User": "撮影者名",
  "CaptureTime": "2024-07-26T10:30:00+09:00",
  "instans-Usernames": "Friend1, Friend2, Friend3",
  "VirtualLens2_Aperture": "0.0",
  "VirtualLens2_FocalLength": "0.0",
  "VirtualLens2_Exposure": "0.0",
  "Integral_Aperture": "0.0",
  "Integral_FocalLength": "0.0",
  "Integral_Exposure": "0.0",
  "Integral_ShutterSpeed": "0.0",
  "Integral_BokehShape": "0",
  "IsIntegral": "false",
  "IsVirtualLens2": "false",
  "NormalCamera": "true"
}
```

### アプリケーション仕様

- **重複起動防止**: Mutex使用（"VrcSnapArchiveKai_Launcher"）
- **タスクトレイ**: 最小化時にタスクトレイに常駐
- **ログ**: デバッグビルド時にコンソール出力

## トラブルシューティング

### OSCが動作しない

1. VRChatのOSC設定が有効になっているか確認
2. ファイアウォールでポート9000/9001が許可されているか確認
3. 他のアプリケーションが同じポートを使用していないか確認

### スクリーンショットが検出されない

1. 監視対象フォルダのパスが正しいか確認
2. VRChatのスクリーンショット保存先を確認
3. アプリケーションを管理者権限で実行

### 設定が保存されない

1. `%APPDATA%\VSA\launcher\`フォルダへの書き込み権限を確認
2. アプリケーションを終了してから設定ファイルを手動編集

## ライセンス

Apache License 2.0

```
Copyright 2024

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

## 謝辞

このプロジェクトは以下のオープンソースライブラリを使用しています：

- [Fleck](https://github.com/statianzo/Fleck) - WebSocketサーバー
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON処理
- [Rug.Osc](https://bitbucket.org/rugcode/rug.osc) - OSC通信
- [SixLabors.ImageSharp](https://sixlabors.com/products/imagesharp/) - 画像処理
- [VRChat.OSCQuery](https://github.com/vrchat-community/vrc-oscquery-lib) - VRChat OSCQuery
