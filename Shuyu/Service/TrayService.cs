using System;
using System.Windows;
using Application = System.Windows.Application;
using Shuyu; // SettingsWindow参照用
using System.Windows.Forms; // NotifyIcon用
using System.Drawing;       // Icon用
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Diagnostics;
using Shuyu.Service; // 追加: PinnedWindowManager 参照

/// <summary>
/// トレイアイコンと関連する操作（キャプチャ開始、設定表示、終了）を管理するサービスです。
/// </summary>
public class TrayService : IDisposable
{
    /// <summary>
    /// システムトレイに表示するアイコンオブジェクト
    /// </summary>
    private NotifyIcon? _trayIcon;
    
    /// <summary>
    /// ホットキー管理を担当するマネージャー（RegisterHotKey と低レベルフック）
    /// </summary>
    private HotkeyManager? _hotkeyManager;
    
    /// <summary>
    /// キャプチャ要求時に呼び出されるコールバック関数
    /// </summary>
    private readonly Action _onCaptureRequested;
    
    /// <summary>
    /// 設定表示要求時に呼び出されるコールバック関数（現在未使用）
    /// </summary>
    private readonly Action _onSettingsRequested;
    
    /// <summary>
    /// 設定ウィンドウのインスタンス（再利用のためキャッシュ）
    /// </summary>
    private SettingsWindow? _settingsWindow;
    
    /// <summary>
    /// アプリケーション終了要求時に呼び出されるコールバック関数
    /// </summary>
    private readonly Action _onExitRequested;
    
    /// <summary>
    /// 現在のホットキーモード（true=低レベルフック、false=RegisterHotKey）
    /// </summary>
    private bool _useLowLevelHook = false;

    /// <summary>
    /// TrayService の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="onCapture">キャプチャ要求時に呼び出されるコールバック。</param>
    /// <param name="onSettings">設定表示要求時に呼び出されるコールバック（未使用）。</param>
    /// <param name="onExit">終了要求時に呼び出されるコールバック。</param>
    public TrayService(Action onCapture, Action onSettings, Action onExit)
    {
        // コールバック関数を保存
        _onCaptureRequested = onCapture;
        _onSettingsRequested = onSettings;
        _onExitRequested = onExit;

        // トレイアイコンとコンテキストメニューを初期化
        InitializeTrayIcon();
        
        // HotkeyManager を作成してホットキー機能を初期化
        _hotkeyManager = new HotkeyManager();
        
        // 保存された設定を読み込み
        var settings = UserSettingsStore.Load();
        _useLowLevelHook = settings.useLowLevelHook;
        
        // 読み込んだ設定に応じてホットキーモードを適用
        _hotkeyManager.ApplyUseLowLevelHook(_useLowLevelHook, true);
        
        // ホットキー押下時にキャプチャコールバックを呼び出すよう設定
        _hotkeyManager.HotkeyPressed += () => _onCaptureRequested?.Invoke();
    }

    /// <summary>
    /// トレイアイコンを初期化して表示します。
    /// </summary>
    private void InitializeTrayIcon()
    {
        // NotifyIcon オブジェクトを作成して設定
        _trayIcon = new NotifyIcon
        {
            Icon = LoadIconFromResource(),      // 埋め込みリソースからアイコンを読み込み
            Visible = true,                     // トレイに表示
            Text = "Shuyu",                     // ツールチップテキスト
            ContextMenuStrip = CreateContextMenu()  // 右クリックメニューを設定
        };
    }

    /// <summary>
    /// 埋め込みリソースからアイコンを読み込みます。
    /// </summary>
    /// <returns>読み込まれた <see cref="Icon"/> オブジェクト。</returns>
    private Icon LoadIconFromResource()
    {
        // 現在実行中のアセンブリを取得
        var assembly = Assembly.GetExecutingAssembly();
        
        // 埋め込みリソースからアイコンファイルのストリームを取得
        using var stream = assembly.GetManifestResourceStream("Shuyu.Resources.Icons.tray.ico");
        
        // リソースが見つからない場合はエラー
        if (stream == null)
            throw new InvalidOperationException("アイコンリソースが見つかりません。");
            
        // ストリームからアイコンオブジェクトを作成して返す
        return new Icon(stream);
    }

