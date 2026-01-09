# Shuyu

Shuyu は WPF と .NET 10 で書かれた軽量なスクリーンキャプチャツールです。

## 主な機能

- オーバーレイによる領域選択と高品質なキャプチャ
- システムトレイからの操作と設定アクセス
- キャプチャ画像のピン留め（常に手前に固定）
- グローバルホットキーによる迅速なキャプチャ
- マルチモニタおよび個々の DPI 環境に対応
- デバッグ用ログ出力

## 要件

- Windows 10 以降
- .NET 10 ランタイム（ソースからビルドする場合は SDK）

## すぐに使う

1. Releases から配布パッケージをダウンロードして展開します。
2. `Shuyu.exe` を実行するとシステムトレイに常駐します。

トレイアイコンを右クリックしてキャプチャ、設定、終了などを選択します。キャプチャ中はドラッグで領域を選択、右クリックでキャンセルします。

## ソースからビルド

```powershell
git clone https://github.com/yourusername/Shuyu.git
cd Shuyu
dotnet build --configuration Release
```

開発には Visual Studio 2022 以降で [Shuyu.slnx](Shuyu.slnx) を開くのが便利です。

## DPI テスト補助スクリプト

`scripts\dpi_test.ps1` は、複数モニタと異なる DPI 設定下でのキャプチャ結果を検証するための PowerShell スクリプトです。`GetDpiForMonitor` や `GetDpiForWindow`、`GetDeviceCaps`、GDI+ の DPI 情報などを順に試して期待されるピクセルサイズを算出し、実際にキャプチャした画像サイズと比較します。

リポジトリルートから実行してください（デスクトップ環境が必要です）：

```powershell
pwsh -File .\scripts\dpi_test.ps1 -OutDir .\artifacts\dpi-tests -Verbose
```

出力は `artifacts\dpi-tests` に PNG ファイルとして保存され、ログで期待サイズと実際のサイズが確認できます。

注意事項：スクリプトは GUI セッションを必要とするため、ヘッドレスな CI ホストでは動作しません。CI で自動化する場合は Windows のデスクトップ環境があるランナーや VM を利用し、生成された PNG をワークフローのアーティファクトとしてアップロードしてください。

## アーキテクチャ（概要）

- `CaptureOverlayWindow`: キャプチャ領域選択の UI
- `AsyncScreenCaptureService`: 非同期での画面キャプチャ処理
- `TrayService`: トレイアイコンとコンテキストメニューの管理
- `PinnedWindowManager`: ピン留めウィンドウの生成／管理
- `HotkeyManager`: グローバルホットキーの登録と処理

非同期処理とログにより、特に DPI やマルチモニタ環境における問題の診断を助けます。

## 貢献

- 大きな変更は先に issue で相談してください。
- プルリクエストは歓迎します。既存スタイルに合わせ、変更は小さく分けてください。

詳細は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## ライセンス

MIT ライセンスの下で公開しています — 詳細は [LICENSE](LICENSE) を参照してください。

## サポート

問題や質問がある場合は GitHub の issue を開いてください。

