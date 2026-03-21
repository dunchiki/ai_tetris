using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// テトリスのゲームフィールドを管理するコンポーネント。
/// フィールド座標: col = 0(左)〜9(右), row = 0(上)〜19(下)
/// </summary>
public class GameFieldController : MonoBehaviour
{
    public const int COLS = 10;
    public const int ROWS = 20;
    private const float CELL_SIZE = 30f;

    // 固定済みブロックのグリッド
    private Image[,] grid = new Image[COLS, ROWS];

    // 現在操作中のテトリミノ
    private int[,] currentShape;
    private Color currentColor;
    private int currentX, currentY;
    private List<Image> currentBlocks = new List<Image>();

    // ── ゲーム設定(Inspectorで調整可) ────────────────────────────
    [Header("Game Settings")]
    [SerializeField] private float fallInterval = 1.0f;  // 自動落下間隔(秒)
    [SerializeField] private float lockDelay    = 0.5f;  // 接地後に固定されるまでの遅延(秒)

    [Header("Debug (Inspector で有効化)")]
    [SerializeField] private bool debugMode = false;     // true のとき C/F キーが有効

    // ── ゲーム状態 ────────────────────────────────────────────────
    private bool  isPlaying;
    private float fallTimer;
    private bool  isLocking;  // 接地して固定待ち中
    private float lockTimer;

    private GameObject m_GameStartPanel;

    // 7種のテトリミノ形状 [row, col]
    private static readonly int[][,] SHAPES = new int[][,]
    {
        new int[,] { {1,1,1,1} },           // I
        new int[,] { {1,1}, {1,1} },         // O
        new int[,] { {0,1,0}, {1,1,1} },     // T
        new int[,] { {0,1,1}, {1,1,0} },     // S
        new int[,] { {1,1,0}, {0,1,1} },     // Z
        new int[,] { {1,0,0}, {1,1,1} },     // J
        new int[,] { {0,0,1}, {1,1,1} },     // L
    };

    private static readonly Color[] COLORS = new Color[]
    {
        new Color(0f,   0.9f, 1f),   // I シアン
        new Color(1f,   0.9f, 0f),   // O 黄
        new Color(0.6f, 0f,   1f),   // T 紫
        new Color(0f,   0.9f, 0f),   // S 緑
        new Color(1f,   0f,   0f),   // Z 赤
        new Color(0f,   0f,   1f),   // J 青
        new Color(1f,   0.5f, 0f),   // L オレンジ
    };

    // 長押し用パラメータ
    // 最初の入力から次の繰り返しまでの遅延(秒)
    private const float DAS_DELAY  = 0.15f;
    // 繰り返し間隔(秒)
    private const float DAS_REPEAT = 0.05f;

    private float dasTimerLeft, dasTimerRight, dasTimerDown;

    private void Start()
    {
        var btn = GameObject.Find("GameStartButton")?.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(StartGame);
    }

    private void Update()
    {
        // デバッグキー (Inspector の debugMode を ON にすると使用可)
        if (debugMode)
        {
            if (Input.GetKeyDown(KeyCode.C)) SpawnRandomMino();
            if (Input.GetKeyDown(KeyCode.F)) LockMinoInternal();
        }

        if (!isPlaying || currentShape == null) return;

        // 操作キー
        if (Input.GetKeyDown(KeyCode.W)) HardDrop();
        if (Input.GetKeyDown(KeyCode.Q)) RotateMino(clockwise: false);
        if (Input.GetKeyDown(KeyCode.E)) RotateMino(clockwise: true);

        HandleDAS(KeyCode.A, -1, 0, ref dasTimerLeft);
        HandleDAS(KeyCode.D,  1, 0, ref dasTimerRight);
        HandleDAS(KeyCode.S,  0, 1, ref dasTimerDown);

        // 固定待ち or 自動落下
        if (isLocking)
        {
            lockTimer += Time.deltaTime;
            if (lockTimer >= lockDelay) LockMinoInternal();
        }
        else
        {
            fallTimer += Time.deltaTime;
            if (fallTimer >= fallInterval)
            {
                fallTimer = 0f;
                AutoFall();
            }
        }
    }

