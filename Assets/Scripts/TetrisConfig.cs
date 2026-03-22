using UnityEngine;

/// <summary>
/// テトリスゲーム全体で共有するパラメータ／データ定義。
/// </summary>
public static class TetrisConfig
{
    // ── フィールドサイズ ───────────────────────────────────────────
    public const int   COLS      = 10;
    public const int   ROWS      = 20;
    public const float CELL_SIZE = 30f;

    // ── DAS (Delayed Auto Shift) ───────────────────────────────────
    /// <summary>長押し判定が始まるまでの時間(秒)</summary>
    public const float DAS_DELAY  = 0.15f;
    /// <summary>長押し中の入力繰り返し間隔(秒)</summary>
    public const float DAS_REPEAT = 0.05f;

    // ── ゲームループのデフォルト値 ────────────────────────────────
    public const float DEFAULT_FALL_INTERVAL = 1.0f;
    public const float DEFAULT_LOCK_DELAY    = 0.5f;

    // ── テトリミノ定義 ─────────────────────────────────────────────
    /// <summary>7種のテトリミノ形状。インデックスは COLORS と対応。[row, col]</summary>
    public static readonly int[][,] SHAPES = new int[][,]
    {
        new int[,] { {1,1,1,1} },           // I
        new int[,] { {1,1}, {1,1} },         // O
        new int[,] { {0,1,0}, {1,1,1} },     // T
        new int[,] { {0,1,1}, {1,1,0} },     // S
        new int[,] { {1,1,0}, {0,1,1} },     // Z
        new int[,] { {1,0,0}, {1,1,1} },     // J
        new int[,] { {0,0,1}, {1,1,1} },     // L
    };

    /// <summary>各テトリミノの色。インデックスは SHAPES と対応。</summary>
    public static readonly Color[] COLORS = new Color[]
    {
        new Color(0f,   0.9f, 1f),   // I シアン
        new Color(1f,   0.9f, 0f),   // O 黄
        new Color(0.6f, 0f,   1f),   // T 紫
        new Color(0f,   0.9f, 0f),   // S 緑
        new Color(1f,   0f,   0f),   // Z 赤
        new Color(0f,   0f,   1f),   // J 青
        new Color(1f,   0.5f, 0f),   // L オレンジ
    };
}
