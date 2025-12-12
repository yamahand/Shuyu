# Shuyu — プロジェクト解析レポート

作成日: 2025-12-12

このドキュメントはワークスペース内の `Shuyu` プロジェクトのコードを読み取り、アーキテクチャ、主要コンポーネント、注意点、改善提案をまとめた解析レポートです。

---

## 概要
- プロジェクト名: Shuyu
- 種別: Windows デスクトップアプリケーション（WPF / .NET 9）
- 目的: 軽量スクリーンキャプチャ（領域選択、ピン留め、ホットキー、トレイ常駐）

## 技術スタック
- フレームワーク: `.NET 9 (net9.0-windows)`
- UI: WPF（`<UseWPF>true</UseWPF>`）、一部で WinForms（`NotifyIcon` 等）を使用
- 主要パッケージ: `System.Drawing.Common (6.0.0)`
- 対応ランタイム: `win-x64`, `win-arm64` を想定

## リポジトリ内の重要ファイル
- `Shuyu.sln` — ソリューション
- `Shuyu/Shuyu.csproj` — プロジェクト設定（WPF、リソース、パッケージ）
- `Shuyu/App.xaml(.cs)` — アプリケーションエントリ
- `Shuyu/MainWindow.xaml.cs` — メインウィンドウ（通常非表示、トレイで動作）
- `Shuyu/Service/TrayService.cs` — トレイ操作とメニュー管理
- `Shuyu/Service/HotkeyManager.cs` — ホットキー登録と低レベルキーボードフック
- `Shuyu/Service/AsyncScreenCaptureService.cs` — 画面キャプチャ処理（System.Drawing を使用）
- `Shuyu/Service/CoordinateTransformation.cs` — DPI / 座標変換ユーティリティ
- `Shuyu/Service/UserSettingsStore.cs` — 設定の永続化
- `Shuyu/Resources/Strings.resx` & `Strings.Designer.cs` — ローカライズ文字列

## アーキテクチャ概要
- アプリは WPF で UI を構成し、メインウィンドウは通常非表示で `TrayService` がシステムトレイ操作を提供します。
- ホットキーは二通りのモード:
  - `RegisterHotKey`（システムホットキー登録）
  - 低レベルキーボードフック（`WH_KEYBOARD_LL` を使い PrintScreen + Shift 抑止）
- キャプチャは `AsyncScreenCaptureService` が `System.Drawing.Bitmap` を生成して `BitmapSource` に変換して配布します。
- DPI やマルチモニターは `CoordinateTransformation` ユーティリティで扱われています。

## 解析した主要箇所と所見

### 1) Hotkey / 低レベルフック (`HotkeyManager`)
- 機能: Shift+PrintScreen をトリガーとしてキャプチャを発生させる。低レベルフックでは PrintScreen を抑止してアプリが優先して処理します。
- 実装のポイント:
  - `SetWindowsHookEx(WH_KEYBOARD_LL, ...)` を用い、`HookCallback` で `GetAsyncKeyState` による Shift 押下判定を行っている。
  - フック用デリゲート (`LowLevelKeyboardProc`) をクラスフィールドで保持して GC から保護している点は正しい。
  - フック導入・解除 (`InstallLowLevelHook` / `UninstallLowLevelHook`) にロックをかけているためスレッド安全性は保たれている。
- 注意点 / リスク:
  - 低レベルフックはグローバルにキーボード入力を監視しているため、他プロセスに影響する可能性がある（パフォーマンス・安定性・セキュリティ）。
  - `GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty)` の呼び出しは PE ベース名取得に依存するため、ネイティブモジュールハンドル取得が意図通りか確認が必要。公式の例では `GetModuleHandle(IntPtr.Zero)` / `GetModuleHandle(null)` を使うケースもある。
  - `HookCallback` 内で `Marshal.ReadInt32(lParam)` を使って VK コードを読み取っているが、低レベルキーボードフックの lParam は `KBDLLHOOKSTRUCT*` であり、先頭に vkCode がある構造体サイズを前提にしている点は一般的だが、構造体のレイアウトに依存する。
  - 例外はキャッチしているが、フックが失敗したり途中で例外が発生した場合のフォールバック（RegisterHotKey に戻す等）の検討を推奨。

推奨対応:
- フックがインストールできなかった場合の明示的なエラー通知と安全なフォールバックを実装する。
- `GetModuleHandle` に `null` を渡す、または `Process.GetCurrentProcess().MainModule` が null の可能性を考慮した安全処理を追加する。
- 低レベルフックを使う理由と影響を設定画面の説明に明記する（既に注意文あり）。

### 2) DPI / マルチディスプレイ (`CoordinateTransformation`)
- 実装:
  - `GetDpiForMonitor`（`shcore.dll`）を用いてモニター単位の DPI を取得する。
  - `PresentationSource.FromVisual(window)` から `TransformToDevice` を読み DPI を計算するメソッドもある。
  - 仮想スクリーン情報は `System.Windows.Forms.SystemInformation.VirtualScreen` を使って取得している。
- 所見:
  - 実装は典型的で、モニター単位・ウィンドウ単位双方の取得パスが用意されている点は良い。
  - DPI を利用してスクリーン座標 ↔ DPI 独立ピクセル (DIP) の変換を提供しているため、選択領域やピン留め配置でのスケーリングが比較的安全に行える。
- 注意点:
  - `GetDpiForMonitor` は Windows 8.1 以降の API（`shcore.dll`）であり、呼び出し時の互換性とエラーコードの扱いに注意。現在は .NET 9 で Windows 専用のため問題は小さいが、例外ハンドリングを堅牢にしておく。

