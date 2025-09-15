# FileSystems Folder Documentation

## FileHelper.cs
**役割**: ファイル操作のヘルパークラス。安全なファイルストリームのオープン操作を提供。

### 関数詳細
- **OpenFileForReading(string filePath)** (public static FileStream)
  - 処理: 指定されたファイルを読み取りモードで開く。
  - 期待する値: 有効なファイルパス
  - 返す値: FileStreamオブジェクト
  - 例外: IOException

- **OpenFileForWriting(string filePath, bool overwrite = false)** (public static FileStream)
  - 処理: 指定されたファイルを書き込みモードで開く。必要に応じてディレクトリを作成。
  - 期待する値: ファイルパス、overwriteフラグ
  - 返す値: FileStreamオブジェクト
  - 例外: IOException

## FileNameGenerator.cs
**役割**: 画像ファイルの名前生成を担当。設定されたフォーマットに基づいて新しいファイル名を作成。

### 関数詳細
- **GenerateFileName(string sourceFilePath)** (public string)
  - 処理: 設定に基づいて新しいファイル名を生成。リネームが無効の場合は元の名前を返す。
  - 期待する値: ソースファイルのパス
  - 返す値: 生成されたファイル名（拡張子付き）

- **GenerateFormattedName(DateTime dateTime, string format)** (public string)
  - 処理: 日付とフォーマットに基づいて名前を生成。
  - 期待する値: DateTimeとフォーマット文字列
  - 返す値: フォーマットされた名前

## FileWatcherService.cs
**役割**: ファイルシステムの変更を監視し、イベントを発火。スクリーンショットの自動検知と処理を行う。

### 関数詳細
(主要な関数: コンストラクタ、StartWatching、StopWatching、ファイル検知イベントハンドラなど - 詳細はファイルを確認)

## FolderStructureManager.cs
**役割**: フォルダ構造の管理と作成。

(詳細な関数情報が必要)