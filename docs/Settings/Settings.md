# Settings Folder Documentation

## AppSettings.cs
**役割**: アプリケーション設定のモデルクラス。各種設定をプロパティとして保持。

(関数なし - プロパティのみのデータクラス)

## RenameFormatSettings.cs
**役割**: ファイル名変更フォーマットの設定クラス。リネームの有効/無効、日付パターン、フォーマットタイプなどを管理。

(関数なし - プロパティのみのデータクラス)

## SettingsManager.cs
**役割**: 設定の永続化と管理。JSONファイルからの読み込みと保存を担当。

### 関数詳細
- **LoadSettings()** (public static AppSettings)
  - 処理: appsettings.jsonから設定を読み込み、デシリアライズ。ファイルが存在しない場合はデフォルト設定を作成。カメラ設定を補完し、ファイルを更新。
  - 期待する値: なし
  - 返す値: AppSettingsオブジェクト
  - 例外: 読み込みエラー時はデフォルト設定を返す

- **EnsureCameraSettingsComplete(AppSettings settings)** (private static void)
  - 処理: CameraSettingsがnullの場合に新規作成し、VirtualLens2とIntegralの設定を補完。
  - 期待する値: AppSettingsオブジェクト
  - 返す値: void

- **SaveSettings(AppSettings settings)** (public static void)
  - 処理: 設定をJSON形式でシリアライズし、appsettings.jsonに保存。
  - 期待する値: AppSettingsオブジェクト
  - 返す値: void
  - 例外: 保存エラー時はログ出力