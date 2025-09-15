# OSCServer Folder Documentation

## DelayedOscServerManager.cs
**役割**: 遅延OSCサーバーの管理。

(詳細な関数情報が必要)

## IntegralOscServer.cs
**役割**: 積分OSCサーバーの実装。

(詳細な関数情報が必要)

## OscDataStore.cs
**役割**: OSCデータの保存。

(詳細な関数情報が必要)

## OscManager.cs
**役割**: OSC通信のメイン管理。VRChatへのパラメータ送信を一元管理。

### 関数詳細
- **OscManager(CancellationToken cancellationToken, OscDataStore dataStore, OSCQueryService oscQueryService)** (コンストラクタ)
  - 処理: OscManagerを初期化し、OSCQueryエンドポイントを登録。
  - 期待する値: CancellationToken, OscDataStore, OSCQueryService
  - 返す値: なし

- **Start()** (public void)
  - 処理: VRChatへのOSC送信機能を開始。OscSenderを初期化して接続。
  - 期待する値: なし
  - 返す値: void

- **SendParameter(CameraType cameraType, string parameterName, object value)** (public void)
  - 処理: 指定されたカメラタイプのパラメータをOSCメッセージとして送信。
  - 期待する値: CameraType, パラメータ名, 値
  - 返す値: void

- **SendCurrentActiveParameters()** (public void)
  - 処理: 現在アクティブなカメラのパラメータをすべて送信。
  - 期待する値: なし
  - 返す値: void

- **SendVirtualLens2Parameters()** (public void)
  - 処理: VirtualLens2の全パラメータを送信。
  - 期待する値: なし
  - 返す値: void

- **SendIntegralParameters()** (public void)
  - 処理: Integralの全パラメータを送信。
  - 期待する値: なし
  - 返す値: void

- **GetFullParameterName(CameraType cameraType, string parameterName)** (private string)
  - 処理: カメラタイプとパラメータ名から完全なパラメータ名を生成。
  - 期待する値: CameraType, パラメータ名
  - 返す値: 完全なパラメータ名文字列

- **RegisterOscQueryEndpoints()** (private void)
  - 処理: OSCQueryサービスにエンドポイントを登録。
  - 期待する値: なし
  - 返す値: void

- **StartParameterMonitoring()** (public void)
  - 処理: データストアの変更イベントを監視開始。
  - 期待する値: なし
  - 返す値: void

- **StopParameterMonitoring()** (public void)
  - 処理: データストアの変更イベント監視を停止。
  - 期待する値: なし
  - 返す値: void

- **OnParameterChanged(object? sender, ParameterChangedEventArgs e)** (private void)
  - 処理: パラメータ変更イベントを処理し、必要に応じてパラメータを送信。
  - 期待する値: sender, ParameterChangedEventArgs
  - 返す値: void

- **Dispose()** (public void)
  - 処理: リソースを解放。
  - 期待する値: なし
  - 返す値: void

## OSCParameterSender.cs
**役割**: OSCパラメータの送信。

(詳細な関数情報が必要)

## VirtualLens2OscServer.cs
**役割**: 仮想レンズOSCサーバー。

(詳細な関数情報が必要)

## VRChatListener.cs
**役割**: VRChatからのOSCメッセージ受信。

(詳細な関数情報が必要)