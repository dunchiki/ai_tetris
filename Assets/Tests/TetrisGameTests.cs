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
}
