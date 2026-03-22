using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TetrisGame の基本操作を検証する Edit Mode テスト。
/// Unity Test Framework の NUnit を使用する。
///
/// テスト対象:
///   StartGame  – 初期化・イベント発火
///   MoveMino   – 左右・下移動、壁への衝突
///   RotateMino – 時計回り・反時計回り
///   HardDrop   – 即時落下・グリッドへの固定
///   Tick       – 自動落下タイマー
///   HoldMino   – 保存・交換・同ターン二重使用禁止
///   ClearLines – 行消去
///   GameOver   – ゲームオーバー判定
/// </summary>
public class TetrisGameTests
{
    // ── フィールド ─────────────────────────────────────────────────
    private GameObject    _fieldObj;
    private TetrisRenderer _renderer;
    private TetrisGame    _game;

    private static readonly BindingFlags NonPublic =
        BindingFlags.NonPublic | BindingFlags.Instance;

    // ── セットアップ / ティアダウン ────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // フィールド用の空 GameObject のみ用意（TetrisRenderer は Hold/Next を null 許容）
        _fieldObj = new GameObject("TestField");
        _renderer = new TetrisRenderer(_fieldObj.transform, holdArea: null);
        _game     = new TetrisGame(
            _renderer,
            TetrisConfig.DEFAULT_FALL_INTERVAL,
            TetrisConfig.DEFAULT_LOCK_DELAY);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_fieldObj);
    }

    // ── リフレクションヘルパー ────────────────────────────────────

    /// <summary>TetrisGame のプライベートフィールドを取得する。</summary>
    private T Get<T>(string fieldName)
    {
        var field = typeof(TetrisGame).GetField(fieldName, NonPublic);
        Assert.IsNotNull(field, $"フィールド '{fieldName}' が見つかりません");
        return (T)field.GetValue(_game);
    }

    /// <summary>TetrisGame のプライベートフィールドに値をセットする。</summary>
    private void Set(string fieldName, object value)
    {
        var field = typeof(TetrisGame).GetField(fieldName, NonPublic);
        Assert.IsNotNull(field, $"フィールド '{fieldName}' が見つかりません");
        field.SetValue(_game, value);
    }

    // ═══════════════════════════════════════════════════════════════
    // StartGame
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void StartGame_SetsIsPlayingTrue()
    {
        _game.StartGame();
        Assert.IsTrue(_game.IsPlaying, "StartGame 後は IsPlaying が true のはず");
    }

    [Test]
    public void StartGame_SpawnsMino()
    {
        _game.StartGame();
        Assert.IsTrue(_game.HasMino, "StartGame 後は HasMino が true のはず");
    }

    [Test]
    public void StartGame_FiresOnGameStartedEvent()
    {
        bool fired = false;
        _game.OnGameStarted += () => fired = true;

        _game.StartGame();

        Assert.IsTrue(fired, "StartGame で OnGameStarted イベントが発火するはず");
    }

    [Test]
    public void StartGame_CanRestartAfterGameOver()
    {
        _game.StartGame();
        // グリッドを埋めてゲームオーバーを発生させる
        while (_game.IsPlaying)
            _game.HardDrop();

        // 再スタート
        _game.StartGame();

        Assert.IsTrue(_game.IsPlaying, "再スタート後は IsPlaying が true のはず");
        Assert.IsTrue(_game.HasMino,   "再スタート後は HasMino が true のはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // MoveMino ── 移動
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void MoveMino_Left_DecreasesX()
    {
        _game.StartGame();
        int initialX = Get<int>("_currentX");

        _game.MoveMino(-1, 0);

        Assert.AreEqual(initialX - 1, Get<int>("_currentX"), "左移動で X が 1 減るはず");
    }

    [Test]
    public void MoveMino_Right_IncreasesX()
    {
        _game.StartGame();
        int initialX = Get<int>("_currentX");

        _game.MoveMino(1, 0);

        Assert.AreEqual(initialX + 1, Get<int>("_currentX"), "右移動で X が 1 増えるはず");
    }

    [Test]
    public void MoveMino_Down_IncreasesY()
    {
        _game.StartGame();
        int initialY = Get<int>("_currentY");

        _game.MoveMino(0, 1);

        Assert.AreEqual(initialY + 1, Get<int>("_currentY"), "下移動で Y が 1 増えるはず");
    }

    [Test]
    public void MoveMino_CannotMoveOutOfLeftBound()
    {
        _game.StartGame();

        // 十分な回数だけ左に動かす
        for (int i = 0; i < TetrisConfig.COLS; i++)
            _game.MoveMino(-1, 0);

        Assert.GreaterOrEqual(Get<int>("_currentX"), 0, "ミノは左端より外に出てはいけない");
    }

    [Test]
    public void MoveMino_CannotMoveOutOfRightBound()
    {
        _game.StartGame();

        // 十分な回数だけ右に動かす
        for (int i = 0; i < TetrisConfig.COLS; i++)
            _game.MoveMino(1, 0);

        int x = Get<int>("_currentX");
        var cells = Get<Vector2Int[]>("_currentCells");
        int maxCellX = 0;
        foreach (var c in cells) if (c.x > maxCellX) maxCellX = c.x;
        int shapeWidth = maxCellX + 1;
        Assert.LessOrEqual(x + shapeWidth, TetrisConfig.COLS, "ミノは右端より外に出てはいけない");
    }

    // ═══════════════════════════════════════════════════════════════
    // RotateMino ── 回転
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void RotateMino_Clockwise_ChangesShapeDimensions()
    {
        _game.StartGame();

        // T ミノ（index=2）をセル座標配列で強制設定
        Set("_currentCells", TetrisGame.GetCells(2));
        Set("_currentShapeIdx", 2);

        _game.RotateMino(true);  // 時計回り

        // 90° 回転後のセル境界ボックスは高さ 3 × 幅 3
        // (SRS state R: cells at x=1,2 y=0,1,2 → max x=2 → width=3)
        var cells = Get<Vector2Int[]>("_currentCells");
        int height = 0, width = 0;
        foreach (var c in cells) { if (c.y + 1 > height) height = c.y + 1; if (c.x + 1 > width) width = c.x + 1; }
        Assert.AreEqual(3, height, "時計回り後: 高さは 3 のはず");
        Assert.AreEqual(3, width,  "時計回り後: 幅は 3 のはず");
    }

    [Test]
    public void RotateMino_CounterClockwise_ChangesShapeDimensions()
    {
        _game.StartGame();

        // T ミノ（index=2）をセル座標配列で強制設定
        Set("_currentCells", TetrisGame.GetCells(2));
        Set("_currentShapeIdx", 2);

        _game.RotateMino(false);  // 反時計回り

        // 90° 回転後のセル境界ボックスは高さ 3 × 幅 2
        var cells = Get<Vector2Int[]>("_currentCells");
        int height = 0, width = 0;
        foreach (var c in cells) { if (c.y + 1 > height) height = c.y + 1; if (c.x + 1 > width) width = c.x + 1; }
        Assert.AreEqual(3, height, "反時計回り後: 高さは 3 のはず");
        Assert.AreEqual(2, width,  "反時計回り後: 幅は 2 のはず");
    }

    [Test]
    public void RotateMino_FourTimes_RestoresOriginalShape()
    {
        _game.StartGame();

        // T ミノ（index=2）をセル座標配列で強制設定して 4 回時計回り回転
        Set("_currentCells", TetrisGame.GetCells(2));
        Set("_currentShapeIdx", 2);

        for (int i = 0; i < 4; i++)
            _game.RotateMino(true);

        // 4 回転後のセル境界ボックスは元の T ミノ（高さ 2、幅 3）
        var cells = Get<Vector2Int[]>("_currentCells");
        int height = 0, width = 0;
        foreach (var c in cells) { if (c.y + 1 > height) height = c.y + 1; if (c.x + 1 > width) width = c.x + 1; }
        Assert.AreEqual(2, height, "4 回転後: 高さが元に戻るはず");
        Assert.AreEqual(3, width,  "4 回転後: 幅が元に戻るはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // HardDrop ── 即時落下
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void HardDrop_PlacesBlockAtBottom()
    {
        _game.StartGame();

        // 1×1 セルを列 5 に配置して落とす
        Set("_currentCells", new Vector2Int[] { Vector2Int.zero });
        Set("_currentX", 5);
        Set("_currentY", 0);

        _game.HardDrop();

        var grid = Get<bool[,]>("_grid");
        Assert.IsTrue(grid[5, TetrisConfig.ROWS - 1],
            "HardDrop 後、ブロックは最下行のグリッドに固定されるはず");
    }

    [Test]
    public void HardDrop_SpawnsNewMinoAfterLock()
    {
        _game.StartGame();
        _game.HardDrop();

        Assert.IsTrue(_game.HasMino, "HardDrop 後に次のミノが生成されるはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // Tick ── 自動落下タイマー
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Tick_AutoFallsAfterOneInterval()
    {
        _game.StartGame();
        int initialY = Get<int>("_currentY");

        _game.Tick(TetrisConfig.DEFAULT_FALL_INTERVAL + 0.01f);

        Assert.AreEqual(initialY + 1, Get<int>("_currentY"),
            "落下間隔を超えた Tick でミノが 1 段落下するはず");
    }

    [Test]
    public void Tick_DoesNotFallBeforeInterval()
    {
        _game.StartGame();
        int initialY = Get<int>("_currentY");

        _game.Tick(TetrisConfig.DEFAULT_FALL_INTERVAL * 0.5f);

        Assert.AreEqual(initialY, Get<int>("_currentY"),
            "落下間隔未満の Tick ではミノが落下しないはず");
    }

    [Test]
    public void Tick_DoesNothingWhenNotPlaying()
    {
        // StartGame を呼ばない状態で Tick しても何も起こらないこと
        Assert.DoesNotThrow(() => _game.Tick(10f),
            "ゲーム開始前の Tick は例外を出さないはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // HoldMino ── ホールド
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void HoldMino_StoresCurrentPiece()
    {
        _game.StartGame();

        _game.HoldMino();

        Assert.GreaterOrEqual(Get<int>("_holdShapeIdx"), 0,
            "HoldMino 後は holdShapeIdx が 0 以上のはず");
    }

    [Test]
    public void HoldMino_SpawnsNewMinoAfterFirstHold()
    {
        _game.StartGame();

        _game.HoldMino();

        Assert.IsTrue(_game.HasMino, "初回 HoldMino 後は新ミノが出るはず");
    }

    [Test]
    public void HoldMino_CannotHoldTwiceInSameTurn()
    {
        _game.StartGame();
        _game.HoldMino(); // 1 回目：有効

        int holdAfterFirst = Get<int>("_holdShapeIdx");

        _game.HoldMino(); // 2 回目：同ターン内なので無視されるはず

        Assert.AreEqual(holdAfterFirst, Get<int>("_holdShapeIdx"),
            "同ターン 2 回目の HoldMino は無視されるはず");
    }

    [Test]
    public void HoldMino_Exchange_ReturnsPreviousHeld()
    {
        _game.StartGame();
        int firstIdx = Get<int>("_currentShapeIdx");

        _game.HoldMino();   // firstIdx を Hold し、新ミノ(B)が登場
        _game.HardDrop();   // ミノ B を最下部まで落として固定 → Y=0 が空くので次ミノ(C)が正常スポーン
        _game.HoldMino();   // C を Hold し、firstIdx が戻る

        Assert.AreEqual(firstIdx, Get<int>("_currentShapeIdx"),
            "Hold 交換後は最初に Hold したミノが操作ミノに戻るはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // ClearLines ── ライン消去
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void ClearLines_FullRow_IsCleared()
    {
        _game.StartGame();

        // リフレクションで _grid を直接取得（参照型なので値が共有される）
        var grid = Get<bool[,]>("_grid");

        // 最下行の左 9 列（0 ～ COLS-2）を埋める
        for (int col = 0; col < TetrisConfig.COLS - 1; col++)
            grid[col, TetrisConfig.ROWS - 1] = true;

        // 残り 1 マス（列 COLS-1）を 1×1 セルで埋めて固定 → 行消去を発動
        Set("_currentCells", new Vector2Int[] { Vector2Int.zero });
        Set("_currentX", TetrisConfig.COLS - 1);
        Set("_currentY", TetrisConfig.ROWS - 1);

        _game.ForceLock();

        // 行消去後、最下行は全 false のはず
        for (int col = 0; col < TetrisConfig.COLS; col++)
            Assert.IsFalse(grid[col, TetrisConfig.ROWS - 1],
                $"列 {col} は行消去で false になるはず");
    }

    [Test]
    public void ClearLines_AboveRowDrops_AfterClear()
    {
        _game.StartGame();

        var grid = Get<bool[,]>("_grid");

        // 最下行を 9 列分埋める
        for (int col = 0; col < TetrisConfig.COLS - 1; col++)
            grid[col, TetrisConfig.ROWS - 1] = true;

        // 2 段目の先頭 1 マスを埋める（行消去後に 1 段落ちることを確認するため）
        grid[0, TetrisConfig.ROWS - 2] = true;

        // 最下行を完成させて行消去
        Set("_currentCells", new Vector2Int[] { Vector2Int.zero });
        Set("_currentX", TetrisConfig.COLS - 1);
        Set("_currentY", TetrisConfig.ROWS - 1);

        _game.ForceLock();

        // 2 段目にあったブロックが最下行（ROWS-1）に落ちているはず
        Assert.IsTrue(grid[0, TetrisConfig.ROWS - 1],
            "行消去後、上にあったブロックが 1 段落ちるはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // GameOver ── ゲームオーバー
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void GameOver_FiresEventWhenGridFull()
    {
        bool fired = false;
        _game.OnGameOver += () => fired = true;

        _game.StartGame();
        while (_game.IsPlaying)
            _game.HardDrop();

        Assert.IsTrue(fired, "グリッドが埋まると OnGameOver が発火するはず");
    }

    [Test]
    public void GameOver_SetsIsPlayingFalse()
    {
        _game.StartGame();
        while (_game.IsPlaying)
            _game.HardDrop();

        Assert.IsFalse(_game.IsPlaying, "ゲームオーバー後は IsPlaying が false のはず");
    }

    [Test]
    public void GameOver_HasMinoIsFalse()
    {
        _game.StartGame();
        while (_game.IsPlaying)
            _game.HardDrop();

        Assert.IsFalse(_game.HasMino, "ゲームオーバー後は HasMino が false のはず");
    }

    // ═══════════════════════════════════════════════════════════════
    // SRS Wall Kick ── T ミノの様々なブロック配置での回転挙動
    //
    // T ミノ各回転状態のローカルセル定義（pivot = (1,1)）:
    //   State 0 (Spawn) : (1,0)(0,1)(1,1)(2,1)  →  .T. / TTT
    //   State R (CW×1)  : (1,0)(1,1)(2,1)(1,2)  →  .T. / .TT / .T.
    //   State 2 (CW×2)  : (0,1)(1,1)(2,1)(1,2)  →  TTT / .T.
    //   State L (CCW×1) : (1,0)(0,1)(1,1)(1,2)  →  .T. / TT. / .T.
    //
    // JLSTZ キックテーブル（Y 下向き正）:
    //   0→R : (0,0)(-1,0)(-1,-1)(0,2)(-1,2)
    //   R→0 : (0,0)(1,0)(1,1)(0,-2)(1,-2)
    //   R→2 : (0,0)(1,0)(1,1)(0,-2)(1,-2)
    //   2→R : (0,0)(-1,0)(-1,-1)(0,2)(-1,2)
    //   2→L : (0,0)(1,0)(1,-1)(0,2)(1,2)
    //   L→2 : (0,0)(-1,0)(-1,1)(0,-2)(-1,-2)
    //   L→0 : (0,0)(-1,0)(-1,1)(0,-2)(-1,-2)
    //   0→L : (0,0)(1,0)(1,-1)(0,2)(1,2)
    // ═══════════════════════════════════════════════════════════════

    // ── ヘルパー ─────────────────────────────────────────────────

    /// <summary>_grid[col, row] を true にする。</summary>
    private void Block(int col, int row) => Get<bool[,]>("_grid")[col, row] = true;

    /// <summary>T ミノを指定状態・位置でセットし、回転前の前提条件を整える。</summary>
    private void SetupT(Vector2Int[] cells, int rotation, int x, int y)
    {
        _game.StartGame();
        Set("_currentCells",    cells);
        Set("_currentShapeIdx", 2);
        Set("_currentRotation", rotation);
        Set("_currentX", x);
        Set("_currentY", y);
    }

    // ── 障害なしの基本回転 ─────────────────────────────────────────

    /// <summary>
    /// 障害なし・中央配置での CW 回転: キック 1 (0,0) で成功し位置は変わらない。
    /// </summary>
    [Test]
    public void SRS_T_CW_0toR_OpenField_NoPositionChange()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 3, y: 5);

        _game.RotateMino(true);

        Assert.AreEqual(1, Get<int>("_currentRotation"), "rotation = 1 (state R) になるはず");
        Assert.AreEqual(3, Get<int>("_currentX"),        "障害なしで x は変わらないはず");
        Assert.AreEqual(5, Get<int>("_currentY"),        "障害なしで y は変わらないはず");
    }

    /// <summary>
    /// CW 4 回転で rotation が 0→1→2→3→0 とサイクルし、元のセルに戻る。
    /// </summary>
    [Test]
    public void SRS_T_CW_FourRotations_CyclesStateAndRestoresCells()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 3, y: 5);

        _game.RotateMino(true); Assert.AreEqual(1, Get<int>("_currentRotation"), "1 回目: state R");
        _game.RotateMino(true); Assert.AreEqual(2, Get<int>("_currentRotation"), "2 回目: state 2");
        _game.RotateMino(true); Assert.AreEqual(3, Get<int>("_currentRotation"), "3 回目: state L");
        _game.RotateMino(true); Assert.AreEqual(0, Get<int>("_currentRotation"), "4 回目: state 0");

        CollectionAssert.AreEquivalent(
            TetrisGame.GetCells(2), Get<Vector2Int[]>("_currentCells"),
            "4 回転でセルが元に戻るはず");
    }

    /// <summary>
    /// CCW 1 回転: 0 → state L (rotation=3)。セルが state L になることを確認。
    /// </summary>
    [Test]
    public void SRS_T_CCW_0toL_OpenField_CellsMatchStateL()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 3, y: 5);

        _game.RotateMino(false);  // CCW: 0 → L

        Assert.AreEqual(3, Get<int>("_currentRotation"), "rotation = 3 (state L) になるはず");
        var expectedL = new Vector2Int[] { new(1,0), new(0,1), new(1,1), new(1,2) };
        CollectionAssert.AreEquivalent(expectedL, Get<Vector2Int[]>("_currentCells"),
            "state L のセルになるはず");
    }

    // ── 壁キック ──────────────────────────────────────────────────

    /// <summary>
    /// 0→R CW、キック 2 (dx=-1, dy=0):
    /// state R の下端セル (4,7) が塞がれているためキック 1 が失敗し、
    /// 左シフトしたキック 2 で成功する。
    ///
    ///   col:  3  4  5     state 0 @ (3,5)    state R @ kick1 (3,5)
    ///   row 5:    ●        . T .               . T .
    ///   row 6:    ●        T T T               . T T
    ///   row 7:    □ ←block                     . T .  ← (4,7) が塞がれて失敗
    ///
    ///   kick2(-1,0) → state R @ (2,5): (3,5)(3,6)(4,6)(3,7) — 空き → 成功
    /// </summary>
    [Test]
    public void SRS_T_CW_0toR_Kick2_Dx_Minus1()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 3, y: 5);
        Block(4, 7);  // state R @ kick1 の下端セルを塞ぐ

        _game.RotateMino(true);

        Assert.AreEqual(1, Get<int>("_currentRotation"), "state R (rotation=1) になるはず");
        Assert.AreEqual(2, Get<int>("_currentX"),        "左シフトで x = 2 になるはず");
        Assert.AreEqual(5, Get<int>("_currentY"),        "y は変わらないはず");
    }

    /// <summary>
    /// 0→R CW、キック 3 (dx=-1, dy=-1):
    /// キック 1・2 が失敗し、左+上シフトのキック 3 で成功する。
    ///
    ///   Block (4,7): kick1(0,0) → state R @ (3,5) 失敗
    ///   Block (3,7): kick2(-1,0) → state R @ (2,5) の (3,7) が塞がれて失敗
    ///   kick3(-1,-1) → state R @ (2,4): (3,4)(3,5)(4,5)(3,6) — 空き → 成功
    /// </summary>
    [Test]
    public void SRS_T_CW_0toR_Kick3_Dx_Minus1_Dy_Minus1()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 3, y: 5);
        Block(4, 7);  // kick1 失敗: state R @ (3,5) の (4,7) を塞ぐ
        Block(3, 7);  // kick2 失敗: state R @ (2,5) の (3,7) を塞ぐ

        _game.RotateMino(true);

        Assert.AreEqual(1, Get<int>("_currentRotation"), "state R (rotation=1) になるはず");
        Assert.AreEqual(2, Get<int>("_currentX"),        "左シフト -1 で x = 2 になるはず");
        Assert.AreEqual(4, Get<int>("_currentY"),        "上シフト -1 で y = 4 になるはず");
    }

    /// <summary>
    /// L→0 CW、キック 2 (dx=-1, dy=0) — 右壁による自動キック:
    /// state L を右端 (x=8) に置くと、回転後の state 0 の右端セルが
    /// col=10 で右壁外となりキック 1 が失敗する。障害ブロック不要。
    ///
    ///   state L @ (8,5): col 8-9 は有効
    ///   state 0 @ kick1(0,0) @ (8,5): (8+2,5+1)=col 10 → 右壁外 → 失敗
    ///   kick2(-1,0) → state 0 @ (7,5): (8,5)(7,6)(8,6)(9,6) — 成功
    /// </summary>
    [Test]
    public void SRS_T_CW_LtoO_Kick2_RightWall_NoGridBlock()
    {
        // state L のセルを手動設定（x=8 で右壁ぎわ）
        SetupT(new Vector2Int[] { new(1,0), new(0,1), new(1,1), new(1,2) },
               rotation: 3, x: 8, y: 5);

        _game.RotateMino(true);  // CW: L → 0

        Assert.AreEqual(0, Get<int>("_currentRotation"), "state 0 (rotation=0) になるはず");
        Assert.AreEqual(7, Get<int>("_currentX"),        "右壁キックで x = 7 になるはず");
        Assert.AreEqual(5, Get<int>("_currentY"),        "y は変わらないはず");
    }

    /// <summary>
    /// R→0 CCW、キック 2 (dx=+1, dy=0) — 左壁キック:
    /// state R を左端 (x=0) に置くと、回転後の state 0 の左端セル (col 0+0=0) が
    /// kick1(0,0) では _grid[0][row] の障害で失敗し、右シフトで成功する。
    ///
    ///   state R @ (0,5): (1,5)(1,6)(2,6)(1,7)
    ///   state 0 @ kick1(0,0) @ (0,5): (1,5)(0,6)(1,6)(2,6)
    ///     → Block (0,6) で失敗
    ///   kick2(+1,0) → state 0 @ (1,5): (2,5)(1,6)(2,6)(3,6) — 成功
    /// </summary>
    [Test]
    public void SRS_T_CCW_RtoO_Kick2_LeftWall()
    {
        SetupT(new Vector2Int[] { new(1,0), new(1,1), new(2,1), new(1,2) },
               rotation: 1, x: 0, y: 5);
        Block(0, 6);  // state 0 @ kick1 の左端セルを塞ぐ

        _game.RotateMino(false);  // CCW: R → 0

        Assert.AreEqual(0, Get<int>("_currentRotation"), "state 0 (rotation=0) になるはず");
        Assert.AreEqual(1, Get<int>("_currentX"),        "右シフトキックで x = 1 になるはず");
        Assert.AreEqual(5, Get<int>("_currentY"),        "y は変わらないはず");
    }

    // ── フロアキック（上方向シフト） ───────────────────────────────

    /// <summary>
    /// R→2 CW、キック 4 (dx=0, dy=-2) — 上方向フロアキック:
    /// フィールド底付近で kick1～kick3 が失敗し、上 2 マスシフトで成功する。
    ///
    ///   state R @ (3,17): (4,17)(4,18)(5,18)(4,19)
    ///   state 2 cells: (0,1)(1,1)(2,1)(1,2)
    ///
    ///   kick1(0,0)  @ (3,17): (3,18) が Block → 失敗
    ///   kick2(1,0)  @ (4,17): (6,18) が Block → 失敗
    ///   kick3(1,1)  @ (4,18): (5,20) が row>=ROWS → 自動失敗
    ///   kick4(0,-2) @ (3,15): (3,16)(4,16)(5,16)(4,17) — 空き → 成功
    /// </summary>
    [Test]
    public void SRS_T_CW_RtoO2_Kick4_FloorKick_UpShift2()
    {
        SetupT(new Vector2Int[] { new(1,0), new(1,1), new(2,1), new(1,2) },
               rotation: 1, x: 3, y: TetrisConfig.ROWS - 3);  // y=17
        Block(3, TetrisConfig.ROWS - 2);  // (3,18): kick1 を失敗させる
        Block(6, TetrisConfig.ROWS - 2);  // (6,18): kick2 を失敗させる

        _game.RotateMino(true);  // CW: R → 2

        Assert.AreEqual(2,                        Get<int>("_currentRotation"), "state 2 になるはず");
        Assert.AreEqual(3,                        Get<int>("_currentX"),        "x は変わらないはず");
        Assert.AreEqual(TetrisConfig.ROWS - 5,    Get<int>("_currentY"),        "上 2 マスキックで y = 15 になるはず");
    }

    /// <summary>
    /// R→2 CW、キック 5 (dx=+1, dy=-2) — 右+上シフト（T スピン系キック）:
    /// kick1～kick4 が失敗し、右 1・上 2 マスシフトで成功する。
    ///
    ///   state R @ (2,17): (3,17)(3,18)(4,18)(3,19)
    ///   kick1(0,0)  @ (2,17): (2,18) が Block → 失敗
    ///   kick2(1,0)  @ (3,17): (5,18) が Block → 失敗
    ///   kick3(1,1)  @ (3,18): (4,20) が row>=ROWS → 自動失敗
    ///   kick4(0,-2) @ (2,15): (2,16) が Block → 失敗
    ///   kick5(1,-2) @ (3,15): (3,16)(4,16)(5,16)(4,17) — 空き → 成功
    /// </summary>
    [Test]
    public void SRS_T_CW_RtoO2_Kick5_RightUpShift()
    {
        SetupT(new Vector2Int[] { new(1,0), new(1,1), new(2,1), new(1,2) },
               rotation: 1, x: 2, y: TetrisConfig.ROWS - 3);  // y=17
        Block(2, TetrisConfig.ROWS - 2);  // (2,18): kick1 を失敗させる
        Block(5, TetrisConfig.ROWS - 2);  // (5,18): kick2 を失敗させる
        Block(2, TetrisConfig.ROWS - 4);  // (2,16): kick4 を失敗させる

        _game.RotateMino(true);  // CW: R → 2

        Assert.AreEqual(2,                     Get<int>("_currentRotation"), "state 2 になるはず");
        Assert.AreEqual(3,                     Get<int>("_currentX"),        "右 1 マスキックで x = 3 になるはず");
        Assert.AreEqual(TetrisConfig.ROWS - 5, Get<int>("_currentY"),        "上 2 マスキックで y = 15 になるはず");
    }

    // ── 全キック失敗 ──────────────────────────────────────────────

    /// <summary>
    /// 全キック (5 箇所) が失敗するとき回転は発生しない:
    /// state 0 → R CW で、各キック位置の最小障害ブロックを配置する。
    ///
    ///   state 0 @ (3,5) world: (4,5)(3,6)(4,6)(5,6)  ← 開始位置（ブロックなし）
    ///   kick1(0,0)  @ (3,5): state R の (4,7) を Block → 失敗
    ///   kick2(-1,0) @ (2,5): state R の (3,7) を Block → 失敗
    ///   kick3(-1,-1)@ (2,4): state R の (3,4) を Block → 失敗
    ///   kick4(0,2)  @ (3,7): (4,7) が既にブロック済み → 失敗
    ///   kick5(-1,2) @ (2,7): (3,7) が既にブロック済み → 失敗
    /// </summary>
    [Test]
    public void SRS_T_CW_0toR_AllKicksFail_RotationNotApplied()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 3, y: 5);
        Block(4, 7);  // kick1 & kick4 の共通セル
        Block(3, 7);  // kick2 & kick5 の共通セル
        Block(3, 4);  // kick3 専用セル

        _game.RotateMino(true);

        Assert.AreEqual(0, Get<int>("_currentRotation"), "全キック失敗なら rotation は変わらないはず");
        Assert.AreEqual(3, Get<int>("_currentX"),        "x は変わらないはず");
        Assert.AreEqual(5, Get<int>("_currentY"),        "y は変わらないはず");
        CollectionAssert.AreEquivalent(
            TetrisGame.GetCells(2), Get<Vector2Int[]>("_currentCells"),
            "セルは変わらないはず");
    }

    /// <summary>
    /// フィールド左端 (x=0) に state 0 を配置して全キックが失敗することを確認する。
    ///
    /// State R local cells: (1,0)(1,1)(2,1)(1,2) → pivot=(1,1)
    /// State 0 @ (0,5) world: (1,5)(0,6)(1,6)(2,6)
    ///
    /// JLSTZ 0→R キックテーブル: (0,0)(-1,0)(-1,-1)(0,2)(-1,2)
    ///
    ///   kick1 (0,0)  → state R @ (0,5):  world (1,5)(1,6)(2,6)(1,7) → Block(1,7) → 失敗
    ///   kick2 (-1,0) → state R @ (-1,5): world (0,5)(0,6)(1,6)(0,7) → Block(0,7) → 失敗
    ///   kick3 (-1,-1)→ state R @ (-1,4): world (0,4)(0,5)(1,5)(0,6) → Block(0,6) → 失敗
    ///   kick4 (0,2)  → state R @ (0,7):  world (1,7)(1,8)(2,8)(1,9) → Block(1,7) → 失敗
    ///   kick5 (-1,2) → state R @ (-1,7): world (0,7)(0,8)(1,8)(0,9) → Block(0,7) → 失敗
    /// </summary>
    [Test]
    public void SRS_T_CW_0toR_AllKicksFail_LeftWallFully_Blocked()
    {
        SetupT(TetrisGame.GetCells(2), rotation: 0, x: 0, y: 5);
        Block(1, 7);  // kick1(0,0) と kick4(0,2) を失敗させる
        Block(0, 7);  // kick2(-1,0) と kick5(-1,2) を失敗させる
        Block(0, 6);  // kick3(-1,-1) を失敗させる (state R @ (-1,4) の (0,6))

        _game.RotateMino(true);

        Assert.AreEqual(0, Get<int>("_currentRotation"), "左壁ぎわ全キック失敗で rotation は変わらないはず");
        Assert.AreEqual(0, Get<int>("_currentX"),        "x は変わらないはず");
    }
}
