# AI Tetris (Unity)

Unity で実装されたテトリスゲームです。  
`TetrisGame` にゲームロジックを集約し、`TetrisRenderer` で描画を担当する構成になっており、ロジックと表示が分離されています。

## 概要

このリポジトリは、以下を備えたシンプルなテトリス実装です。

- 10x20 グリッドの基本ルール
- 7種1巡（7-bag）方式のミノ生成
- SRS (Super Rotation System) ベースの回転と Wall Kick
- Hold 機能
- 複数枠の Next 表示（シーン上の `NextArea1`, `NextArea2`, ... を自動検出）
- ライン消去、ゲームオーバー判定、ロック遅延
- Unity Test Framework による Edit Mode テスト

## 技術スタック

- **Unity**: `6000.1.5f1`
- **言語**: C#
- **主なパッケージ**:
  - `com.unity.ugui`
  - `com.unity.test-framework`

## 主要コンポーネント

- `Assets/Scripts/TetrisGame.cs`
  - 純粋なゲームロジック（盤面、移動、回転、落下、固定、消去、ゲーム状態）
- `Assets/Scripts/TetrisRenderer.cs`
  - UI `Image` の生成/更新/破棄による描画
- `Assets/Scripts/GameFieldController.cs`
  - 入力処理とゲームループ制御（DAS 含む）
- `Assets/Scripts/TetrisConfig.cs`
  - 定数・ミノ形状・色定義
- `Assets/Tests/TetrisGameTests.cs`
  - ロジック中心の Edit Mode テスト

## 操作方法（デフォルト）

- `A` / `D`: 左右移動
- `S`: ソフトドロップ
- `W`: ハードドロップ
- `Q` / `E`: 回転（反時計 / 時計）
- `H`: Hold

> `GameFieldController` の `debugMode` を有効にすると、`W` は上移動（デバッグ）に切り替わり、`C`（ミノ生成）や `F`（即固定）が使えます。

## セットアップと実行

1. Unity Hub で本リポジトリを開く
2. Unity `6000.1.5f1` でプロジェクトをロード
3. `Assets/Scenes/SampleScene.unity` を開く
4. Play して `GameStartButton` から開始

## テスト

Unity Test Runner から Edit Mode テストを実行します。

- 対象: `Assets/Tests/TetrisGameTests.cs`
- 主な検証項目:
  - 開始/再開始
  - 移動と境界判定
  - 回転
  - ハードドロップ
  - Tick による自動落下
  - Hold
  - ライン消去
  - ゲームオーバー

## ディレクトリ構成（抜粋）

```text
Assets/
  Scenes/
    SampleScene.unity
  Scripts/
    GameFieldController.cs
    TetrisConfig.cs
    TetrisGame.cs
    TetrisRenderer.cs
  Tests/
    TetrisGameTests.cs
Packages/
ProjectSettings/
```

## 補足

現状はシンプルな実装にフォーカスしており、スコア管理・レベル制・BGM/SE などは未実装です。必要に応じて `TetrisGame` を中心に拡張しやすい構成です。
