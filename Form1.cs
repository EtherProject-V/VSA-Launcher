using System;
using System.Collections.Generic;
using System.Diagnostics; // Process関連の操作のため
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using VSA_launcher.OSCServer; // 追加
using VSA_launcher.VRC_Game; // VRChatInitializationManager用
using VRC.OSCQuery; // 追加

namespace VSA_launcher
{
    public partial class VSA_launcher : Form
    {
        private FolderBrowserDialog screenshotFolderBrowser = new FolderBrowserDialog();
        private FolderBrowserDialog outputFolderBrowser = new FolderBrowserDialog();
        private SystemTrayIcon _systemTrayIcon = null!;
        private AppSettings _settings = null!;
        private int _detectedFilesCount = 0;
        private int _processedFilesCount = 0;
        private int _errorCount = 0;
        private FileWatcherService _fileWatcher = null!;
        private System.Windows.Forms.Timer _statusUpdateTimer = null!;
        private readonly VRChatLogParser _logParser = null!;
        private ImageProcessor _imageProcessor = null!;
        private FolderStructureManager _folderManager = null!;
        private FileNameGenerator _fileNameGenerator = null!;
        private string _currentMetadataImagePath = string.Empty;

        // OSC関連の追加
        private OscDataStore _oscDataStore = null!;
        private VRChatListener _vrchatListener = null!; // VRChatからの受信用リスナー
        private CancellationTokenSource _cancellationTokenSource = null!;
        private OSCQueryService? _oscQueryService;
        private OSCParameterSender? _oscParameterSender; // OSC送信専用クラス

        // VRChat監視用
        private System.Windows.Forms.Timer _vrchatMonitorTimer = null!;
        private bool _isVRChatRunning = false;
        private bool _hasInitializedCamera = false; // カメラ初期化済みフラグ
        private bool _cameraSettingsApplied = false; // カメラ設定適用済みフラグ
        private bool _hasExecutedOscInitialization = false; // アプリセッション中のOSC初期化実行フラグ

        private VRC_Game.VRChatInitializationManager? _vrchatInitializationManager;

        // 設定ファイルから読み込んだスタートアップ設定
        private bool _startWithWindows = false;

        // カメラモード選択UI要素
        private RadioButton normalCamera_radioButton = null!;
        private RadioButton integral_radioButton = null!;
        private RadioButton virtualLens2_radioButton = null!;
        private TextBox CameraUsetextbox = null!;

