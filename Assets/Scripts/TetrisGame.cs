using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// テトリスゲームの内部処理を担当するクラス。
/// グリッド管理・ミノ操作・ライン消去・ゲームオーバー判定を行う。
/// Unity 非依存の純粋なゲームロジックとして実装し、
/// 描画は TetrisRenderer へ委譲する。
/// </summary>
public class TetrisGame
{
    // ── イベント ──────────────────────────────────────────────────
    /// <summary>ゲームオーバー時に発火する。</summary>
    public event Action OnGameOver;
    /// <summary>ゲーム開始時に発火する。</summary>
    public event Action OnGameStarted;

    // ── 公開プロパティ ─────────────────────────────────────────────
    public bool IsPlaying => _isPlaying;
    public bool HasMino   => _currentShape != null;

    // ── グリッド状態 ──────────────────────────────────────────────
    // true = 固定済みブロックあり
    private readonly bool[,] _grid = new bool[TetrisConfig.COLS, TetrisConfig.ROWS];

    // ── 現在操作中ミノ ────────────────────────────────────────────
    private int[,] _currentShape;
    private Color  _currentColor;
    private int    _currentX, _currentY;
    private int    _currentShapeIdx;

    // ── Hold ─────────────────────────────────────────────────────
    private int  _holdShapeIdx = -1;  // -1 = 空
    private bool _holdUsed;           // true = 今ターン使用済み

    // ── Next ─────────────────────────────────────────────────────
    private readonly int          _nextCount;              // 表示する Next 数
    private readonly Queue<int>   _nextQueue = new Queue<int>(); // 次ミノのキュー

    // ── 7種1順バッグ ─────────────────────────────────────────────
    private readonly List<int> _bag = new List<int>(); // シャッフル済みバッグ

    // ── ゲーム状態 ────────────────────────────────────────────────
    private bool  _isPlaying;
    private float _fallTimer;
    private bool  _isLocking;   // 接地して固定待ち中
    private float _lockTimer;

    private readonly TetrisRenderer _renderer;
    private readonly float          _fallInterval;
    private readonly float          _lockDelay;

    // ── コンストラクタ ─────────────────────────────────────────────
    public TetrisGame(TetrisRenderer renderer, float fallInterval, float lockDelay, int nextCount = 1)
    {
        _renderer     = renderer;
        _fallInterval = fallInterval;
        _lockDelay    = lockDelay;
        _nextCount    = Mathf.Max(1, nextCount);
    }

    // ── 公開 API ──────────────────────────────────────────────────

    /// <summary>ゲームを開始する。</summary>
    public void StartGame()
    {
        ClearGrid();
        _isPlaying    = true;
        _fallTimer    = 0f;
        _isLocking    = false;
        _lockTimer    = 0f;
        _holdShapeIdx = -1;
        _holdUsed     = false;
        _renderer.ClearHold();
        // キューを初期化して Next ミノを先読みする
        _nextQueue.Clear();
        _bag.Clear();
        for (int i = 0; i < _nextCount; i++)
            _nextQueue.Enqueue(NextFromBag());
        OnGameStarted?.Invoke();
        SpawnRandomMino();
    }

    /// <summary>Next キューの先頭ミノを生成し、新しいミノをキューに積む。</summary>
    public void SpawnRandomMino()
    {
        // キューが空の場合（デバッグ直呼び出し等）は補填
        while (_nextQueue.Count < _nextCount)
            _nextQueue.Enqueue(NextFromBag());

        int spawnIdx = _nextQueue.Dequeue();
        _nextQueue.Enqueue(NextFromBag());
        _renderer.DrawNextQueue(_nextQueue.ToArray());
        SpawnMino(spawnIdx);
    }

    /// <summary>
    /// 7種1順バッグから次のミノインデックスを返す。
    /// バッグが空になったら全7種をシャッフルして補充する。
    /// </summary>
    private int NextFromBag()
    {
        if (_bag.Count == 0)
        {
            for (int i = 0; i < TetrisConfig.SHAPES.Length; i++)
                _bag.Add(i);
            // Fisher-Yatesシャッフル
            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
        }
        int idx = _bag[_bag.Count - 1];
        _bag.RemoveAt(_bag.Count - 1);
        return idx;
    }

