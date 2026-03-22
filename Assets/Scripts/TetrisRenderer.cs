using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// テトリスフィールドの画面描画を担当するクラス。
/// ブロック Image の生成・移動・破棄を管理する。
/// </summary>
public class TetrisRenderer
{
    private readonly Transform _field;

    // 固定済みグリッドブロックの Image[col, row]
    private readonly Image[,] _blockImages =
        new Image[TetrisConfig.COLS, TetrisConfig.ROWS];

    // 現在操作中ミノの Image リスト
    private readonly List<Image> _minoImages = new List<Image>();

    public TetrisRenderer(Transform fieldTransform) => _field = fieldTransform;

    // ── ミノ描画 ──────────────────────────────────────────────────

    /// <summary>操作中ミノを再描画する。</summary>
    public void DrawMino(int[,] shape, Color color, int posX, int posY)
    {
        ClearMino();
        int rows = shape.GetLength(0), cols = shape.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (shape[r, c] == 1)
                    _minoImages.Add(CreateImage(posX + c, posY + r, color));
    }

    /// <summary>操作中ミノの Image を全て破棄する。</summary>
    public void ClearMino()
    {
        foreach (var img in _minoImages)
            if (img != null) Object.Destroy(img.gameObject);
        _minoImages.Clear();
    }

    // ── グリッドブロック操作 ───────────────────────────────────────

    /// <summary>指定セルに固定ブロックの Image を生成する。</summary>
    public void PlaceBlock(int col, int row, Color color)
    {
        if (_blockImages[col, row] != null)
            Object.Destroy(_blockImages[col, row].gameObject);
        _blockImages[col, row] = CreateImage(col, row, color);
    }

    /// <summary>指定セルの固定ブロック Image を破棄する。</summary>
    public void RemoveBlock(int col, int row)
    {
        if (_blockImages[col, row] == null) return;
        Object.Destroy(_blockImages[col, row].gameObject);
        _blockImages[col, row] = null;
    }

    /// <summary>ブロック Image を fromRow から toRow へ1段落下させる。</summary>
    public void DropBlock(int col, int fromRow, int toRow)
    {
        if (_blockImages[col, fromRow] == null) return;
        _blockImages[col, toRow]   = _blockImages[col, fromRow];
        _blockImages[col, fromRow] = null;
        _blockImages[col, toRow].GetComponent<RectTransform>().anchoredPosition
            = FieldToLocalPos(col, toRow);
    }

    /// <summary>全ての Image (ミノ・グリッド) を破棄する。</summary>
    public void ClearAll()
    {
        ClearMino();
        for (int col = 0; col < TetrisConfig.COLS; col++)
            for (int row = 0; row < TetrisConfig.ROWS; row++)
                RemoveBlock(col, row);
    }

    // ── ユーティリティ ─────────────────────────────────────────────

    private Image CreateImage(int col, int row, Color color)
    {
        var go = new GameObject($"Block_{col}_{row}");
        go.transform.SetParent(_field, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(TetrisConfig.CELL_SIZE - 1f, TetrisConfig.CELL_SIZE - 1f);
        rt.anchoredPosition = FieldToLocalPos(col, row);

        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>フィールド座標 → GameField ローカル UI 座標 (中心原点)。</summary>
    public static Vector2 FieldToLocalPos(int col, int row)
    {
        float ox = -(TetrisConfig.COLS * TetrisConfig.CELL_SIZE) * 0.5f + TetrisConfig.CELL_SIZE * 0.5f;
        float oy =  (TetrisConfig.ROWS * TetrisConfig.CELL_SIZE) * 0.5f - TetrisConfig.CELL_SIZE * 0.5f;
        return new Vector2(ox + col * TetrisConfig.CELL_SIZE, oy - row * TetrisConfig.CELL_SIZE);
    }
}