        public VSA_launcher()
        {
            try
            {
                InitializeComponent();
                _settings = SettingsManager.LoadSettings(); // ここに移動

                // LauncherSettingsがnullの場合に備えて初期化
                if (_settings.LauncherSettings == null)
                {
                    _settings.LauncherSettings = new LauncherSettings();
                }

                // OSC関連の初期化
                _cancellationTokenSource = new CancellationTokenSource();
                _oscDataStore = new OSCServer.OscDataStore();

                // OSCQueryServiceの初期化
                _oscQueryService = new OSCQueryServiceBuilder()
                    .AdvertiseOSC()
                    .AdvertiseOSCQuery()
                    .Build();

                // OSC設定に基づいてVRChatからの受信用リスナーを開始
                _vrchatListener = new OSCServer.VRChatListener(_oscDataStore);
                _vrchatListener.Start();

                // OSC受信イベントの設定
                _vrchatListener.MessageReceived += OnOscMessageReceived;
                _vrchatListener.LogMessageReceived += OnOscLogMessageReceived;

                Console.WriteLine($"[OSC初期化] VRChat OSC Listener started - 受信ポート: {_settings.LauncherSettings.OSCSettings.ReceiverPort}");
                System.Diagnostics.Debug.WriteLine("VRChat OSC Listener started on port 9001");

                // OSCマネージャーを初期化
                var oscManager = new OSCServer.OscManager(_cancellationTokenSource.Token, _oscDataStore, _oscQueryService);
                oscManager.Start();
                oscManager.StartParameterMonitoring();
                Console.WriteLine($"[OSC初期化] OSC Manager started - 送信先: 127.0.0.1:{_settings.LauncherSettings.OSCSettings.SenderPort}");

                // OSCParameterSenderを初期化
                _oscParameterSender = new OSCServer.OSCParameterSender(oscManager, _oscDataStore, _settings);
                Console.WriteLine("[OSC初期化] OSCParameterSender initialized");

                _systemTrayIcon = new SystemTrayIcon(this, notifyIcon, contextMenuStrip1);

                // ファイル監視サービスの初期化 - 設定を渡す
                _fileWatcher = new FileWatcherService();
                _fileWatcher.StatusChanged += FileWatcher_StatusChanged;
                _fileWatcher.FileDetected += FileWatcher_FileDetected;  // 重複登録を削除

                // 以下、残りの初期化処理...
                _statusUpdateTimer = new System.Windows.Forms.Timer();
                _statusUpdateTimer.Interval = 3000; // 3秒ごとに更新
                _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
                _statusUpdateTimer.Start();

                launchMainApp_button.Click += (s, e) => LaunchMainApplication();
                metadataEnabled_checkBox.CheckedChanged += metadataEnabled_CheckedChanged;
                monthCompression_checkBox.CheckedChanged += monthCompression_CheckedChanged;
                monthRadio_Button.CheckedChanged += radioButton_CheckedChanged;
                weekRadio_Button.CheckedChanged += radioButton_CheckedChanged;
                dayRadio_Button.CheckedChanged += radioButton_CheckedChanged;
                fileSubdivision_checkBox.CheckedChanged += checkBox3_CheckedChanged;
                // PictureBoxクリックイベントの登録
                PngPreview_pictureBox.Click += PngPreview_pictureBox_Click;

                // ファイル名フォーマットのコンボボックス変更イベント追加
                if (fileRename_comboBox != null)
                {
                    fileRename_comboBox.SelectedIndexChanged += FileRename_ComboBox_SelectedIndexChanged;

                    // コンボボックスの初期化（値がまだ設定されていない場合）
                    if (fileRename_comboBox.Items.Count == 0)
                    {
                        InitializeFileRenameComboBox();
                    }
                }

                _logParser = new VRChatLogParser();

                System.Windows.Forms.Timer logUpdateTimer = new System.Windows.Forms.Timer();
                logUpdateTimer.Interval = 2000; // 2秒ごとに更新
                logUpdateTimer.Tick += (s, e) =>
                {
                    _logParser.ParseLatestLog();

                    // ログを見に行った後、現在のフレンド情報をコンソールに出力
                    Debug.WriteLine($"[{DateTime.Now:yyyy.MM.dd HH:mm:ss}] 現在のインスタンス内ユーザー情報:");

                    // フレンドリストを取得して出力
                    if (_logParser.CurrentFriends != null && _logParser.CurrentFriends.Any())
                    {
                        foreach (var friend in _logParser.CurrentFriends)
                        {
                            Debug.WriteLine($" - {friend}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine(" - インスタンス内ユーザー情報なし");
                    }

                    // 世界情報も出力
                    Debug.WriteLine("----------------------------------------");
                };
                logUpdateTimer.Start();

                // 画像プロセッサを初期化
                _folderManager = new FolderStructureManager(_settings);
                _fileNameGenerator = new FileNameGenerator(_settings);
                _imageProcessor = new ImageProcessor(_settings, _logParser, _fileWatcher, UpdateStatusInfo, _oscDataStore);

                // スタートアップ設定を適用
                _startWithWindows = _settings.LauncherSettings.StartWithWindows;
                startup_checkBox.Checked = _startWithWindows;

                // VRChat監視タイマーの初期化
                _vrchatMonitorTimer = new System.Windows.Forms.Timer();
                _vrchatMonitorTimer.Interval = 30000; // 30秒ごと
                _vrchatMonitorTimer.Tick += VRChatMonitorTimer_Tick;
                _vrchatMonitorTimer.Start();

                if (_oscParameterSender != null)
                {
                    _vrchatInitializationManager = new VRC_Game.VRChatInitializationManager(
                        _logParser,
                        _oscParameterSender,
                        UpdateStatusInfo
                    );
                    _vrchatInitializationManager.Start();
                    Console.WriteLine("[Form1] VRChat初期化マネージャーを開始しました");
                }

                // カメラUI制御の初期化
                InitializeCameraControls();

                // カメラ設定ボタンのイベントハンドラ追加
                if (CameraSettingApply_button != null)
                {
                    CameraSettingApply_button.Click += CameraSettingApply_button_Click;
                    CameraSettingApply_button.Text = "設定を保存"; // ボタンテキストを設定
                }

                // 開発モード・OSCログ関連のイベントハンドラ追加
                if (devMode_checkBox != null)
                {
                    devMode_checkBox.CheckedChanged += (s, e) =>
                    {
                        // 開発モードOFF時はOSCステータスラベルを非表示
                        if (label5 != null)
                        {
                            label5.Visible = devMode_checkBox.Checked;
                        }

                        // OSCStatusタブの表示/非表示制御
                        if (tabControl != null && OSCStatus != null)
                        {
                            if (devMode_checkBox.Checked)
                            {
                                // 開発モードON: OSCStatusタブを表示
                                if (!tabControl.TabPages.Contains(OSCStatus))
                                {
                                    tabControl.TabPages.Add(OSCStatus);
                                }
                            }
                            else
                            {
                                // 開発モードOFF: OSCStatusタブを非表示
                                if (tabControl.TabPages.Contains(OSCStatus))
                                {
                                    tabControl.TabPages.Remove(OSCStatus);
                                }
                            }
                        }
                    };
                }

                if (OSCLog_checkBox != null)
                {
                    OSCLog_checkBox.CheckedChanged += (s, e) =>
                    {
                        // OSCログOFF時はテキストボックスをクリア
                        if (!OSCLog_checkBox.Checked && OSCLog_richTextBox != null)
                        {
                            OSCLog_richTextBox.Clear();
                        }
                    };
                }

                // OSCStatusタブの初期状態を設定（開発モードがOFFの場合は非表示）
                if (tabControl != null && OSCStatus != null && devMode_checkBox != null)
                {
                    if (!devMode_checkBox.Checked && tabControl.TabPages.Contains(OSCStatus))
                    {
                        tabControl.TabPages.Remove(OSCStatus);
                    }
                }

                // CameraSettingsタブの初期状態を設定（cameraSettomg_checkBoxがOFFの場合は非表示）
                if (tabControl != null && CameraSettings != null && cameraSettomg_checkBox != null)
                {
                    if (!cameraSettomg_checkBox.Checked && tabControl.TabPages.Contains(CameraSettings))
                    {
                        tabControl.TabPages.Remove(CameraSettings);
                    }
                }

                // スタートアップの実際の状態を反映
            }
            catch (Exception ex)
            {
                MessageBox.Show($"アプリケーション初期化エラー: {ex.Message}\n\nスタックトレース: {ex.StackTrace}",
                               "起動エラー",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        // ファイル名フォーマットコンボボックスの初期化
        private void InitializeFileRenameComboBox()
        {
            // コンボボックスに項目を追加
            fileRename_comboBox.Items.Clear();
            fileRename_comboBox.Items.Add("名前を変更しない"); // インデックス0
            fileRename_comboBox.Items.Add("年_月_日_時分_連番"); // インデックス1
            fileRename_comboBox.Items.Add("年月日_時分_連番"); // インデックス2
            fileRename_comboBox.Items.Add("年-月-日-曜日-時分-連番"); // インデックス3
            fileRename_comboBox.Items.Add("日-月-年-時分-連番"); // インデックス4
            fileRename_comboBox.Items.Add("月-日-年-時分-連番"); // インデックス5
            fileRename_comboBox.Items.Add("年.月.日.時分.連番"); // インデックス6
            fileRename_comboBox.Items.Add("時分_年月日_連番"); // インデックス7

            // 設定に基づいて選択項目を設定
            bool enabled = _settings.FileRenaming.Enabled;
            string format = _settings.FileRenaming.Format;

            int selectedIndex = 0; // デフォルトは「変更しない」

            if (enabled)
            {
                // フォーマットに基づいて適切なインデックスを選択
                switch (format)
                {
                    case "yyyy_MM_dd_HHmm_seq": selectedIndex = 1; break;
                    case "yyyyMMdd_HHmm_seq": selectedIndex = 2; break;
                    case "yyyy-MM-dd-ddd-HHmm-seq": selectedIndex = 3; break;
                    case "dd-MM-yyyy-HHmm-seq": selectedIndex = 4; break;
                    case "MM-dd-yyyy-HHmm-seq": selectedIndex = 5; break;
                    case "yyyy.MM.dd.HHmm.seq": selectedIndex = 6; break;
                    case "HHmm_yyyyMMdd_seq": selectedIndex = 7; break;
                    default: selectedIndex = 0; break;
                }
            }

            fileRename_comboBox.SelectedIndex = selectedIndex;

            // ラベル初期更新
            UpdateFileRenamePreviewLabel();
        }

        // コンボボックス変更イベントハンドラ
        private void FileRename_ComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // 選択されたインデックスに基づいて設定を更新
            int selectedIndex = fileRename_comboBox.SelectedIndex;

            // 名前変更が有効かどうか（0以外なら有効）
            _settings.FileRenaming.Enabled = (selectedIndex != 0);

            // フォーマットの更新
            switch (selectedIndex)
            {
                case 1: _settings.FileRenaming.Format = "yyyy_MM_dd_HHmm_seq"; break;
                case 2: _settings.FileRenaming.Format = "yyyyMMdd_HHmm_seq"; break;
                case 3: _settings.FileRenaming.Format = "yyyy-MM-dd-ddd-HHmm-seq"; break;
                case 4: _settings.FileRenaming.Format = "dd-MM-yyyy-HHmm-seq"; break;
                case 5: _settings.FileRenaming.Format = "MM-dd-yyyy-HHmm-seq"; break;
                case 6: _settings.FileRenaming.Format = "yyyy.MM.dd.HHmm.seq"; break;
                case 7: _settings.FileRenaming.Format = "HHmm_yyyyMMdd_seq"; break;
                default: _settings.FileRenaming.Format = ""; break;
            }

            // 設定を保存
            SettingsManager.SaveSettings(_settings);

            // プレビューラベルを更新
            UpdateFileRenamePreviewLabel();
        }

        // プレビューラベルの更新
        private void UpdateFileRenamePreviewLabel()
        {
            if (fileRename_label == null) return;

            // 選択されたインデックス
            int selectedIndex = fileRename_comboBox.SelectedIndex;

            if (selectedIndex == 0)
            {
                // 名前変更なしの場合
                fileRename_label.Text = "ファイル名はそのまま保持されます";
                return;
            }

            // フォーマットに基づくプレビューを生成
            string previewName = _fileNameGenerator.GeneratePreviewFileName(_settings.FileRenaming.Format);

            // ラベルに表示
            fileRename_label.Text = $"例: {previewName}";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 設定を読み込み、UIに反映
            ApplySettingsToUI();

            // スタートアップ設定の初期化
            InitializeStartupSetting();


            // 初期状態のステータス表示（カメラモード情報を含む）
            UpdateStatusInfoWithCamera("アプリケーション初期化完了", "監視準備中...");

            // スクリーンショットフォルダが設定済みなら監視を開始
            if (!string.IsNullOrEmpty(_settings.ScreenshotPath) && Directory.Exists(_settings.ScreenshotPath))
            {
                StartWatching();
            }
            else
            {
                UpdateStatusInfo("監視未設定", "スクリーンショットフォルダを設定してください");
            }
        }

        private void ApplySettingsToUI()
        {
            // パス設定
            screenShotFile_textBox.Text = _settings.ScreenshotPath;
            outPut_textBox.Text = _settings.OutputPath;

            // チェックボックス
            metadataEnabled_checkBox.Checked = _settings.Metadata.Enabled;
            fileSubdivision_checkBox.Checked = _settings.FolderStructure.Enabled;
            monthCompression_checkBox.Checked = _settings.Compression.AutoCompress;

            // フォルダ分け設定（ラジオボタン）
            switch (_settings.FolderStructure.Type)
            {
                case "month":
                    monthRadio_Button.Checked = true;
                    break;
                case "week":
                    weekRadio_Button.Checked = true;
                    break;
                case "day":
                    dayRadio_Button.Checked = true;
                    break;
            }

            // ファイル名フォーマットのコンボボックスを更新
            if (fileRename_comboBox != null)
            {
                InitializeFileRenameComboBox();
            }

            // フォルダ分けグループのUI状態
            fileSubdivision_Group.Enabled = fileSubdivision_checkBox.Checked;
        }

        private void screenShotFile_button_Click(object sender, EventArgs e)
        {
            if (screenshotFolderBrowser.ShowDialog() == DialogResult.OK)
            {
                screenShotFile_textBox.Text = screenshotFolderBrowser.SelectedPath;
                _settings.ScreenshotPath = screenshotFolderBrowser.SelectedPath;
                SettingsManager.SaveSettings(_settings);

                // フォルダ設定後に監視を開始
                StartWatching();
            }
        }
        private void outPut_button_Click(object sender, EventArgs e)
        {
            if (outputFolderBrowser.ShowDialog() == DialogResult.OK)
            {
                outPut_textBox.Text = outputFolderBrowser.SelectedPath;
                _settings.OutputPath = outputFolderBrowser.SelectedPath;
                SettingsManager.SaveSettings(_settings);

                // ステータス更新
                UpdateStatusInfo("出力先フォルダを設定しました", $"フォルダ: {_settings.OutputPath}");
            }
        }

        private void metadataEnabled_CheckedChanged(object? sender, EventArgs e)
        {
            _settings.Metadata.Enabled = metadataEnabled_checkBox.Checked;
            SettingsManager.SaveSettings(_settings);
        }

        private void checkBox3_CheckedChanged(object? sender, EventArgs e)
        {
            fileSubdivision_Group.Enabled = fileSubdivision_checkBox.Checked;
            _settings.FolderStructure.Enabled = fileSubdivision_checkBox.Checked;
            SettingsManager.SaveSettings(_settings);
        }

        private void monthCompression_CheckedChanged(object? sender, EventArgs e)
        {
            _settings.Compression.AutoCompress = monthCompression_checkBox.Checked;
            SettingsManager.SaveSettings(_settings);
        }

        private void radioButton_CheckedChanged(object? sender, EventArgs e)
        {
            if (monthRadio_Button.Checked)
                _settings.FolderStructure.Type = "month";
            else if (weekRadio_Button.Checked)
                _settings.FolderStructure.Type = "week";
            else if (dayRadio_Button.Checked)
                _settings.FolderStructure.Type = "day";

            SettingsManager.SaveSettings(_settings);
        }

        // ステータス表示の更新
        public void UpdateStatusInfo(string statusMessage, string fileStatusMessage)
        {
            // UIスレッドでの実行を保証
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateStatusInfo(statusMessage, fileStatusMessage)));
                return;
            }

            startingState_toolStripStatusLabel.Text = statusMessage;
            fileStatus_toolStripStatusLabel1.Text = fileStatusMessage;
        }

        // カメラモード情報を含むステータス表示の更新
        public void UpdateStatusInfoWithCamera(string statusMessage, string fileStatusMessage)
        {
            // UIスレッドでの実行を保証
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateStatusInfoWithCamera(statusMessage, fileStatusMessage)));
                return;
            }

