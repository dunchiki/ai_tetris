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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))          SpawnRandomMino();
        if (Input.GetKeyDown(KeyCode.F))          LockMino();
        if (Input.GetKeyDown(KeyCode.LeftArrow))  MoveMino(-1, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow)) MoveMino( 1, 0);
        if (Input.GetKeyDown(KeyCode.DownArrow))  MoveMino( 0, 1);
        if (Input.GetKeyDown(KeyCode.UpArrow))    RotateMino();
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
    /// ランダムなテトリミノを上部中央に生成する。(C キー)
    /// </summary>
    public void SpawnRandomMino()
    {
        int idx = Random.Range(0, SHAPES.Length);
        PlaceMino(SHAPES[idx], COLORS[idx]);
    }

    /// <summary>
    /// 操作中のテトリミノを移動する。dx: 横変位, dy: 縦変位（下が正）
    /// 矢印キーで呼ばれる。
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
        }
    }

    /// <summary>
    /// 操作中のテトリミノを時計回りに90度回転する。(↑ キー)
    /// </summary>
    public void RotateMino()
    {
        if (currentShape == null) return;
        var rotated = RotateShape(currentShape);
        if (IsValidPosition(rotated, currentX, currentY))
        {
            currentShape = rotated;
            DrawCurrentMino();
        }
    }

    /// <summary>
    /// 操作中のテトリミノをフィールドに固定する。(F キー)
    /// </summary>
    public void LockMino()
    {
        if (currentShape == null) return;
        int rows = currentShape.GetLength(0), cols = currentShape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (currentShape[r, c] == 1)
                    PlaceBlock(currentX + c, currentY + r, currentColor);

        ClearCurrentBlockImages();
        currentShape = null;
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

    private static int[,] RotateShape(int[,] shape)
    {
        int rows = shape.GetLength(0), cols = shape.GetLength(1);
        var result = new int[cols, rows];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                result[c, rows - 1 - r] = shape[r, c];
        return result;
    }
}
