using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace VSA_launcher
{
    /// <summary>
    /// VRChatのログファイルからワールド情報やフレンドリストを解析するクラス
    /// </summary>
    public class VRChatLogParser
    {
        // ユーザー状態を再現するクラス
        public class UserState
        {
            public string Username { get; set; } = string.Empty;
            public DateTime JoinTime { get; set; }
            public DateTime? LeaveTime { get; set; }
            public bool IsCurrentlyInInstance => !LeaveTime.HasValue;
            
            public override string ToString()
            {
                return $"{Username} (参加:{JoinTime:HH:mm:ss}{(LeaveTime.HasValue ? $", 退出:{LeaveTime:HH:mm:ss}" : "")})";
            }
        }
        
        // ログファイルのディレクトリパス候補
        private static readonly string[] LOG_PATH_CANDIDATES = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "VRChat", "VRChat"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "..\\LocalLow\\VRChat\\VRChat")
        };
        
        // ログファイルのディレクトリパス（実際に見つかったパス）
        private readonly string? _logFolderPath;
        
        // パターンマッチング用の正規表現（コンパイル済み）
        private readonly Regex _worldEntryPattern = new Regex(@"Entering Room: (.*?)(?:\n|$)", RegexOptions.Compiled);
        private readonly Regex _worldIdPattern = new Regex(@"wrld_[0-9a-fA-F\-]+", RegexOptions.Compiled);        
        private readonly Regex _localUserPattern = new Regex(@".*\[Behaviour\] Initialized PlayerAPI ""([^""]+)"" is local", RegexOptions.Compiled);
        private readonly Regex _authUserPattern = new Regex(@".*User Authenticated: ([^\(]+) \(usr_[a-z0-9\-]+\)", RegexOptions.Compiled);

        // タイムスタンプ付きログエントリの正規表現
        private readonly Regex _timeStampLogPattern = new Regex(@"(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*", RegexOptions.Compiled);
        
        // インスタンス境界検出用の正規表現を追加
        private readonly Regex _instanceJoinPattern = new Regex(@"\[(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*?\] \[Behaviour\] Joining (.*?) (wrld_.*?:)", RegexOptions.Compiled);
        private readonly Regex _onJoinedRoomPattern = new Regex(@"\[(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*?\] \[Behaviour\] OnJoinedRoom has been called", RegexOptions.Compiled);
        private readonly Regex _roomJoinCompletePattern = new Regex(@"\[(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*?\] \[Behaviour\] Room Join Completed for (.*)", RegexOptions.Compiled);
        
        // ワールド参加ログに対応する正規表現（提供された形式に基づく）
        private readonly Regex _worldEntryBehaviourPattern = new Regex(@"(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*\[Behaviour\] Entering Room: (.*?)$", RegexOptions.Compiled | RegexOptions.Multiline);
        
        // プレイヤー参加ログに対応する正規表現
        private readonly Regex _playerJoinPattern = new Regex(@"(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*\[Behaviour\] OnPlayerJoined (.*?)( \(usr_.*?\))?$", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex _playerJoinCompletePattern = new Regex(@"(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*\[Behaviour\] OnPlayerJoinComplete (.*?)$", RegexOptions.Compiled | RegexOptions.Multiline);
        
        // プレイヤー退出ログに対応する正規表現
        private readonly Regex _playerLeftPattern = new Regex(@"(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*\[Behaviour\] OnPlayerLeft (.*?)( \(usr_.*?\))?$", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex _playerRemovedPattern = new Regex(@"(\d{4}.\d{2}.\d{2} \d{2}:\d{2}:\d{2}).*Removed player (.*?)$", RegexOptions.Compiled | RegexOptions.Multiline);
        
        // 解析結果の保持
        public string CurrentWorldName { get; private set; } = "Unknown World";
        public string CurrentWorldId { get; private set; } = "";
        // フレンドリストを保持するリストを追加
        public List<string> CurrentFriends { get; private set; } = new List<string>();
        // 撮影者（ユーザー名）を保持するプロパティを追加
        public string Username { get; private set; } = "Unknown User";
        public DateTime LastLogParseTime { get; private set; }
        public bool IsValidLogFound { get; private set; }
        
        // 自動更新間隔（ミリ秒）
        private const int AUTO_UPDATE_INTERVAL = 1000; // 1秒（より頻繁に更新）
        private System.Threading.Timer? _autoUpdateTimer;
        
        // イベント - ワールド変更時に発火
        public event EventHandler<WorldChangedEventArgs>? WorldChanged;
        
        // ワールド移動とプレイヤー参加時間を追跡するための変数
        private Dictionary<string, DateTime> _playerJoinTimestamps = new Dictionary<string, DateTime>();
        private DateTime _lastWorldChangeTime = DateTime.MinValue;
        
        // プレイヤー管理用
        private HashSet<string> _activePlayers = new HashSet<string>();
        private DateTime _lastRoomJoinTime = DateTime.MinValue;
        
        // インスタンス内のユーザー状態を管理する辞書（キー: ユーザー名）
        private Dictionary<string, UserState> _instanceUsers = new Dictionary<string, UserState>();
        private DateTime _currentInstanceStartTime = DateTime.MinValue;
        
        // ログ解析の制限サイズを拡大（MB単位）
        private const int MAX_READ_SIZE = 5 * 1024 * 1024; // 5MB

        private VRChatUserDetector _userDetector = new VRChatUserDetector();
        
        /// <summary>
        /// コンストラクタ - VRChatログフォルダを検索して初期化
        /// </summary>
        public VRChatLogParser(bool enableAutoUpdate = true)
        {
            // 有効なログパスを検索
            foreach (var path in LOG_PATH_CANDIDATES)
            {
                if (Directory.Exists(path))
                {
                    _logFolderPath = path;
                    IsValidLogFound = true;
                    break;
                }
            }
            
            if (IsValidLogFound)
            {
                // 初回解析
                ParseLatestLog();
                
                // 自動更新タイマーのセットアップ（オプション） 
                if (enableAutoUpdate)
                {
                    _autoUpdateTimer = new System.Threading.Timer(
                        callback: _ => Task.Run(() => AutoUpdateLog()),
                        state: null,
                        dueTime: AUTO_UPDATE_INTERVAL, 
                        period: AUTO_UPDATE_INTERVAL
                    );
                }
            }
            else
            {
                LogError("VRChatログフォルダが見つかりませんでした。メタデータにデフォルト値を使用します。");
            }
        }
        
        /// <summary>
        /// 自動更新処理（新しいワールド情報を定期的にチェック）
        /// </summary>
        private void AutoUpdateLog()
        {
            try
            {
                string oldWorldName = CurrentWorldName;
                string oldWorldId = CurrentWorldId;
                
                // 最新のインスタンス情報を再構築
                if (ReconstructCurrentInstanceState())
                {
                    // ワールド情報が変更された場合
                    if (oldWorldName != CurrentWorldName || oldWorldId != CurrentWorldId)
                    {
                        OnWorldChanged(new WorldChangedEventArgs(CurrentWorldName, CurrentWorldId));
                    }
                }
            }
            catch (Exception ex)
            {
                // タイマーからの呼び出しでの例外は無視するが、ログには残す
                LogError($"自動ログ更新中のエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 最新のログファイルを解析
        /// </summary>
        /// <returns>解析に成功したかどうか</returns>
        public bool ParseLatestLog()
        {
            if (string.IsNullOrEmpty(_logFolderPath) || !Directory.Exists(_logFolderPath))
            {
                LogError("VRChatログフォルダが見つかりません");
                return false;
            }
            
            try
            {
                // 最新のログファイルを取得（複数の命名パターンに対応）
                string[] searchPatterns = { "output_log_*.txt", "VRChat-*.log" };
                var logFiles = new List<string>();
                
                foreach (var pattern in searchPatterns)
                {
                    if (Directory.Exists(_logFolderPath))
                    {
                        logFiles.AddRange(Directory.GetFiles(_logFolderPath, pattern));
                    }
                }
                
                if (logFiles.Count == 0)
                {
                    LogError("VRChatログファイルが見つかりません");
                    return false;
                }
                
                // 最新のログファイルを選択（作成日時の降順）
                string? latestLogPath = logFiles
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
                
                if (string.IsNullOrEmpty(latestLogPath))
                {
                    LogError("有効なログファイルが見つかりません");
                    return false;
                }
                
                // ログファイルを読み込む（共有モードで開く）
                string logContent;
                using (var fileStream = new FileStream(latestLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream))
                {
                    // ログファイルが大きい場合は最後の部分のみ読み込む
                    if (fileStream.Length > MAX_READ_SIZE)
                    {
                        fileStream.Seek(-MAX_READ_SIZE, SeekOrigin.End);
                        // 行の途中からの読み込みを避けるため、次の行の先頭まで読み飛ばす
                        reader.ReadLine();
                    }
                    
                    logContent = reader.ReadToEnd();
                }
                
                // 旧来の解析方法からインスタンス状態再構築メソッドを使用する方式に変更
                return ReconstructCurrentInstanceState();
            }
            catch (Exception ex)
            {
                LogError($"VRChatログ解析エラー: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 現在のインスタンス状態を正確に再構築する
        /// </summary>
        /// <returns>再構築に成功したかどうか</returns>
        public bool ReconstructCurrentInstanceState()
        {
            try
            {
                if (string.IsNullOrEmpty(_logFolderPath) || !Directory.Exists(_logFolderPath))
                {
                    LogError("VRChatログフォルダが見つかりません");
                    return false;
                }
                
                // 最新のログファイルを取得
                string? latestLogPath = GetLatestLogFilePath();
                if (string.IsNullOrEmpty(latestLogPath)) return false;

                // ログファイルを読み込む（より大きなサイズを読み込み）
                string logContent = ReadLatestLogContent(latestLogPath);
                
                // 0. ローカルユーザー（撮影者）を検出
                DetectLocalUser(logContent);
                
                // 1. 最新のワールド変更を検出
                DateTime worldJoinTime = DetectLatestWorldJoin(logContent);
                if (worldJoinTime == DateTime.MinValue) return false;

                // 2. そのワールド参加以降のユーザー動向を追跡
                ReconstructUserMovements(logContent, worldJoinTime);

                // 3. 現在のユーザーリストを更新
                UpdateCurrentFriendsList();

                Console.WriteLine($"[INFO] インスタンス状態再構築完了:");
                Console.WriteLine($"  ワールド: {CurrentWorldName}");
                Console.WriteLine($"  参加時刻: {worldJoinTime:yyyy.MM.dd HH:mm:ss}");
                Console.WriteLine($"  撮影者: {Username}");
                Console.WriteLine($"  現在のユーザー数: {CurrentFriends.Count}");
                Console.WriteLine($"  ユーザー: {string.Join(", ", CurrentFriends)}");

                LastLogParseTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogError($"インスタンス状態再構築エラー: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// ローカルユーザー（撮影者）を検出
        /// </summary>
        private void DetectLocalUser(string logContent)
        {
            // VRChatUserDetectorを使用してローカルユーザーを検出
            string detectedUser = _userDetector.DetectLocalUser(logContent);
            
            if (!string.IsNullOrEmpty(detectedUser) && detectedUser != "Unknown User")
            {
                Username = detectedUser;
                Console.WriteLine($"[DEBUG] ローカルユーザー検出: {Username}");
            }
        }
        
        /// <summary>
        /// 最新のログファイルパスを取得
        /// </summary>
        private string? GetLatestLogFilePath()
        {
            try
            {
                // 最新のログファイルを取得
                string[] searchPatterns = { "output_log_*.txt" };
                var logFiles = new List<string>();
                
                foreach (var pattern in searchPatterns)
                {
                    if (Directory.Exists(_logFolderPath))
                    {
                        logFiles.AddRange(Directory.GetFiles(_logFolderPath, pattern));
                    }
                }
                
                if (logFiles.Count == 0)
                {
                    LogError("VRChatログファイルが見つかりません");
                    return null;
                }
                
                // 最新のログファイルを選択（作成日時の降順）
                string? latestLogPath = logFiles
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
                
                if (string.IsNullOrEmpty(latestLogPath))
                {
                    LogError("有効なログファイルが見つかりません");
                    return null;
                }
                
                return latestLogPath;
            }
            catch (Exception ex)
            {
                LogError($"ログファイル検索エラー: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// ログファイルの内容を読み込む（より大きなサイズを読み込む）
        /// </summary>
        private string ReadLatestLogContent(string logPath)
        {
            using (var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream))
            {
                // ファイルサイズチェック
                if (fileStream.Length > MAX_READ_SIZE)
                {
                    // 最後の部分から読み取り（ただし、完全な行から開始）
                    fileStream.Seek(-MAX_READ_SIZE, SeekOrigin.End);
                    
                    // 行の途中から開始することを避ける
                    reader.ReadLine(); // 最初の不完全な行を破棄
                }
                
                return reader.ReadToEnd();
            }
        }
        
        /// <summary>
        /// 最新のワールド参加時刻を検出
        /// </summary>
        private DateTime DetectLatestWorldJoin(string logContent)
        {
            DateTime latestJoinTime = DateTime.MinValue;
            string worldName = string.Empty;
            
            // 1. Entering Room パターンで検索（提供されたログ形式に合わせる）
            var worldEntryMatches = _worldEntryBehaviourPattern.Matches(logContent);
            if (worldEntryMatches.Count > 0)
            {
                // 最新のワールドエントリを取得
                var lastEntry = worldEntryMatches[worldEntryMatches.Count - 1];
                if (lastEntry.Groups.Count >= 3)
                {
                    string timeString = lastEntry.Groups[1].Value;
                    worldName = lastEntry.Groups[2].Value.Trim();
                    
                    if (DateTime.TryParse(timeString, out DateTime entryTime))
                    {
                        latestJoinTime = entryTime;
                        
                        // ワールド情報を更新
                        CurrentWorldName = worldName;
                        
                        // ワールドIDの抽出は別途実施（今回はワールド名のみで対応）
                        var worldIdMatch = _worldIdPattern.Match(worldName);
                        if (worldIdMatch.Success)
                        {
                            CurrentWorldId = worldIdMatch.Value;
                        }
                        else
                        {
                            CurrentWorldId = string.Empty;
                        }
                        
                        Console.WriteLine($"[DEBUG] 最新ワールド参加検出: {CurrentWorldName} 時刻: {latestJoinTime:yyyy.MM.dd HH:mm:ss}");
                    }
                }
            }
            
            // joinが検出された場合、状態をリセット
            if (latestJoinTime != DateTime.MinValue)
            {
                _currentInstanceStartTime = latestJoinTime;
                _lastWorldChangeTime = latestJoinTime;
                
                // 新しいインスタンスなので、ユーザー状態をリセット
                _instanceUsers.Clear();
                _activePlayers.Clear();
                
                // ワールド変更イベントを発火
                OnWorldChanged(new WorldChangedEventArgs(CurrentWorldName, CurrentWorldId));
            }
            
            return latestJoinTime;
        }
        
        /// <summary>
        /// ユーザーの動向を時系列で再構築
        /// </summary>
        private void ReconstructUserMovements(string logContent, DateTime instanceStartTime)
        {
            // インスタンス開始以降のログのみを対象
            string relevantLog = ExtractLogAfterTime(logContent, instanceStartTime);
            
            // 全イベントを時系列で収集
            var allEvents = new List<(DateTime Time, string Type, string Username)>();
            
            // プレイヤー参加イベントを収集（OnPlayerJoined）
            CollectPlayerJoinEvents(relevantLog, allEvents);
            
            // プレイヤー退出イベントを収集（OnPlayerLeft）
            CollectPlayerLeftEvents(relevantLog, allEvents);
            
            // 時系列順にソート
            allEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
            
            // イベントを順次処理して現在の状態を再構築
            foreach (var (time, type, username) in allEvents)
            {
                if (!IsLocalUser(username)) // 自分自身は除外
                {
                    ProcessUserEvent(time, type, username);
                }
            }
            
            Console.WriteLine($"[DEBUG] 処理したイベント数: {allEvents.Count}");
            if (allEvents.Count > 0)
            {
                Console.WriteLine($"  最初のイベント: {allEvents.First().Time:HH:mm:ss} {allEvents.First().Type} {allEvents.First().Username}");
                Console.WriteLine($"  最後のイベント: {allEvents.Last().Time:HH:mm:ss} {allEvents.Last().Type} {allEvents.Last().Username}");
            }
        }
        
        /// <summary>
        /// プレイヤー参加イベントを収集
        /// </summary>
        private void CollectPlayerJoinEvents(string logContent, List<(DateTime, string, string)> events)
        {
            // OnPlayerJoined イベントを抽出
            var joinMatches = _playerJoinPattern.Matches(logContent);
            foreach (Match match in joinMatches)
            {
                if (match.Groups.Count >= 3 && DateTime.TryParse(match.Groups[1].Value, out DateTime joinTime))
                {
                    string username = CleanUsername(match.Groups[2].Value);
                    if (!string.IsNullOrEmpty(username))
                    {
                        events.Add((joinTime, "JOIN", username));
                    }
                }
            }
            
            // OnPlayerJoinComplete イベントも追加（参考情報として）
            var joinCompleteMatches = _playerJoinCompletePattern.Matches(logContent);
            foreach (Match match in joinCompleteMatches)
            {
                if (match.Groups.Count >= 3 && DateTime.TryParse(match.Groups[1].Value, out DateTime joinTime))
                {
                    string username = CleanUsername(match.Groups[2].Value);
                    if (!string.IsNullOrEmpty(username))
                    {
                        // 既に同じユーザーのJOINがあるか確認
                        bool alreadyExists = events.Any(e => 
                            e.Item2 == "JOIN" && 
                            string.Equals(e.Item3, username, StringComparison.OrdinalIgnoreCase));
                        
                        if (!alreadyExists)
                        {
                            events.Add((joinTime, "JOIN", username));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// プレイヤー退出イベントを収集
        /// </summary>
        private void CollectPlayerLeftEvents(string logContent, List<(DateTime, string, string)> events)
        {
            // OnPlayerLeft イベントを抽出
            var leftMatches = _playerLeftPattern.Matches(logContent);
            foreach (Match match in leftMatches)
            {
                if (match.Groups.Count >= 3 && DateTime.TryParse(match.Groups[1].Value, out DateTime leftTime))
                {
                    string username = CleanUsername(match.Groups[2].Value);
                    if (!string.IsNullOrEmpty(username))
                    {
                        events.Add((leftTime, "LEAVE", username));
                    }
                }
            }
            
            // Removed player イベントも参照（追加情報として）
            var removedMatches = _playerRemovedPattern.Matches(logContent);
            foreach (Match match in removedMatches)
            {
                if (match.Groups.Count >= 3 && DateTime.TryParse(match.Groups[1].Value, out DateTime leftTime))
                {
                    string username = CleanUsername(match.Groups[2].Value);
                    if (!string.IsNullOrEmpty(username))
                    {
                        // 既に同じユーザーのLEAVEがあるか確認
                        bool alreadyExists = events.Any(e => 
                            e.Item2 == "LEAVE" && 
                            string.Equals(e.Item3, username, StringComparison.OrdinalIgnoreCase));
                        
                        if (!alreadyExists)
                        {
                            events.Add((leftTime, "LEAVE", username));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// ユーザー名のクリーンアップ（ID部分を除去）
        /// </summary>
        private string CleanUsername(string rawUsername)
        {
            if (string.IsNullOrEmpty(rawUsername)) return string.Empty;
            
            // ユーザーID部分を削除（例: "ユーザー名 (usr_xxxxx)" → "ユーザー名"）
            int parenthesesIndex = rawUsername.IndexOf('(');
            if (parenthesesIndex > 0)
            {
                return rawUsername.Substring(0, parenthesesIndex).Trim();
            }
            
            return rawUsername.Trim();
        }
        
        /// <summary>
        /// ユーザーイベントの処理
        /// </summary>
        private void ProcessUserEvent(DateTime eventTime, string eventType, string username)
        {
            // ローカルユーザー（撮影者自身）は処理から除外
            if (IsLocalUser(username))
            {
                Console.WriteLine($"[DEBUG] ローカルユーザーをスキップ: {username}");
                return;
            }
            
            switch (eventType)
            {
                case "JOIN":
                    if (_instanceUsers.ContainsKey(username))
                    {
                        // 既に記録されている場合は時刻を更新（再join）
                        _instanceUsers[username].JoinTime = eventTime;
                        _instanceUsers[username].LeaveTime = null;
                    }
                    else
                    {
                        // 新規ユーザー
                        _instanceUsers[username] = new UserState
                        {
                            Username = username,
                            JoinTime = eventTime,
                            LeaveTime = null
                        };
                    }
                    Console.WriteLine($"[DEBUG] ユーザー参加: {username} ({eventTime:HH:mm:ss})");
                    break;
                case "LEAVE":
                    if (_instanceUsers.ContainsKey(username))
                    {
                        _instanceUsers[username].LeaveTime = eventTime;
                        Console.WriteLine($"[DEBUG] ユーザー退出: {username} ({eventTime:HH:mm:ss})");
                    }
                    else
                    {
                        // 大規模インスタンスではすべてのJOINイベントをログから取得できない場合があるため
                        // 未知のユーザーのLEAVEに対して新しいユーザー状態を作成し、すでに退出したと記録
                        _instanceUsers[username] = new UserState
                        {
                            Username = username,
                            JoinTime = _currentInstanceStartTime, // インスタンス開始時に参加していたと仮定
                            LeaveTime = eventTime
                        };
                        Console.WriteLine($"[DEBUG] 大規模インスタンス: JOINなしユーザー退出: {username} ({eventTime:HH:mm:ss})");
                    }
                    break;
            }
        }
        
        /// <summary>
        /// 現在のフレンドリストを更新
        /// </summary>
        private void UpdateCurrentFriendsList()
        {
            // 現在のフレンドリストをクリア
            CurrentFriends.Clear();
            
            // 現在もインスタンスにいるユーザーのみを追加
            foreach (var userState in _instanceUsers.Values)
            {
                if (userState.IsCurrentlyInInstance)
                {
                    CurrentFriends.Add(userState.Username);
                }
            }

            Console.WriteLine($"[DEBUG] 現在のインスタンス内ユーザー数: {CurrentFriends.Count}");
            foreach (var username in CurrentFriends)
            {
                var state = _instanceUsers[username];
                Console.WriteLine($"  {username} (参加: {state.JoinTime:HH:mm:ss})");
            }
        }
        
        /// <summary>
        /// 指定時刻以降のログを抽出
        /// </summary>
        private string ExtractLogAfterTime(string logContent, DateTime afterTime)
        {
            string timeString = afterTime.ToString("yyyy.MM.dd HH:mm:ss");
            int startIndex = logContent.IndexOf(timeString);
            
            if (startIndex >= 0)
            {
                return logContent.Substring(startIndex);
            }
            
            // 正確な時刻が見つからない場合は、近い時刻から開始
            var timeMatches = _timeStampLogPattern.Matches(logContent);
            
            // 指定時刻以降の最初のエントリを探す
            foreach (Match match in timeMatches)
            {
                if (DateTime.TryParse(match.Groups[1].Value, out DateTime entryTime))
                {
                    if (entryTime >= afterTime.AddMinutes(-1)) // 1分のバッファ
                    {
                        return logContent.Substring(match.Index);
                    }
                }
            }
            
            // どれも見つからない場合は最後の30%のログを使用
            int cutPosition = (int)(logContent.Length * 0.7);
            return logContent.Substring(cutPosition);
        }
        
        /// <summary>
        /// 現在のインスタンス内ユーザーの詳細状態を取得
        /// </summary>
        public List<UserState> GetCurrentInstanceUsers()
        {
            return _instanceUsers.Values.Where(u => u.IsCurrentlyInInstance).ToList();
        }
        
        /// <summary>
        /// フレンドリストを指定の区切り文字で結合した文字列を取得（改良版）
        /// </summary>
        public string GetFriendsString(string separator = ".")
        {
            if (CurrentFriends == null || CurrentFriends.Count == 0)
            {
                return "ボッチ(だれもいません)";
            }
            return string.Join(separator, CurrentFriends);
        }
        
        /// <summary>
        /// メタデータ辞書を生成（PngCSで使用）
        /// </summary>
        /// <returns>メタデータキーと値のディクショナリ</returns>
        public Dictionary<string, string> GenerateMetadata()
        {
            // 現在のインスタンス状態を再構築
            ReconstructCurrentInstanceState();
            
            // メタデータの生成（Form1から直接使用可能）
            return new Dictionary<string, string>
            {
                // 処理済みマーカー
                { "VSACheck", "true" },
                
                // ワールド情報
                { "WorldName", CurrentWorldName },
                { "WorldID", CurrentWorldId },
                
                // フレンド情報（.区切り）
                { "Usernames", GetFriendsString() },
                
                // 撮影者情報
                { "User", Username },
                
                // 撮影日時
                { "CaptureTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };
        }

        /// <summary>
        /// ワールド変更イベント発火
        /// </summary>
        protected virtual void OnWorldChanged(WorldChangedEventArgs e)
        {
            WorldChanged?.Invoke(this, e);
        }

        /// <summary>
        /// エラーログを出力（アプリケーションのログシステムと連携可能）
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        private void LogError(string message)
        {
            Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            
            // 将来的なログ連携のためのフック
            // Logger.LogError(message); などに置き換え可能
        }
        
        /// <summary>
        /// リソース解放
        /// </summary>
        public void Dispose()
        {
            _autoUpdateTimer?.Dispose();
        }
        
        /// <summary>
        /// 現在のワールド情報をリセット（明示的なリセット処理）
        /// </summary>
        public void ResetWorldData()
        {
            CurrentWorldName = "Unknown World";
            CurrentWorldId = "";
            CurrentFriends.Clear();
            _playerJoinTimestamps.Clear();
            _lastWorldChangeTime = DateTime.MinValue;
            _instanceUsers.Clear();
            _currentInstanceStartTime = DateTime.MinValue;
            
            Console.WriteLine("[DEBUG] ワールド情報を明示的にリセットしました");
        }
        
        /// <summary>
        /// アプリケーション起動時の初期化処理（明示的な初期化メソッド）
        /// </summary>
        public void InitializeFromLatestLog()
        {
            // 現在の情報をリセット
            ResetWorldData();
            
            // 最新のログを解析
            if (ReconstructCurrentInstanceState())
            {
                Console.WriteLine("[DEBUG] アプリ起動初期化: インスタンス状態再構築成功");
            }
            else
            {
                Console.WriteLine("[DEBUG] アプリ起動初期化: インスタンス状態再構築失敗");
            }
        }

        /// <summary>
        /// ユーザー名比較のための拡張メソッドを追加
        /// </summary>
        private bool UsernameEquals(string name1, string name2)
        {
            if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
            {
                Console.WriteLine($"[DEBUG] ユーザー名比較: 空の名前があります name1=「{name1}」, name2=「{name2}」");
                return false;
            }
            
            bool result = string.Equals(name1.Trim(), name2.Trim(), StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"[DEBUG] ユーザー名比較: 「{name1}」 vs 「{name2}」 結果={result}");
            return result;
        }

        /// <summary>
        /// ローカルユーザーかどうかを判定
        /// </summary>
        private bool IsLocalUser(string playerName)
        {
            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(Username))
            {
                return false;
            }
            
            // 大文字小文字を区別するように変更
            bool isLocal = string.Equals(playerName.Trim(), Username.Trim(), StringComparison.Ordinal);
            
            if (isLocal)
            {
                Console.WriteLine($"[DEBUG] ローカルユーザー判定: {playerName} = {Username} (ローカル)");
            }
            
            return isLocal;
        }
    }
    
    /// <summary>
    /// ワールド変更イベント引数
    /// </summary>
    public class WorldChangedEventArgs : EventArgs
    {
        public string WorldName { get; }
        public string WorldId { get; }
        
        public WorldChangedEventArgs(string worldName, string worldId)
        {
            WorldName = worldName;
            WorldId = worldId;
        }
    }
}