    // DAS (Delayed Auto Shift): 初回 GetKeyDown で即反応、長押しで DAS_DELAY 後に DAS_REPEAT 間隔で繰り返す
    private void HandleDAS(KeyCode key, int dx, int dy, ref float timer)
    {
        if (Input.GetKeyDown(key))
        {
            MoveMino(dx, dy);
            timer = -DAS_DELAY;   // 初回遅延をタイマーにセット（負→0→繰り返し）
        }
        else if (Input.GetKey(key))
        {
            timer += Time.deltaTime;
            while (timer >= 0f)
            {
                MoveMino(dx, dy);
                timer -= DAS_REPEAT;
            }
        }
        else
        {
            timer = -DAS_DELAY;   // キーが離されたらリセット
        }
    }

    /// <summary>
    /// テトリスフィールドの指定座標にブロックを配置する。
    /// col: 0〜9, row: 0〜19
    /// </summary>
    public Image PlaceBlock(int col, int row, Color color)
    {
        if (col < 0 || col >= COLS || row < 0 || row >= ROWS) return null;

        if (grid[col, row] != null)
            Destroy(grid[col, row].gameObject);

        var img = CreateBlockImage(col, row, color);
        grid[col, row] = img;
        return img;
    }

    /// <summary>
    /// 指定した形状のテトリミノをフィールド上部中央に配置する。
    /// shape: [row, col] の2次元配列。1 = ブロックあり。
    /// </summary>
    public void PlaceMino(int[,] shape, Color color)
    {
        ClearCurrentMino();
        currentShape = shape;
        currentColor = color;
        currentX = (COLS - shape.GetLength(1)) / 2;
        currentY = 0;
        DrawCurrentMino();
    }

    /// <summary>
    /// ランダムなテトリミノを上部中央に生成する。配置不可ならゲームオーバー。
    /// </summary>
    public void SpawnRandomMino()
    {
        int idx    = Random.Range(0, SHAPES.Length);
        int[,] sh  = SHAPES[idx];
        int spawnX = (COLS - sh.GetLength(1)) / 2;
        // ゲームプレイ中のみゲームオーバー判定を行う
        if (isPlaying && !IsValidPosition(sh, spawnX, 0))
        {
            GameOver();
            return;
        }
        PlaceMino(sh, COLORS[idx]);
        isLocking = false;
        lockTimer = 0f;
        fallTimer = 0f;
    }

    /// <summary>
    /// 操作中のテトリミノを移動する。dx: 横変位, dy: 縦変位（下が正）
    /// A/S/D キーで呼ばれる。
    /// </summary>
    public void MoveMino(int dx, int dy)
    {
        if (currentShape == null) return;
        int nx = currentX + dx, ny = currentY + dy;
        if (IsValidPosition(currentShape, nx, ny))
        {
            currentX = nx;
            currentY = ny;
            DrawCurrentMino();
            // ソフトドロップ: 落下タイマーリセット
            if (dy > 0) fallTimer = 0f;
            // 横移動で接地が解消された場合はロックタイマーリセット
            if (isLocking && IsValidPosition(currentShape, currentX, currentY + 1))
            {
                isLocking = false;
                lockTimer = 0f;
            }
        }
        else if (dy > 0 && !isLocking)
        {
            // 下向き移動が失敗 = 接地 → 固定待ち開始
            isLocking = true;
            lockTimer = 0f;
        }
    }

    /// <summary>
    /// 操作中のテトリミノを回転する。clockwise=true で時計回り(E)、false で反時計回り(Q)。
    /// </summary>
    public void RotateMino(bool clockwise = true)
    {
        if (currentShape == null) return;
        var rotated = RotateShape(currentShape, clockwise);
        if (IsValidPosition(rotated, currentX, currentY))
        {
            currentShape = rotated;
            DrawCurrentMino();
            // 回転で接地が解消された場合はロックタイマーリセット
            if (isLocking && IsValidPosition(currentShape, currentX, currentY + 1))
            {
                isLocking = false;
                lockTimer = 0f;
            }
        }
    }

    /// <summary>
    /// ミノを最下部まで一気に落として即固定する。(W キー)
    /// </summary>
    public void HardDrop()
    {
        if (currentShape == null) return;
        while (IsValidPosition(currentShape, currentX, currentY + 1))
            currentY++;
        DrawCurrentMino();
        LockMinoInternal();
    }

    // ── ゲームロジック ────────────────────────────────────────────

    /// <summary>ゲームを開始する。GameStartButton の onClick から呼ばれる。</summary>
    public void StartGame()
    {
        ClearGrid();
        isPlaying = true;
        fallTimer = 0f;
        isLocking = false;
        lockTimer = 0f;
        m_GameStartPanel = GameObject.Find("GameStartPanel");
        if (m_GameStartPanel != null) m_GameStartPanel.SetActive(false);
        SpawnRandomMino();
    }

