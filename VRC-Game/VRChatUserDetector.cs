using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VSA_launcher
{
    /// <summary>
    /// VRChatのログからユーザー名を検出するための専用クラス
    /// </summary>
    public class VRChatUserDetector
    {
        // ユーザー情報データモデル
        public class VRChatUserInfo
        {
            public string WorldName { get; set; } = "Unknown World";
            public string WorldId { get; set; } = string.Empty;
            public DateTime CaptureTime { get; set; }
            public string Photographer { get; set; } = "Unknown User";
            public List<string> Friends { get; set; } = new();
            
            public string ToDelimitedString() => 
                $"{WorldName};{WorldId};{CaptureTime:yyyyMMddHHmmss};{Photographer};{string.Join(",", Friends)}";
        }        // 正規表現パターン
        private readonly Regex _localUserPattern = new Regex(@".*\[Behaviour\] Initialized PlayerAPI ""([^""]+)"" is local", RegexOptions.Compiled);
        private readonly Regex _remoteUserPattern = new Regex(@".*\[Behaviour\] Initialized PlayerAPI ""([^""]+)"" is remote", RegexOptions.Compiled);
        private readonly Regex _authUserPattern = new Regex(@".*User Authenticated: ([^\(]+) \(usr_[a-z0-9\-]+\)", RegexOptions.Compiled);

        // 最後に検出したローカルユーザー（自分自身）
        private string _detectedLocalUser = "Unknown User";
        /// <summary>
        /// ログコンテンツからローカルユーザー（自分自身）を検出
        /// </summary>
        public string DetectLocalUser(string logContent)
        {
            // 最新のマッチを探すために、すべてのマッチを確認
            string detectedUser = "Unknown User";

            // PlayerAPI パターンで検索（最新の物を使用）
            var localMatches = _localUserPattern.Matches(logContent);
            if (localMatches.Count > 0)
            {
                // 最後のマッチを使用
                var lastMatch = localMatches[localMatches.Count - 1];
                if (lastMatch.Groups.Count > 1)
                {
                    string username = lastMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(username))
                    {
                        detectedUser = username;
                        _detectedLocalUser = username;
                        Console.WriteLine($"[DEBUG] PlayerAPI ローカルユーザー検出: {username}");
                    }
                }
            }

            // User Authenticated パターンで検索
            var authMatches = _authUserPattern.Matches(logContent);
            if (authMatches.Count > 0)
            {
                // 最後のマッチを使用
                var lastMatch = authMatches[authMatches.Count - 1];
                if (lastMatch.Groups.Count > 1)
                {
                    string username = lastMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(username))
                    {
                        detectedUser = username;
                        _detectedLocalUser = username;
                        Console.WriteLine($"[DEBUG] User Authenticated ローカルユーザー検出: {username}");
                    }
                }
            }

            Console.WriteLine($"[DEBUG] 最終的に検出されたローカルユーザー: {detectedUser}");
            return detectedUser;
        }

        /// <summary>
        /// ログコンテンツからリモートユーザー（他プレイヤー）のリストを検出
        /// </summary>
        public List<string> DetectRemoteUsers(string logContent, DateTime instanceStartTime)
        {
            HashSet<string> remoteUsers = new HashSet<string>();
            var remoteMatches = _remoteUserPattern.Matches(logContent);
            foreach (Match match in remoteMatches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    string username = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(username) && !IsLocalUser(username))
                    {
                        remoteUsers.Add(username);
                    }
                }
            }

            return remoteUsers.Count > 0 ? new List<string>(remoteUsers) : new List<string> { "ボッチ(だれもいません)" };
        }

        /// <summary>
        /// ユーザー情報全体を構築
        /// </summary>
        public VRChatUserInfo BuildUserInfo(string worldName, string worldId, List<string> remoteUsers)
        {
            return new VRChatUserInfo
            {
                WorldName = worldName,
                WorldId = worldId,
                CaptureTime = DateTime.Now,
                Photographer = _detectedLocalUser,
                Friends = remoteUsers
            };
        }

        /// <summary>
        /// 指定されたユーザー名が自分自身（ローカルユーザー）かどうか判定
        /// </summary>
        private bool IsLocalUser(string username)
        {
            // 大文字小文字を区別するように変更 (StringComparison.Ordinal を使用)
            return string.Equals(username.Trim(), _detectedLocalUser.Trim(), StringComparison.Ordinal);
        }
    }
}