### 3) 画面キャプチャ (`AsyncScreenCaptureService`) と `System.Drawing` の使用
- 実装:
  - `Bitmap` を作成し `Graphics.CopyFromScreen` で領域をコピー。
  - `Bitmap.LockBits` → `BitmapSource.Create` で WPF 用の `BitmapSource` に変換し `Freeze()` して返す。
- 所見 / 注意点:
  - `System.Drawing.Common` の使用は Windows 環境では動作するが、.NET 6 以降でクロスプラットフォーム互換性が制限されている点に注意（ただし本プロジェクトは Windows 専用）。
  - 高 DPI 環境では `CopyFromScreen` に渡す座標が DPI スケール済みかどうかを厳密に確認する必要がある（`CoordinateTransformation` と一貫性を持たせる）。
  - `Bitmap` / `Graphics` の確実な破棄はできているが、例外パスでの破棄が漏れないようにさらに注意（現在は try/finally で Dispose を呼んでいる）。

代替案:
- 将来的な拡張やパフォーマンス改善のために、Windows 固有の `Graphics.CopyFromScreen` の代わりに DWM/DirectX 経由や、`Windows.Graphics.Capture` (Windows 10/11 API) の使用を検討すると低レイテンシで高品質なキャプチャが可能。
- クロスプラットフォームを目指すのであれば `ImageSharp` 等のマネージドライブラリへ段階的に移行を検討。

### 4) 設定永続化 / セキュリティ (`UserSettingsStore`, `SecurityHelper`)
- 実装:
  - 設定は `%APPDATA%\\Shuyu\\settings.json` に書き込み/読み込み。
  - `SecurityHelper` にパス検証、JSON 検証、読み書きのラップ関数が用意されている。
- 所見:
  - 入出力時にパス検証・JSON 検証・例外ハンドリングが実装されており、悪意のあるパスや壊れたファイルに対して慎重に扱われている点は良い。
  - `SafeWriteSettingsFile` は一時ファイル書き込み→ファイル移動のパターンでアトミックに近い操作を行っている。

### 5) トレイ・UI 周り (`TrayService`, `PinnedWindowManager`)
- `TrayService` は `NotifyIcon` を使ってトレイメニューを構築、ホットキーイベントによりキャプチャを呼び出す。
- `PinnedWindowManager` は UI スレッドでウィンドウを生成・管理し、スレッド安全なリストを保持している。

所見:
- トレイアイコンの読み込みで埋め込みリソース名が `Shuyu.Resources.Icons.tray.ico` になっているため、実際のリソースの埋め込みパスと一致しているか確認が必要（ビルド設定では `EmbeddedResource` に指定されている）。
- アプリ終了時に `TrayService.Dispose()` が呼ばれていることを確認（`MainWindow.ExitApplication` では `Application.Current.Shutdown()` を呼んでおり、Dispose が呼ばれるライフサイクルに注意）。

## 追加した CI ワークフロー
- `.github/workflows/dotnet-win.yml` を追加しました。内容:
  - `dotnet restore` / `dotnet build` を実行
  - `dotnet publish` を `win-x64` と `win-arm64` 向けに実行して `artifacts/` に出力
  - 出力アーティファクトを GitHub Actions に保存

CI を追加した目的: 自動ビルドでターゲットプラットフォーム向けのビルド確認を行いやすくするためです。

## 潜在的な問題点のまとめ（優先度順）
1. 低レベルキーボードフックのエラー・フォールバック未整備（高）
2. `GetModuleHandle` の呼び出しにおけるモジュール名取得の脆弱性（中）
3. DPI 変換の適用箇所の不整合（中）
4. `System.Drawing.Common` の将来的互換性（低→中）
5. トレイアイコンリソースの埋め込みパス不一致の可能性（低）

## 推奨対応（短期 / 中期 / 長期）
- 短期 (即時対応)
  - フックインストール失敗時のログ出力と RegisterHotKey へのフォールバックを実装する。
  - `GetModuleHandle` 呼び出しを堅牢に（null ケース、例外ハンドリング）。
  - CI（既に追加済み）を通してビルドが通ることを確認する。

- 中期
  - 低レベルフック使用時の UI 上での注意文・確認を強化する（設定画面の説明を明示的に）。
  - DPI 関連の結合テスト（複数モニター、異なるスケール）を手順化してテスト実行する。

- 長期
  - キャプチャ実装の選択肢（`Windows.Graphics.Capture` 等）を検討してパフォーマンス/品質向上を図る。
  - 重大な将来互換性を見越して `System.Drawing.Common` 依存を段階的に抽象化する。

## 参照ファイル（本解析で確認した主なファイル）
- `Shuyu/Shuyu.csproj`
- `Shuyu/App.xaml.cs`
- `Shuyu/MainWindow.xaml.cs`
- `Shuyu/Service/HotkeyManager.cs`
- `Shuyu/Service/TrayService.cs`
- `Shuyu/Service/AsyncScreenCaptureService.cs`
- `Shuyu/Service/CoordinateTransformation.cs`
- `Shuyu/Service/UserSettingsStore.cs`
- `Shuyu/Service/SecurityHelper.cs`
- `Shuyu/Resources/Strings.Designer.cs`

---

## 次の推奨アクション（私が代行できます）
1. 低レベルフック部分のコード修正パッチ作成（例: `GetModuleHandle` の安全化、フォールバック処理）
2. DPI 複数画面での手動テスト手順書作成 or 自動テストの雛形作成
3. `System.Drawing` 使用箇所の抽出と、代替ライブラリへの移行プラン提案

どれを優先しますか？またはこのレポートをさらに詳しく（箇所別にコード修正提案を含める等）作成しましょうか。