    // 1ステップ自動落下。接地したら固定待ちを開始する。
    private void AutoFall()
    {
        if (currentShape == null) return;
        if (IsValidPosition(currentShape, currentX, currentY + 1))
        {
            currentY++;
            DrawCurrentMino();
        }
        else
        {
            isLocking = true;
            lockTimer = 0f;
        }
    }

    // ミノを固定し、行消去 → 次ミノ生成までの一連の処理
    private void LockMinoInternal()
    {
        if (currentShape == null) return;
        int rows = currentShape.GetLength(0), cols = currentShape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (currentShape[r, c] == 1)
                    PlaceBlock(currentX + c, currentY + r, currentColor);

        ClearCurrentBlockImages();
        currentShape = null;
        isLocking    = false;
        lockTimer    = 0f;
        fallTimer    = 0f;

        ClearLines();
        if (isPlaying) SpawnRandomMino();
    }

    // 揃った行を消去し、上のブロックを落とす
    private void ClearLines()
    {
        for (int row = ROWS - 1; row >= 0; row--)
        {
            if (!IsRowFull(row)) continue;

            // 行を削除
            for (int col = 0; col < COLS; col++)
            {
                Destroy(grid[col, row].gameObject);
                grid[col, row] = null;
            }
            // 消した行より上のブロックを1行落とす
            for (int r = row - 1; r >= 0; r--)
                for (int col = 0; col < COLS; col++)
                {
                    if (grid[col, r] == null) continue;
                    grid[col, r + 1] = grid[col, r];
                    grid[col, r]     = null;
                    grid[col, r + 1].GetComponent<RectTransform>().anchoredPosition
                        = FieldToLocalPos(col, r + 1);
                }
            row++; // 同インデックスを再チェック（連続消去対応）
        }
    }

    private bool IsRowFull(int row)
    {
        for (int col = 0; col < COLS; col++)
            if (grid[col, row] == null) return false;
        return true;
    }

    private void GameOver()
    {
        isPlaying = false;
        ClearCurrentMino();
        if (m_GameStartPanel != null) m_GameStartPanel.SetActive(true);
    }

    private void ClearGrid()
    {
        ClearCurrentMino();
        for (int col = 0; col < COLS; col++)
            for (int row = 0; row < ROWS; row++)
                if (grid[col, row] != null)
                {
                    Destroy(grid[col, row].gameObject);
                    grid[col, row] = null;
                }
    }

    // ---- private ----

    private void DrawCurrentMino()
    {
        ClearCurrentBlockImages();
        int rows = currentShape.GetLength(0), cols = currentShape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (currentShape[r, c] == 1)
                    currentBlocks.Add(CreateBlockImage(currentX + c, currentY + r, currentColor));
    }

    private Image CreateBlockImage(int col, int row, Color color)
    {
        var go = new GameObject($"Block_{col}_{row}");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CELL_SIZE - 1f, CELL_SIZE - 1f);
        rt.anchoredPosition = FieldToLocalPos(col, row);

        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // テトリスフィールド座標 → GameField内のローカルUI座標（中心原点）
    private static Vector2 FieldToLocalPos(int col, int row)
    {
        float ox = -(COLS * CELL_SIZE) * 0.5f + CELL_SIZE * 0.5f;
        float oy =  (ROWS * CELL_SIZE) * 0.5f - CELL_SIZE * 0.5f;
        return new Vector2(ox + col * CELL_SIZE, oy - row * CELL_SIZE);
    }

    private void ClearCurrentBlockImages()
    {
        foreach (var img in currentBlocks)
            if (img != null) Destroy(img.gameObject);
        currentBlocks.Clear();
    }

    private void ClearCurrentMino()
    {
        ClearCurrentBlockImages();
        currentShape = null;
    }

    private bool IsValidPosition(int[,] shape, int posX, int posY)
    {
        int rows = shape.GetLength(0), cols = shape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (shape[r, c] == 1)
                {
                    int fx = posX + c, fy = posY + r;
                    if (fx < 0 || fx >= COLS || fy < 0 || fy >= ROWS) return false;
                    if (grid[fx, fy] != null) return false;
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
                {
                    result[c, rows - 1 - r] = shape[r, c];
                }
                else
                {
                    result[cols - 1 - c, r] = shape[r, c];
                }

        return result;
    }
}
