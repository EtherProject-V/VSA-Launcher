# VRC-Game Folder Documentation

## VRChatInitializationManager.cs
**役割**: VRChatの起動監視とカメラ初期化を管理。

### 関数詳細
- **VRChatInitializationManager(VRChatLogParser logParser, OSCParameterSender oscParameterSender, Action<string, string> updateStatusAction)** (コンストラクタ)
  - 処理: 初期化マネージャーを設定。
  - 期待する値: VRChatLogParser, OSCParameterSender, Actionデリゲート
  - 返す値: なし

- **Start()** (public void)
  - 処理: VRChatの監視を開始。
  - 期待する値: なし
  - 返す値: void

(他の関数も多数存在 - タイマーコールバック、プロセスチェックなど)

## VRChatLogParser.cs
**役割**: VRChatログの解析。ログファイルからワールド情報、フレンドリスト、ユーザー情報を抽出。

### 関数詳細
- **VRChatLogParser(bool enableAutoUpdate = true)** (コンストラクタ)
  - 処理: VRChatログフォルダを検索し、初回ログ解析を実行。自動更新タイマーをセットアップ。
  - 期待する値: enableAutoUpdateフラグ（デフォルトtrue）
  - 返す値: なし

- **ParseLatestLog()** (public bool)
  - 処理: 最新のVRChatログファイルを読み込み、ワールド情報とフレンドリストを解析。
  - 期待する値: なし
  - 返す値: bool - 解析成功時true

- **GetFriendsString(string separator = ".")** (public string)
  - 処理: 現在のフレンドリストを指定された区切り文字で結合した文字列を返す。
  - 期待する値: separator（デフォルト"."）
  - 返す値: フレンドリストの文字列、または"ボッチ(だれもいません)"

- **GenerateMetadata()** (public Dictionary<string, string>)
  - 処理: 現在のワールド、フレンド、ユーザー情報を含むメタデータ辞書を生成。
  - 期待する値: なし
  - 返す値: メタデータのキーと値の辞書

- **Dispose()** (public void)
  - 処理: リソースを解放（自動更新タイマーを停止）。
  - 期待する値: なし
  - 返す値: void

- **ResetWorldData()** (public void)
  - 処理: ワールドデータをリセット。
  - 期待する値: なし
  - 返す値: void

- **InitializeFromLatestLog()** (public void)
  - 処理: 最新のログから初期化。
  - 期待する値: なし
  - 返す値: void

## VRChatUserDetector.cs
**役割**: VRChatログからユーザー名を検出。ローカルユーザーとリモートユーザーの識別を行う。

### 関数詳細
- **DetectLocalUser(string logContent)** (public string)
  - 処理: ログコンテンツからローカルユーザー（自分自身）の名前を検出。正規表現で"Initialized PlayerAPI"や"User Authenticated"を検索。
  - 期待する値: ログコンテンツの文字列
  - 返す値: 検出されたユーザー名、または前回検出された名前

- **DetectRemoteUsers(string logContent, DateTime instanceStartTime)** (public List<string>)
  - 処理: ログコンテンツからリモートユーザー（他プレイヤー）のリストを検出。ローカルユーザーを除外。
  - 期待する値: ログコンテンツ、インスタンス開始時間
  - 返す値: リモートユーザーの名前リスト

- **BuildUserInfo(string worldName, string worldId, List<string> remoteUsers)** (public VRChatUserInfo)
  - 処理: ワールド情報とユーザー情報を含むVRChatUserInfoオブジェクトを構築。
  - 期待する値: ワールド名、ワールドID、リモートユーザーリスト
  - 返す値: VRChatUserInfoオブジェクト

(VRChatUserInfo.ToDelimitedString() メソッドも存在 - 情報を区切り文字で結合した文字列を返す)