    /// <summary>指定インデックスのミノを初期回転でフィールド上部に生成する。</summary>
    private void SpawnMino(int shapeIdx)
    {
        int[,] sh  = TetrisConfig.SHAPES[shapeIdx];
        int spawnX = (TetrisConfig.COLS - sh.GetLength(1)) / 2;
        if (_isPlaying && !IsValidPosition(sh, spawnX, 0))
        {
            GameOver();
            return;
        }
        _currentShapeIdx = shapeIdx;
        PlaceMino(sh, TetrisConfig.COLORS[shapeIdx]);
        _isLocking = false;
        _lockTimer = 0f;
        _fallTimer = 0f;
    }

    /// <summary>現在のミノを Hold する、または Hold 内のミノと入れ替える。(H キー)</summary>
    public void HoldMino()
    {
        if (_currentShape == null || _holdUsed) return;

        int prevHoldIdx = _holdShapeIdx;
        _holdShapeIdx   = _currentShapeIdx;
        _holdUsed       = true;

        // Hold 表示を更新
        _renderer.DrawHold(TetrisConfig.SHAPES[_holdShapeIdx], TetrisConfig.COLORS[_holdShapeIdx]);
        _renderer.ClearMino();
        _currentShape = null;

        // Hold が空だった → 新ミノを生成、そうでなければ Hold からミノを取り出す
        if (prevHoldIdx < 0)
            SpawnRandomMino();
        else
            SpawnMino(prevHoldIdx);
    }

    /// <summary>操作中ミノを移動する。dx: 横変位, dy: 縦変位（下が正）</summary>
    public void MoveMino(int dx, int dy)
    {
        if (_currentShape == null) return;
        int nx = _currentX + dx, ny = _currentY + dy;
        if (IsValidPosition(_currentShape, nx, ny))
        {
            _currentX = nx;
            _currentY = ny;
            _renderer.DrawMino(_currentShape, _currentColor, _currentX, _currentY);
            // ソフトドロップ: 落下タイマーリセット
            if (dy > 0) _fallTimer = 0f;
            // 横移動で接地が解消された場合はロックタイマーリセット
            if (_isLocking && IsValidPosition(_currentShape, _currentX, _currentY + 1))
            {
                _isLocking = false;
                _lockTimer = 0f;
            }
        }
        else if (dy > 0 && !_isLocking)
        {
            // 下向き移動失敗 = 接地 → 固定待ち開始
            _isLocking = true;
            _lockTimer = 0f;
        }
    }

    /// <summary>操作中ミノを回転する。clockwise=true で時計回り(E)。</summary>
    public void RotateMino(bool clockwise)
    {
        if (_currentShape == null) return;
        var rotated = RotateShape(_currentShape, clockwise);
        if (!IsValidPosition(rotated, _currentX, _currentY)) return;

        _currentShape = rotated;
        _renderer.DrawMino(_currentShape, _currentColor, _currentX, _currentY);
        // 回転で接地が解消された場合はロックタイマーリセット
        if (_isLocking && IsValidPosition(_currentShape, _currentX, _currentY + 1))
        {
            _isLocking = false;
            _lockTimer = 0f;
        }
    }

    /// <summary>ミノを最下部まで落として即固定する。(W キー)</summary>
    public void HardDrop()
    {
        if (_currentShape == null) return;
        while (IsValidPosition(_currentShape, _currentX, _currentY + 1))
            _currentY++;
        _renderer.DrawMino(_currentShape, _currentColor, _currentX, _currentY);
        LockMinoInternal();
    }

    /// <summary>デバッグ用: 現在ミノを即座に固定する。</summary>
    public void ForceLock() => LockMinoInternal();

    /// <summary>毎フレーム呼ぶタイマー更新処理（自動落下・固定遅延）。</summary>
    public void Tick(float deltaTime)
    {
        if (!_isPlaying || _currentShape == null) return;

        if (_isLocking)
        {
            _lockTimer += deltaTime;
            if (_lockTimer >= _lockDelay) LockMinoInternal();
        }
        else
        {
            _fallTimer += deltaTime;
            if (_fallTimer >= _fallInterval)
            {
                _fallTimer = 0f;
                AutoFall();
            }
        }
    }