    /// <summary>
    /// トレイのコンテキストメニューを作成します。
    /// </summary>
    /// <returns>作成された <see cref="ContextMenuStrip"/> インスタンス。</returns>
    private ContextMenuStrip CreateContextMenu()
    {
        // コンテキストメニューを作成
        var menu = new ContextMenuStrip();

        // メニュー項目を追加：キャプチャ開始（ホットキー表示付き）
        menu.Items.Add("キャプチャ開始 (Shift+PrintScreen)", null, (s, e) => _onCaptureRequested?.Invoke());
        
        // 区切り線を追加
        menu.Items.Add(new ToolStripSeparator());

        // ピン留め関連
        menu.Items.Add("ピン留めをすべて削除", null, (s, e) => PinnedWindowManager.CloseAll());
        menu.Items.Add("ピン留めの表示/非表示を切り替え", null, (s, e) => PinnedWindowManager.ToggleAllVisibility());
        
        // 区切り線を追加
        menu.Items.Add(new ToolStripSeparator());
        
        // メニュー項目を追加：設定画面表示
        menu.Items.Add("設定", null, (s, e) => ShowSettingsWindow());
        
        // メニュー項目を追加：バージョン情報表示
        menu.Items.Add("バージョン情報", null, (s, e) => ShowAbout());
        
        // 区切り線を追加
        menu.Items.Add(new ToolStripSeparator());
        
        // メニュー項目を追加：アプリケーション終了
        menu.Items.Add("終了", null, (s, e) => _onExitRequested?.Invoke());

        return menu;
    }

    // ... existing code for hotkey handled by HotkeyManager ...

    /// <summary>
    /// 設定ウィンドウを表示し、ユーザーが変更した設定を適用・永続化します。
    /// </summary>
    private void ShowSettingsWindow()
    {
        // 設定ウィンドウが未作成またはアンロード状態の場合は新規作成
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
        }

        // 現在の設定値を設定ウィンドウに反映
        _settingsWindow.useLowLevelHook = _useLowLevelHook;

        // メインウィンドウを親として設定（存在する場合）
        try
        {
            _settingsWindow.Owner = Application.Current?.MainWindow;
        }
        catch (InvalidOperationException e)
        {
            // MainWindow が存在しない場合は無視
            LogService.LogException(e, "TrayService: Unable to set SettingsWindow owner");
        }

        // モーダルダイアログとして表示
        var res = _settingsWindow.ShowDialog();
        
        // OKボタンが押された場合のみ設定を適用
        if (res == true)
        {
            // 設定ウィンドウから新しい設定値を取得
            var wantHook = _settingsWindow.useLowLevelHook;
            
            // 設定に応じてホットキーモードを切り替え
            ApplyHookSetting(wantHook);

            // 変更された設定をファイルに永続化
            var s = new UserSettings { useLowLevelHook = wantHook };
            UserSettingsStore.Save(s);
        }
    }

    // 設定を適用: RegisterHotKey と低レベルフックを切り替える
    /// <summary>
    /// RegisterHotKey と低レベルキーボードフックのいずれかを有効/無効に切り替えます。
    /// </summary>
    /// <param name="wantHook">低レベルフックを使用する場合は true。</param>
    private void ApplyHookSetting(bool wantHook)
    {
        // 既に要求された状態と同じ場合は何もしない
        if (wantHook == _useLowLevelHook) return;

        if (wantHook)
        {
            // 低レベルフックモードに切り替え：HotkeyManager に低レベルフック使用を指示
            _hotkeyManager?.ApplyUseLowLevelHook(true, false);
            _useLowLevelHook = true;
        }
        else
        {
            // RegisterHotkeyモードに切り替え：HotkeyManager に標準ホットキー使用を指示
            _hotkeyManager?.ApplyUseLowLevelHook(false, false);
            _useLowLevelHook = false;
        }
    }

    /// <summary>
    /// バージョン情報ダイアログを表示します。
    /// </summary>
    private void ShowAbout()
    {
        // Windows Forms のメッセージボックスでバージョン情報を表示
        System.Windows.Forms.MessageBox.Show(
            "Shuyu v1.0\nスクリーンキャプチャツール",    // メッセージ本文
            "バージョン情報",                          // タイトル
            MessageBoxButtons.OK,                      // ボタン：OKのみ
            MessageBoxIcon.Information                 // アイコン：情報アイコン
        );
    }

    /// <summary>
    /// 使用中のリソースを解放します。
    /// </summary>
    public void Dispose()
    {
        // HotkeyManager のリソース（フック、ホットキー登録）を解放
        _hotkeyManager?.Dispose();
        
        // トレイアイコンを削除してリソースを解放
        _trayIcon?.Dispose();
    }

    // HotkeyManager が責務を持つため、ここに P/Invoke は不要になった
}