            // カメラモード情報を取得
            string cameraStatus = _oscDataStore.GetCameraModeStatus();

            startingState_toolStripStatusLabel.Text = $"{statusMessage} | {cameraStatus}";
            fileStatus_toolStripStatusLabel1.Text = fileStatusMessage;
        }

        // 処理状態の更新
        public void UpdateProcessingStats(int detected, int processed, int errors)
        {
            _detectedFilesCount = detected;
            _processedFilesCount = processed;
            _errorCount = errors;

            // ファイル統計表示の更新
            UpdateStatusInfo($"監視中: {detected}ファイル", $"処理済: {processed} エラー: {errors}");
        }

        // メインアプリ起動
        // publicに変更してSystemTrayIconからアクセスできるようにする
        public void LaunchMainApplication()
        {
            MessageBox.Show(
                "現在メインアプリは開発中です。完成をお待ちください。\n\n" +
                "最新情報は下記のURLからご確認ください：\n" +
                "Booth: https://fefaether-vrc.booth.pm/\n" +
                "Twitter(X): https://x.com/fefaethervrc",
                "お知らせ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 起動ボタンの状態を更新
        /// </summary>
        private void UpdateLaunchButtonState()
        {
            bool isRunning = IsMainAppRunning();

            // UIスレッドでの実行を保証
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateLaunchButtonState));
                return;
            }

            // ボタンの状態を更新
            launchMainApp_button.Enabled = !isRunning;
            launchMainApp_button.Text = isRunning ? "アプリ実行中" : "アプリを起動する";

            // システムトレイのメニュー項目も更新
            メインアプリケーションを起動ToolStripMenuItem.Enabled = !isRunning;
            メインアプリケーションを起動ToolStripMenuItem.Text = isRunning ? "アプリ実行中" : "メインアプリケーションを起動";
        }

        private bool IsMainAppRunning()
        {
            try
            {
                // 専用のプロセス名で検索
                string[] exactProcessNames = new[] { "SnapArchiveKai", "VrcSnapArchive" };
                foreach (string processName in exactProcessNames)
                {
                    if (Process.GetProcessesByName(processName).Length > 0)
                    {
                        return true;
                    }
                }

                // Electronプロセスを検索し、起動引数をチェック
                Process[] electronProcesses = Process.GetProcessesByName("electron");
                if (electronProcesses.Length > 0)
                {
                    // 相互排他ロックファイルの存在確認
                    string lockFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SnapArchiveKai",
                        ".app_running");

                    if (File.Exists(lockFilePath))
                    {
                        try
                        {
                            // ファイルの最終更新時間が5分以内なら実行中と判断
                            if ((DateTime.Now - File.GetLastWriteTime(lockFilePath)).TotalMinutes < 5)
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // ファイルアクセスエラーは無視
                        }
                    }

                    // お互いに排他的なミューテックス名を使用（アプリケーション間で共有）
                    bool createdNew;
                    using (var mutex = new Mutex(false, "SnapArchiveKaiRunningInstance", out createdNew))
                    {
                        // ミューテックスがすでに存在する（獲得できない）なら実行中
                        if (!createdNew && !mutex.WaitOne(0))
                        {
                            return true;
                        }

                        if (!createdNew)
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }

                return false;
            }
            catch
            {
                // 例外発生時は安全のため実行していないと判断
                return false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        private void toolStripTextBox1_Click(object sender, EventArgs e)
        {
            // 何もしない
        }

        private void label1_Click(object sender, EventArgs e)
        {
            // 何もしない
        }

        private void StartWatching()
        {
            if (string.IsNullOrEmpty(_settings.ScreenshotPath))
            {
                UpdateStatusInfo("監視エラー", "スクリーンショットフォルダが設定されていません");
                return;
            }

            // 監視開始前に一旦停止
            _fileWatcher.StopWatching();

            // ディレクトリが存在するか確認
            if (!Directory.Exists(_settings.ScreenshotPath))
            {
                UpdateStatusInfo("監視エラー", $"指定されたフォルダが見つかりません: {_settings.ScreenshotPath}");
                return;
            }

            // 月別フォルダ構造を検出し、適切な監視方法を選択
            bool success = _fileWatcher.StartWatching(_settings.ScreenshotPath);

            if (success)
            {
                if (_fileWatcher.CurrentMonthFolder != null)
                {
                    // 月別フォルダ監視が自動的に開始された
                    UpdateStatusInfo("月別フォルダ監視開始",
                        $"親フォルダ: {_settings.ScreenshotPath}, 現在の月: {Path.GetFileName(_fileWatcher.CurrentMonthFolder)}");
                }
                else
                {
                    // 通常の単一フォルダ監視
                    UpdateStatusInfo("監視開始", $"フォルダ: {_settings.ScreenshotPath}");
                }
            }
            else
            {
                UpdateStatusInfo("監視開始失敗", "フォルダの監視を開始できませんでした");
            }
        }

        private void FileWatcher_StatusChanged(object? sender, StatusChangedEventArgs e)
        {
            // UIスレッドでの実行を保証
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => FileWatcher_StatusChanged(sender, e)));
                return;
            }

            UpdateStatusInfo(e.Message, $"監視: {_fileWatcher.DetectedFilesCount} 処理: {_fileWatcher.ProcessedFilesCount} エラー: {_fileWatcher.ErrorCount}");
        }

        private void FileWatcher_FileDetected(object? sender, FileDetectedEventArgs e)
        {
            // ファイルが検出されたときの処理
            // 実際の処理はバックグラウンドで
            Task.Run(() => ProcessFile(e.FilePath));
        }

        private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // 定期的なステータス更新
            if (_fileWatcher.IsWatching)
            {
                // カメラモード情報を含むステータス更新
                string fileStatus = $"監視: {_fileWatcher.DetectedFilesCount} 処理: {_fileWatcher.ProcessedFilesCount} エラー: {_fileWatcher.ErrorCount}";
                string cameraStatus = _oscDataStore.GetCameraModeStatus();

                startingState_toolStripStatusLabel.Text = $"監視中 | {cameraStatus}";
                fileStatus_toolStripStatusLabel1.Text = fileStatus;
            }
        }

        private void ProcessFile(string sourceFilePath)
        {
            _imageProcessor.ProcessImage(sourceFilePath);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fileWatcher?.Dispose();
                _statusUpdateTimer?.Dispose();
                _vrchatMonitorTimer?.Dispose(); // VRChat監視タイマーのDispose追加
                _vrchatInitializationManager?.Dispose(); // VRChat初期化マネージャーのDispose追加
                _systemTrayIcon?.Dispose();
                _cancellationTokenSource?.Cancel(); // OSCサーバーに停止を通知
                _vrchatListener?.Dispose(); // VRChatリスナーを
                _cancellationTokenSource?.Dispose();
                _oscQueryService?.Dispose(); // 追加
                _oscParameterSender = null; // OSCParameterSenderをnull化
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void fileSubdivision_Group_Enter(object sender, EventArgs e)
        {

        }

        private void checkBox3_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void メインアプリケーションを起動ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LaunchMainApplication();
        }

        private void launchMainApp_button_Click(object sender, EventArgs e)
        {
            LaunchMainApplication();
        }

        private void screenShotFile_textBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void PngMetaDate_button_Click(object sender, EventArgs e)
        {
            // ファイルをオープンして選択したファイルの情報を表示
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PNG画像|*.png|JPG画像|*.jpg;*.jpeg|すべてのファイル|*.*";
            openFileDialog.Title = "メタデータを表示する画像を選択";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                PngMetaDate_textBox.Text = openFileDialog.FileName;

                // 選択した画像を表示してメタデータを解析
                DisplayImageAndMetadata(openFileDialog.FileName);
            }
        }

        // 画像をプレビューに表示し、メタデータを解析して表示するメソッド
        public void DisplayImageAndMetadata(string imagePath)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => DisplayImageAndMetadata(imagePath)));
                return;
            }

            try
            {
                _currentMetadataImagePath = imagePath;

                // 画像プレビューの表示
                if (PngPreview_pictureBox.Image != null)
                {
                    PngPreview_pictureBox.Image.Dispose();
                    PngPreview_pictureBox.Image = null;
                }

                // 画像ファイルが存在するか確認
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    UpdateStatusInfo("エラー", "ファイルが見つかりません");
                    return;
                }

                // 画像を読み込んで表示
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    PngPreview_pictureBox.Image = Image.FromStream(stream);
                    PngPreview_pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                }

                // メタデータの取得と表示
                Dictionary<string, string> metadata = MetadataAnalyzer.ReadMetadataFromImage(imagePath);
                DisplayMetadata(metadata);
            }
            catch (Exception ex)
            {
                UpdateStatusInfo("画像読み込みエラー", ex.Message);
            }
        }

        // メタデータをテキストボックスなどに表示
        private void DisplayMetadata(Dictionary<string, string> metadata)
        {
            // デバッグログ出力
            System.Diagnostics.Debug.WriteLine($"DisplayMetadata called with {metadata.Count} items");
            foreach (var pair in metadata)
            {
                System.Diagnostics.Debug.WriteLine($"  {pair.Key} = {pair.Value}");
            }

            // メタデータの表示をクリア
            worldName_richTextBox.Text = string.Empty;
            worldFriends_richTextBox.Text = string.Empty;
            photoTime_textBox.Text = string.Empty;
            photographName_textBox.Text = string.Empty;

            // メタデータを表示
            if (metadata.TryGetValue("WorldName", out string? worldName) && worldName != null)
            {
                worldName_richTextBox.Text = worldName;
            }

            if (metadata.TryGetValue("Usernames", out string? usernames) && usernames != null) // 'Friends'を'Usernames'に変更
            {
                worldFriends_richTextBox.Text = usernames;
            }

            if (metadata.TryGetValue("CaptureTime", out string? captureTime) && captureTime != null)
            {
                photoTime_textBox.Text = captureTime;
            }

            if (metadata.TryGetValue("User", out string? user) && user != null) // 'Username'を'User'に変更
            {
                photographName_textBox.Text = user;
            }

            // カメラの使用状況を表示
            CameraInfo_richTextBox.Text = string.Empty; // テキストボックスをクリア
            bool isIntegral = metadata.TryGetValue("IsIntegral", out string? isIntegralStr) && isIntegralStr?.ToLower() == "true";
            bool isVirtualLens2 = metadata.TryGetValue("IsVirtualLens2", out string? isVirtualLens2Str) && isVirtualLens2Str?.ToLower() == "true";

            if (isIntegral)
            {
                CameraUse_textBox.Text = "Integral";
                StringBuilder sb = new StringBuilder();
                if (metadata.TryGetValue("Integral_Aperture", out string? aperture) && aperture != null) sb.AppendLine($"Aperture: {aperture}");
                if (metadata.TryGetValue("Integral_FocalLength", out string? focalLength) && focalLength != null) sb.AppendLine($"FocalLength: {focalLength}");
                if (metadata.TryGetValue("Integral_Exposure", out string? exposure) && exposure != null) sb.AppendLine($"Exposure: {exposure}");
                if (metadata.TryGetValue("Integral_ShutterSpeed", out string? shutterSpeed) && shutterSpeed != null) sb.AppendLine($"ShutterSpeed: {shutterSpeed}");
                if (metadata.TryGetValue("Integral_BokehShape", out string? bokehShape) && bokehShape != null) sb.AppendLine($"BokehShape: {bokehShape}");
                CameraInfo_richTextBox.Text = sb.ToString();
            }
            else if (isVirtualLens2)
            {
                CameraUse_textBox.Text = "VirtualLens2";
                StringBuilder sb = new StringBuilder();
                if (metadata.TryGetValue("VirtualLens2_Aperture", out string? aperture) && aperture != null) sb.AppendLine($"Aperture: {aperture}");
                if (metadata.TryGetValue("VirtualLens2_FocalLength", out string? focalLength) && focalLength != null) sb.AppendLine($"FocalLength: {focalLength}");
                if (metadata.TryGetValue("VirtualLens2_Exposure", out string? exposure) && exposure != null) sb.AppendLine($"Exposure: {exposure}");
                CameraInfo_richTextBox.Text = sb.ToString();
            }
            else
            {
                CameraUse_textBox.Text = "ノーマルカメラ";
                CameraInfo_richTextBox.Text = "ノーマルカメラのためなし";
            }

            // メタデータの存在確認とステータス表示
            if (metadata.Count == 0)
            {
                UpdateStatusInfo("メタデータなし", "この画像にはVSAメタデータが含まれていません");
            }
            else
            {
                UpdateStatusInfo("メタデータ読み込み完了", $"{metadata.Count}項目のメタデータを読み込みました");
            }
        }

        // PictureBoxのクリックイベント - 画像を外部ビューアで開く
        private void PngPreview_pictureBox_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentMetadataImagePath) && File.Exists(_currentMetadataImagePath))
            {
                try
                {
                    // 画像ファイルをデフォルトのビューアで開く
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _currentMetadataImagePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    UpdateStatusInfo("画像を開けませんでした", ex.Message);
                }
            }
        }

        // テスト画像作成ボタン用のコード
        private void CreateTestImage_button_Click(object sender, EventArgs e)
        {
            try
            {
                // テスト用メタデータ辞書
                var metadata = new Dictionary<string, string>
                {
                    { "VSACheck", "true" },
                    { "WorldName", "テストワールド名" },
                    { "WorldID", "wrld_test-world-id-123" },
                    { "User", "テストユーザー名" }, // 'Username'を'User'に変更
                    { "CaptureTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    { "Usernames", "ユーザー1, ユーザー2, ユーザー3, 日本語名前" },
                    { "TestKey", "これはテストです" }
                };

                // 保存先を選択
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PNG画像|*.png",
                    Title = "テスト画像の保存先を選択",
                    FileName = "test_metadata.png"
                };

                if (saveDialog.ShowDialog() != DialogResult.OK)
                    return;

                // テスト用の画像を作成
                using (Bitmap bmp = new Bitmap(400, 300))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);

                    // 日本語のテキストを正しく表示
                    using (Font font = new Font("Yu Gothic UI", 20))
                    {
                        g.DrawString("メタデータテスト画像", font, Brushes.Black, new PointF(50, 120));
                    }

                    // 一時ファイルとして保存
                    string tempPath = Path.GetTempFileName() + ".png";
                    bmp.Save(tempPath, ImageFormat.Png);

                    // デバッグ情報を表示
                    StringBuilder logSb = new StringBuilder();
                    logSb.AppendLine("テストデータ:");
                    foreach (var entry in metadata)
                    {
                        logSb.AppendLine($"  {entry.Key}: {entry.Value}");
                    }
                    System.Diagnostics.Debug.WriteLine(logSb.ToString());

                    // PngMetadataManager を使ってメタデータを追加して保存
                    bool success = PngMetadataManager.AddMetadataToPng(tempPath, saveDialog.FileName, metadata);

                    // 一時ファイルの削除
                    try { File.Delete(tempPath); } catch { }

                    if (success)
                    {
                        // メタデータの検証
                        var pngMetadata = PngMetadataManager.ReadMetadataFromPng(saveDialog.FileName);

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("テスト画像作成結果:");
                        sb.AppendLine($"保存先: {saveDialog.FileName}");
                        sb.AppendLine("");
                        sb.AppendLine($"PngMetadataManager (tEXtチャンク): {pngMetadata.Count}項目");
                        foreach (var pair in pngMetadata)
                        {
                            sb.AppendLine($"   {pair.Key}: {pair.Value}");
                        }

                        MessageBox.Show(sb.ToString(), "テスト画像作成成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // UIに表示
                        PngMetaDate_textBox.Text = saveDialog.FileName;
                        DisplayImageAndMetadata(saveDialog.FileName);
                    }
                    else
                    {
                        MessageBox.Show("テスト画像の作成に失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"テスト画像作成エラー: {ex.Message}\n{ex.StackTrace}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LICENSEOpenFolder_button_Click(object sender, EventArgs e)
        {
            try
            {
                // ライセンスフォルダのパスを取得
                string licenseFolderPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "LICENSE");

                // フォルダが存在するか確認
                if (Directory.Exists(licenseFolderPath))
                {
                    // フォルダをエクスプローラーで開く
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = licenseFolderPath,
                        UseShellExecute = true
                    });
                    UpdateStatusInfo("ライセンスフォルダを開きました", $"パス: {licenseFolderPath}");
                }
                else
                {
                    MessageBox.Show(
                        "ライセンスフォルダが見つかりませんでした。\nパス: " + licenseFolderPath,
                        "エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    UpdateStatusInfo("エラー", "ライセンスフォルダが見つかりませんでした");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "ライセンスフォルダを開く際にエラーが発生しました。\n" + ex.Message,
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                UpdateStatusInfo("エラー", "ライセンスフォルダを開けませんでした");
            }
        }

        private void worldFriends_label_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// スタートアップチェックボックスの変更イベントハンドラ
        /// </summary>
        private void startUp_checkBox_CheckedChanged(object? sender, EventArgs e)
        {
            try
            {
                bool isChecked = startup_checkBox.Checked;
                bool success;

                if (isChecked)
                {
                    // スタートアップに登録
                    success = StartupManager.RegisterInStartup();
                    if (success)
                    {
                        _startWithWindows = true;
                        UpdateStatusInfo("設定", "Windowsスタートアップに登録しました");
                    }
                    else
                    {
                        startup_checkBox.Checked = false;
                        _startWithWindows = false;
                        UpdateStatusInfo("エラー", "スタートアップ登録に失敗しました");
                        MessageBox.Show(
                            "Windowsスタートアップへの登録に失敗しました。\n管理者権限で実行するか、別の方法をお試しください。",
                            "スタートアップ登録エラー",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    // スタートアップから解除
                    success = StartupManager.RemoveFromStartup();
                    if (success)
                    {
                        _startWithWindows = false;
                        UpdateStatusInfo("設定", "Windowsスタートアップから解除しました");
                    }
                    else
                    {
                        startup_checkBox.Checked = true;
                        _startWithWindows = true;
                        UpdateStatusInfo("エラー", "スタートアップ解除に失敗しました");
                        MessageBox.Show(
                            "Windowsスタートアップからの解除に失敗しました。\n管理者権限で実行するか、別の方法をお試しください。",
                            "スタートアップ解除エラー",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }

                // 設定を保存
                if (_settings != null)
                {
                    _settings.LauncherSettings.StartWithWindows = _startWithWindows;
                    SettingsManager.SaveSettings(_settings);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusInfo("エラー", "スタートアップ設定エラー");
                MessageBox.Show(
                    $"スタートアップ設定中にエラーが発生しました。\n{ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// フォーム初期化時にスタートアップ状態を確認して反映
        /// </summary>
        private void InitializeStartupSetting()
        {
            // 現在のスタートアップ状態を確認
            _startWithWindows = StartupManager.IsRegisteredInStartup();

            // チェックボックスに反映（イベント発火させないようにする）
            startup_checkBox.CheckedChanged -= startUp_checkBox_CheckedChanged;
            startup_checkBox.Checked = _startWithWindows;
            startup_checkBox.CheckedChanged += startUp_checkBox_CheckedChanged;

            // 設定オブジェクトに反映
            if (_settings != null)
            {
                _settings.LauncherSettings.StartWithWindows = _startWithWindows;
            }
        }

        private void metaData_Click(object sender, EventArgs e)
        {

        }

        private void main_Click(object sender, EventArgs e)
        {

        }

        private void worldFriends_richTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void worldName_richTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void CameraInfo_label_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click_1(object sender, EventArgs e)
        {

        }

        #region VRChat監視とカメラ制御機能

        /// <summary>
        /// VRChat.exe監視タイマーのTick処理
        /// </summary>
        private void VRChatMonitorTimer_Tick(object? sender, EventArgs e)
        {
            bool currentVRChatStatus = IsVRChatRunning();

            if (currentVRChatStatus != _isVRChatRunning)
            {
                _isVRChatRunning = currentVRChatStatus;

                // UIスレッドでの実行を保証
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateVRChatStatusLabel()));
                }
                else
                {
                    UpdateVRChatStatusLabel();
                }

                // VRChat起動時のOSC初期化（新しいロジック）
                if (_isVRChatRunning && !_hasExecutedOscInitialization)
                {
                    // cameraSettomg_checkBoxがtrueかつ設定適用済みの場合のみ実行
                    if ((cameraSettomg_checkBox?.Checked == true) && _cameraSettingsApplied)
                    {
                        Console.WriteLine("[VRChat検知] VRChat起動検知 - 2分後にOSC初期化を実行します");
                        _hasExecutedOscInitialization = true; // アプリセッション中1回だけのフラグ

                        // 2分後にOSC初期化を実行
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromMinutes(2));
                            await ExecuteOscCameraInitialization();
                        });
                    }
                    else
                    {
                        // 条件を満たさない場合はログ出力のみ
                        Console.WriteLine($"[VRChat検知] OSC初期化条件未達成 - カメラ設定有効: {cameraSettomg_checkBox?.Checked}, 設定適用済み: {_cameraSettingsApplied}");
                    }
                }

                // VRChat起動時の従来のカメラ初期化（互換性のため残す）
                if (_isVRChatRunning && !_hasInitializedCamera)
                {
                    Task.Run(async () => await InitializeCameraParametersAsync());
                }
                else if (!_isVRChatRunning)
                {
                    // VRChat終了時にフラグをリセット（従来のフラグのみ、セッション用フラグはリセットしない）
                    _hasInitializedCamera = false;
                }
            }
        }

        /// <summary>
        /// VRChat.exeが起動しているかチェック
        /// </summary>
        private bool IsVRChatRunning()
        {
            try
            {
                Process[] vrchatProcesses = Process.GetProcessesByName("VRChat");
                return vrchatProcesses.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// VRChatステータスラベルの更新
        /// </summary>
        private void UpdateVRChatStatusLabel()
        {
            if (VRCStartStatus_toolStripStatusLabel != null)
            {
                VRCStartStatus_toolStripStatusLabel.Text = _isVRChatRunning ? "VRC: 起動中" : "VRC: 停止中";
            }
        }

        /// <summary>
        /// カメラコントロールの初期化
        /// </summary>
        private void InitializeCameraControls()
        {
            // cameraSettomg_checkBoxのイベントハンドラ設定（CameraSetting_groupBoxの表示制御用）
            if (cameraSettomg_checkBox != null)
            {
                cameraSettomg_checkBox.CheckedChanged += CameraSettomg_CheckBox_CheckedChanged;
            }

            // useCameraチェックボックスのイベントハンドラ設定（useCamera_groupBoxの有効/無効制御用）
           
            // 設定からUIに値を読み込み
            LoadCameraSettingsToUI();

            // 初期状態の設定
            UpdateCameraControlsState();
        }

        /// <summary>
        /// useCameraチェックボックスの変更イベントハンドラ（useCamera_groupBoxの有効/無効制御用）
        /// </summary>
        private void UseCamera_CheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateUseCameraGroupBoxState();
        }

        /// <summary>
        /// cameraSettomg_checkBoxの変更イベントハンドラ（CameraSetting_groupBoxの表示制御用）
        /// </summary>
        private void CameraSettomg_CheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateCameraSettingGroupBoxState();
            UpdateCameraSettingsTabState();
        }

        /// <summary>
        /// カメラモードラジオボタンの変更イベントハンドラ
        /// </summary>
        private void CameraMode_RadioButton_CheckedChanged(object? sender, EventArgs e)
        {
            // 何らかの追加処理が必要な場合はここに記述
        }

        /// <summary>
        /// useCamera_groupBoxの状態を更新（有効/無効制御）
        /// </summary>
        private void UpdateUseCameraGroupBoxState()
        {

        }

        /// <summary>
        /// CameraSetting_groupBoxの状態を更新（表示/非表示制御）
        /// </summary>
        private void UpdateCameraSettingGroupBoxState()
        {
            bool cameraSettingEnabled = cameraSettomg_checkBox?.Checked ?? false;

            // CameraSetting_groupBoxの表示/非表示制御
            if (CameraSetting_groupBox != null)
            {
                CameraSetting_groupBox.Visible = cameraSettingEnabled;
            }
        }

        /// <summary>
        /// CameraSettingsタブの表示/非表示制御
        /// </summary>
        private void UpdateCameraSettingsTabState()
        {
            bool cameraSettingEnabled = cameraSettomg_checkBox?.Checked ?? false;

            // CameraSettingsタブの表示/非表示制御
            if (tabControl != null && CameraSettings != null)
            {
                if (cameraSettingEnabled)
                {
                    // カメラ設定ON: CameraSettingsタブを表示
                    if (!tabControl.TabPages.Contains(CameraSettings))
                    {
                        tabControl.TabPages.Add(CameraSettings);
                    }
                }
                else
                {
                    // カメラ設定OFF: CameraSettingsタブを非表示
                    if (tabControl.TabPages.Contains(CameraSettings))
                    {
                        tabControl.TabPages.Remove(CameraSettings);
                    }
                }
            }
        }

        /// <summary>
        /// カメラコントロールの状態を更新（統合メソッド）
        /// </summary>
        private void UpdateCameraControlsState()
        {
            UpdateUseCameraGroupBoxState();
            UpdateCameraSettingGroupBoxState();
            UpdateCameraSettingsTabState();
        }

        /// <summary>
        /// カメラパラメータの初期化（VRChat起動時）
        /// </summary>
        private async Task InitializeCameraParametersAsync()
        {
            try
            {


                Debug.WriteLine("VRChat起動検知 - カメラパラメータ初期化開始");

                // 少し待機してからパラメータ送信
                await Task.Delay(2000);

                _hasInitializedCamera = true;
                Debug.WriteLine("カメラパラメータ初期化完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"カメラ初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// VirtualLens2パラメータの初期化
        /// </summary>
        private async Task InitializeVirtualLens2ParametersAsync()
        {
            try
            {
                // Enableパラメータを有効化
                _oscParameterSender?.SendCameraEnableParameter(CameraType.VirtualLens2, true);
                await Task.Delay(100);
                _oscParameterSender?.SendCameraEnableParameter(CameraType.VirtualLens2, false);
                await Task.Delay(100);

                // TextBoxから値を取得して送信（0~100を0~1に変換）
                if (VirtualLens2_Aperture_textBox != null && float.TryParse(VirtualLens2_Aperture_textBox.Text, out float aperture))
                {
                    _oscParameterSender?.SendVirtualLens2Parameter("VirtualLens2_Aperture", aperture / 100.0f);
                    await Task.Delay(100);
                }

                if (VirtualLens2_FocalLength_textBox != null && float.TryParse(VirtualLens2_FocalLength_textBox.Text, out float focalLength))
                {
                    _oscParameterSender?.SendVirtualLens2Parameter("VirtualLens2_FocalLength", focalLength / 100.0f);
                    await Task.Delay(100);
                }

                if (VirtualLens2_Exposure_textBox != null && float.TryParse(VirtualLens2_Exposure_textBox.Text, out float exposure))
                {
                    _oscParameterSender?.SendVirtualLens2Parameter("VirtualLens2_Exposure", exposure / 100.0f);
                    await Task.Delay(100);
                }

                Debug.WriteLine("VirtualLens2パラメータ初期化完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VirtualLens2初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Integralパラメータの初期化
        /// </summary>
        private async Task InitializeIntegralParametersAsync()
        {
            try
            {
                // Enableパラメータを有効化
                _oscParameterSender?.SendCameraEnableParameter(CameraType.Integral, true);
                await Task.Delay(100);
                _oscParameterSender?.SendCameraEnableParameter(CameraType.Integral, false);
                await Task.Delay(100);

                // TextBoxから値を取得して送信（0~100を0~1に変換）
                if (Integral_Aperture_textBox != null && float.TryParse(Integral_Aperture_textBox.Text, out float aperture))
                {
                    _oscParameterSender?.SendIntegralParameter("Integral_Aperture", aperture / 100.0f);
                    await Task.Delay(100);
                }

                if (Integral_FocalLength_textBox != null && float.TryParse(Integral_FocalLength_textBox.Text, out float focalLength))
                {
                    _oscParameterSender?.SendIntegralParameter("Integral_FocalLength", focalLength / 100.0f);
                    await Task.Delay(100);
                }

                if (Integral_Exposure_textBox != null && float.TryParse(Integral_Exposure_textBox.Text, out float exposure))
                {
                    _oscParameterSender?.SendIntegralParameter("Integral_Exposure", exposure / 100.0f);
                    await Task.Delay(100);
                }

                // IntegralのShutterSpeedとBokehShapeも初期化
                if (Integral_BokeShape_textBox != null && float.TryParse(Integral_BokeShape_textBox.Text, out float shutterSpeed))
                {
                    _oscParameterSender?.SendIntegralParameter("Integral_ShutterSpeed", shutterSpeed / 100.0f);
                    await Task.Delay(100);
                }

                if (Integral_ShutterSpeed_textBox != null && float.TryParse(Integral_ShutterSpeed_textBox.Text, out float bokehShape))
                {
                    // BokehShapeは整数値として送信
                    _oscParameterSender?.SendIntegralParameter("Integral_BokehShape", (int)(bokehShape / 100.0f * 10)); // 0-10の範囲と仮定
                    await Task.Delay(100);
                }

                Debug.WriteLine("Integralパラメータ初期化完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Integral初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// カメラ設定ボタンのクリック処理
        /// </summary>
        private void CameraSettingApply_button_Click(object? sender, EventArgs e)
        {
            try
            {
                // 現在のTextBoxの値を設定に保存
                SaveCameraSettingsFromUI();

                // 設定ファイルに書き込み
                SettingsManager.SaveSettings(_settings);

                // 設定適用フラグを設定
                _cameraSettingsApplied = true;

                // 成功メッセージ
                UpdateStatusInfo("設定保存完了", "カメラ設定がappsettings.jsonに保存されました");

                MessageBox.Show("カメラ設定を保存しました。", "保存完了", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // VRChatが起動中で、cameraSettomg_checkBoxがtrueの場合、即座にOSC送信を実行
                if (_isVRChatRunning && (cameraSettomg_checkBox?.Checked == true))
                {
                    Console.WriteLine("[カメラ設定] VRChat起動中のため、即座にOSC送信を実行");
                    _ = Task.Run(async () => await ExecuteOscCameraInitialization());
                }
            }
            catch (Exception ex)
            {
                UpdateStatusInfo("設定保存エラー", $"エラー: {ex.Message}");
                MessageBox.Show($"設定の保存に失敗しました。\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// UIの値を設定クラスに保存
        /// </summary>
        private void SaveCameraSettingsFromUI()
        {
            // カメラ設定の有効/無効（cameraSettomg_checkBoxを使用）
            _settings.CameraSettings.Enabled = cameraSettomg_checkBox?.Checked ?? false;

            // VirtualLens2設定
            if (int.TryParse(VirtualLens2_Aperture_textBox?.Text, out int vl2Aperture))
            {
                _settings.CameraSettings.VirtualLens2.Aperture = Math.Max(0, Math.Min(100, vl2Aperture));
            }
            if (int.TryParse(VirtualLens2_FocalLength_textBox?.Text, out int vl2FocalLength))
            {
                _settings.CameraSettings.VirtualLens2.FocalLength = Math.Max(0, Math.Min(100, vl2FocalLength));
            }
            if (int.TryParse(VirtualLens2_Exposure_textBox?.Text, out int vl2Exposure))
            {
                _settings.CameraSettings.VirtualLens2.Exposure = Math.Max(0, Math.Min(100, vl2Exposure));
            }

            // Integral設定
            if (int.TryParse(Integral_Aperture_textBox?.Text, out int intAperture))
            {
                _settings.CameraSettings.Integral.Aperture = Math.Max(0, Math.Min(100, intAperture));
            }
            if (int.TryParse(Integral_FocalLength_textBox?.Text, out int intFocalLength))
            {
                _settings.CameraSettings.Integral.FocalLength = Math.Max(0, Math.Min(100, intFocalLength));
            }
            if (int.TryParse(Integral_Exposure_textBox?.Text, out int intExposure))
            {
                _settings.CameraSettings.Integral.Exposure = Math.Max(0, Math.Min(100, intExposure));
            }
            if (int.TryParse(Integral_BokeShape_textBox?.Text, out int intShutterSpeed))
            {
                _settings.CameraSettings.Integral.ShutterSpeed = Math.Max(0, Math.Min(100, intShutterSpeed));
            }
            if (int.TryParse(Integral_ShutterSpeed_textBox?.Text, out int intBokehShape))
            {
                _settings.CameraSettings.Integral.BokehShape = Math.Max(0, Math.Min(100, intBokehShape));
            }
            UpdateOscDataStoreFromUI();
        }

        /// <summary>
        /// 設定をUIに反映
        /// </summary>
        private void LoadCameraSettingsToUI()
        {
            try
            {
                // 設定が存在しない場合はデフォルト値で初期化
                if (_settings?.CameraSettings == null)
                {
                    Debug.WriteLine("カメラ設定がnullです。デフォルト値で初期化します。");
                    if (_settings == null)
                    {
                        _settings = new AppSettings();
                    }
                    _settings.CameraSettings = new CameraSettings();
                }

                // カメラ設定の有効/無効（cameraSettomg_checkBoxを使用）
                if (cameraSettomg_checkBox != null)
                {
                    cameraSettomg_checkBox.Checked = _settings.CameraSettings.Enabled;
                }

                // VirtualLens2設定をUIに反映
                if (_settings.CameraSettings.VirtualLens2 == null)
                {
                    _settings.CameraSettings.VirtualLens2 = new VirtualLens2Settings();
                }

                if (VirtualLens2_Aperture_textBox != null)
                {
                    VirtualLens2_Aperture_textBox.Text = _settings.CameraSettings.VirtualLens2.Aperture.ToString();
                }
                if (VirtualLens2_FocalLength_textBox != null)
                {
                    VirtualLens2_FocalLength_textBox.Text = _settings.CameraSettings.VirtualLens2.FocalLength.ToString();
                }
                if (VirtualLens2_Exposure_textBox != null)
                {
                    VirtualLens2_Exposure_textBox.Text = _settings.CameraSettings.VirtualLens2.Exposure.ToString();
                }

                // Integral設定をUIに反映
                if (_settings.CameraSettings.Integral == null)
                {
                    _settings.CameraSettings.Integral = new IntegralSettings();
                }

                if (Integral_Aperture_textBox != null)
                {
                    Integral_Aperture_textBox.Text = _settings.CameraSettings.Integral.Aperture.ToString();
                }
                if (Integral_FocalLength_textBox != null)
                {
                    Integral_FocalLength_textBox.Text = _settings.CameraSettings.Integral.FocalLength.ToString();
                }
                if (Integral_Exposure_textBox != null)
                {
                    Integral_Exposure_textBox.Text = _settings.CameraSettings.Integral.Exposure.ToString();
                }
                if (Integral_BokeShape_textBox != null)
                {
                    Integral_BokeShape_textBox.Text = _settings.CameraSettings.Integral.ShutterSpeed.ToString();
                }
                if (Integral_ShutterSpeed_textBox != null)
                {
                    Integral_ShutterSpeed_textBox.Text = _settings.CameraSettings.Integral.BokehShape.ToString();
                }

                Debug.WriteLine("カメラ設定をUIに読み込み完了");
                
                // UIの値でOscDataStoreを初期化
                UpdateOscDataStoreFromUI();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"カメラ設定UI読み込みエラー: {ex.Message}");
                // エラーが発生した場合はデフォルト値を設定
                SetDefaultCameraValues();
            }
        }

        /// <summary>
        /// デフォルトのカメラ値をUIに設定
        /// </summary>
        private void SetDefaultCameraValues()
        {
            try
            {
                if (VirtualLens2_Aperture_textBox != null)
                    VirtualLens2_Aperture_textBox.Text = "50";
                if (VirtualLens2_FocalLength_textBox != null)
                    VirtualLens2_FocalLength_textBox.Text = "50";
                if (VirtualLens2_Exposure_textBox != null)
                    VirtualLens2_Exposure_textBox.Text = "50";

                if (Integral_Aperture_textBox != null)
                    Integral_Aperture_textBox.Text = "50";
                if (Integral_FocalLength_textBox != null)
                    Integral_FocalLength_textBox.Text = "50";
                if (Integral_Exposure_textBox != null)
                    Integral_Exposure_textBox.Text = "50";
                if (Integral_BokeShape_textBox != null)
                    Integral_BokeShape_textBox.Text = "50";
                if (Integral_ShutterSpeed_textBox != null)
                    Integral_ShutterSpeed_textBox.Text = "50";

                Debug.WriteLine("デフォルトカメラ値を設定しました");
                
                // デフォルト値でOscDataStoreも更新
                UpdateOscDataStoreFromUI();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"デフォルト値設定エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// UIのTextBox値からOscDataStoreを更新
        /// </summary>
        private void UpdateOscDataStoreFromUI()
        {
            try
            {
                if (_oscDataStore == null)
                {
                    Debug.WriteLine("OscDataStoreが初期化されていません");
                    return;
                }

                // VirtualLens2設定
                if (VirtualLens2_Aperture_textBox != null && float.TryParse(VirtualLens2_Aperture_textBox.Text, out float vl2Aperture))
                {
                    _oscDataStore.SetParameterValue("VirtualLens2_Aperture", vl2Aperture);
                }
                if (VirtualLens2_FocalLength_textBox != null && float.TryParse(VirtualLens2_FocalLength_textBox.Text, out float vl2FocalLength))
                {
                    _oscDataStore.SetParameterValue("VirtualLens2_FocalLength", vl2FocalLength);
                }
                if (VirtualLens2_Exposure_textBox != null && float.TryParse(VirtualLens2_Exposure_textBox.Text, out float vl2Exposure))
                {
                    _oscDataStore.SetParameterValue("VirtualLens2_Exposure", vl2Exposure);
                }

                // Integral設定
                if (Integral_Aperture_textBox != null && float.TryParse(Integral_Aperture_textBox.Text, out float intAperture))
                {
                    _oscDataStore.SetParameterValue("Integral_Aperture", intAperture);
                }
                if (Integral_FocalLength_textBox != null && float.TryParse(Integral_FocalLength_textBox.Text, out float intFocalLength))
                {
                    _oscDataStore.SetParameterValue("Integral_FocalLength", intFocalLength);
                }
                if (Integral_Exposure_textBox != null && float.TryParse(Integral_Exposure_textBox.Text, out float intExposure))
                {
                    _oscDataStore.SetParameterValue("Integral_Exposure", intExposure);
                }
                if (Integral_BokeShape_textBox != null && float.TryParse(Integral_BokeShape_textBox.Text, out float intBokeShape))
                {
                    _oscDataStore.SetParameterValue("Integral_BokeShape", intBokeShape);
                }
                if (Integral_ShutterSpeed_textBox != null && float.TryParse(Integral_ShutterSpeed_textBox.Text, out float intShutterSpeed))
                {
                    _oscDataStore.SetParameterValue("Integral_ShutterSpeed", intShutterSpeed);
                }

                Debug.WriteLine("UIからOscDataStoreを更新しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OscDataStore更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// OSCカメラ初期化処理（2分待機後またはVRChat起動中の設定適用時に実行）
        /// </summary>
        private async Task ExecuteOscCameraInitialization()
        {
            try
            {
                if (_oscParameterSender == null)
                {
                    Console.WriteLine("[OSCエラー] OSCParameterSenderが初期化されていません");
                    return;
                }

                Console.WriteLine("[OSC初期化] カメラパラメータ初期化を開始します");
                await _oscParameterSender.InitializeCameraParameters();
                Console.WriteLine("[OSC初期化] カメラパラメータ初期化が完了しました");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OSCエラー] OSC初期化エラー: {ex.Message}");
                Debug.WriteLine($"OSC初期化エラー: {ex.StackTrace}");
            }
        }

        #endregion

        private void OSCStatus_toolStripStatusLabel_Click(object sender, EventArgs e)
        {

        }

        private void startingState_toolStripStatusLabel_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click_2(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label4_Click_3(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void VirtualLens2_groupBox_Enter(object sender, EventArgs e)
        {

        }

        private void OSCStatus_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// OSCメッセージ受信イベントハンドラ（開発モード用）
        /// </summary>
        private void OnOscMessageReceived(string address, object? value)
        {
            if (devMode_checkBox?.Checked == true)
            {
                if (InvokeRequired)
                {
                    Invoke((Action)(() =>
                    {
                        if (label5 != null)
                        {
                            label5.Text = $"Last OSC: {DateTime.Now:HH:mm:ss.fff} - {address}";
                            label5.Visible = true;
                        }
                    }));
                }
                else
                {
                    if (label5 != null)
                    {
                        label5.Text = $"Last OSC: {DateTime.Now:HH:mm:ss.fff} - {address}";
                        label5.Visible = true;
                    }
                }
            }
        }

        /// <summary>
        /// OSCログメッセージ受信イベントハンドラ（ログモード用）
        /// 重くならないよう最大1000行までに制限
        /// </summary>
        private void OnOscLogMessageReceived(string logMessage)
        {
            if (OSCLog_checkBox?.Checked == true)
            {
                if (InvokeRequired)
                {
                    Invoke((Action)(() =>
                    {
                        if (OSCLog_richTextBox != null)
                        {
                            OSCLog_richTextBox.AppendText(logMessage + Environment.NewLine);

                            // 行数制限（1000行を超えたら古い行を削除）
                            if (OSCLog_richTextBox.Lines.Length > 1000)
                            {
                                var lines = OSCLog_richTextBox.Lines;
                                var newLines = lines.Skip(200).ToArray(); // 上から200行削除
                                OSCLog_richTextBox.Lines = newLines;
                            }

                            // 自動スクロール
                            OSCLog_richTextBox.SelectionStart = OSCLog_richTextBox.Text.Length;
                            OSCLog_richTextBox.ScrollToCaret();
                        }
                    }));
                }
            }
        }

        private void CameraSetting_groupBox_Enter(object sender, EventArgs e)
        {

        }
    }

    public class SystemTrayIcon
    {
        private NotifyIcon _notifyIcon = null!;
        private ContextMenuStrip _contextMenu = null!;
        private VSA_launcher _mainForm;

        public SystemTrayIcon(VSA_launcher mainForm, NotifyIcon notifyIcon, ContextMenuStrip contextMenu)
        {
            _mainForm = mainForm;
            _notifyIcon = notifyIcon;
            _contextMenu = contextMenu;
            
            // NotifyIconにコンテキストメニューを設定
            _notifyIcon.ContextMenuStrip = _contextMenu;
            
            // イベントハンドラの設定
            _notifyIcon.DoubleClick += (sender, e) => ShowSettings();
            
            // メニューの各項目を調べて名前で見つける - より安全な方法
            foreach (ToolStripItem item in _contextMenu.Items)
            {
                if (item.Text == "設定")
                {
                    item.Click += (sender, e) => ShowSettings();
                }
                else if (item.Text == "終了")
                {
                    item.Click += (sender, e) => Application.Exit();
                }
            }
            
            // モニタリング処理を開始
            StartMainAppMonitoring();
        }
        public void LaunchMainApplication()
        {
            _mainForm.LaunchMainApplication();
        }

        private void ShowSettings()
        {
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        }

        private void StartMainAppMonitoring()
        {
            // メインアプリケーションの状態を監視するコード
            // 現在は実装されていないようです
        }

        public void Dispose()
        {
            // NotifyIconはフォームが所有しているので、ここでは何もしない
        }
    }
}