    // ── 内部処理 ──────────────────────────────────────────────────

    private void PlaceMino(int[,] shape, Color color)
    {
        _renderer.ClearMino();
        _currentShape = shape;
        _currentColor = color;
        _currentX = (TetrisConfig.COLS - shape.GetLength(1)) / 2;
        _currentY = 0;
        _renderer.DrawMino(_currentShape, _currentColor, _currentX, _currentY);
    }

    // 1ステップ自動落下。接地したら固定待ちを開始する。
    private void AutoFall()
    {
        if (_currentShape == null) return;
        if (IsValidPosition(_currentShape, _currentX, _currentY + 1))
        {
            _currentY++;
            _renderer.DrawMino(_currentShape, _currentColor, _currentX, _currentY);
        }
        else
        {
            _isLocking = true;
            _lockTimer = 0f;
        }
    }

    // ミノを固定し、ライン消去 → 次ミノ生成を行う。
    private void LockMinoInternal()
    {
        if (_currentShape == null) return;
        int rows = _currentShape.GetLength(0), cols = _currentShape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (_currentShape[r, c] == 1)
                {
                    int col = _currentX + c, row = _currentY + r;
                    _grid[col, row] = true;
                    _renderer.PlaceBlock(col, row, _currentColor);
                }

        _renderer.ClearMino();
        _currentShape = null;
        _isLocking    = false;
        _lockTimer    = 0f;
        _fallTimer    = 0f;
        _holdUsed     = false;   // ロック完了で Hold 再使用可能に

        ClearLines();
        if (_isPlaying) SpawnRandomMino();
    }

    // 揃った行を消去し、上のブロックを1段落とす。連続消去にも対応。
    private void ClearLines()
    {
        for (int row = TetrisConfig.ROWS - 1; row >= 0; row--)
        {
            if (!IsRowFull(row)) continue;

            // 行を削除
            for (int col = 0; col < TetrisConfig.COLS; col++)
            {
                _grid[col, row] = false;
                _renderer.RemoveBlock(col, row);
            }
            // 消した行より上のブロックを1行落とす
            for (int r = row - 1; r >= 0; r--)
                for (int col = 0; col < TetrisConfig.COLS; col++)
                {
                    if (!_grid[col, r]) continue;
                    _grid[col, r + 1] = _grid[col, r];
                    _grid[col, r]     = false;
                    _renderer.DropBlock(col, r, r + 1);
                }
            row++; // 同インデックスを再チェック（連続消去対応）
        }
    }

    private bool IsRowFull(int row)
    {
        for (int col = 0; col < TetrisConfig.COLS; col++)
            if (!_grid[col, row]) return false;
        return true;
    }

    private void GameOver()
    {
        _isPlaying    = false;
        _currentShape = null;
        _renderer.ClearMino();
        OnGameOver?.Invoke();
    }

    private void ClearGrid()
    {
        _renderer.ClearAll();
        _currentShape = null;
        for (int col = 0; col < TetrisConfig.COLS; col++)
            for (int row = 0; row < TetrisConfig.ROWS; row++)
                _grid[col, row] = false;
    }

    private bool IsValidPosition(int[,] shape, int posX, int posY)
    {
        int rows = shape.GetLength(0), cols = shape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (shape[r, c] == 1)
                {
                    int fx = posX + c, fy = posY + r;
                    if (fx < 0 || fx >= TetrisConfig.COLS) return false;
                    if (fy < 0 || fy >= TetrisConfig.ROWS) return false;
                    if (_grid[fx, fy]) return false;
                }
        return true;
    }

    private static int[,] RotateShape(int[,] shape, bool isCw)
    {
        int rows = shape.GetLength(0), cols = shape.GetLength(1);
        var result = new int[cols, rows];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (isCw)
                    result[c, rows - 1 - r] = shape[r, c];
                else
                    result[cols - 1 - c, r] = shape[r, c];
        return result;
    }
}
