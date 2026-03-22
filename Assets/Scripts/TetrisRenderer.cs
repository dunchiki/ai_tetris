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

    // Hold 表示用 Image リスト
    private readonly List<Image> _holdImages = new List<Image>();

    // Next 表示用 Image リスト（スロットごと）
    private readonly Transform[]    _nextAreas;
    private readonly List<Image>[]  _nextImagesList;

    private readonly Transform _holdArea;

    public TetrisRenderer(Transform fieldTransform, Transform holdArea, Transform[] nextAreas = null)
    {
        _field      = fieldTransform;
        _holdArea   = holdArea;
        _nextAreas  = nextAreas ?? new Transform[0];
        _nextImagesList = new List<Image>[_nextAreas.Length];
        for (int i = 0; i < _nextAreas.Length; i++)
            _nextImagesList[i] = new List<Image>();
    }

    // ── ミノ描画 ──────────────────────────────────────────────────

    /// <summary>操作中ミノを再描画する。</summary>
    public void DrawMino(Vector2Int[] cells, Color color, int posX, int posY)
    {
        ClearMino();
        foreach (var c in cells)
            _minoImages.Add(CreateImage(posX + c.x, posY + c.y, color));
    }

    /// <summary>操作中ミノの Image を全て破棄する。</summary>
    public void ClearMino()
    {
        foreach (var img in _minoImages)
            if (img != null) DestroyObject(img.gameObject);
        _minoImages.Clear();
    }

    // ── グリッドブロック操作 ───────────────────────────────────────

    /// <summary>指定セルに固定ブロックの Image を生成する。</summary>
    public void PlaceBlock(int col, int row, Color color)
    {
        if (_blockImages[col, row] != null)
            DestroyObject(_blockImages[col, row].gameObject);
        _blockImages[col, row] = CreateImage(col, row, color);
    }

    /// <summary>指定セルの固定ブロック Image を破棄する。</summary>
    public void RemoveBlock(int col, int row)
    {
        if (_blockImages[col, row] == null) return;
        DestroyObject(_blockImages[col, row].gameObject);
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

    /// <summary>全ての Image (ミノ・グリッド・Hold・Next) を破棄する。</summary>
    public void ClearAll()
    {
        ClearMino();
        ClearHold();
        ClearNext();
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

    // ── Hold 表示 ──────────────────────────────────────────────────

    /// <summary>Hold ミノを HoldArea 中央に描画する。</summary>
    public void DrawHold(Vector2Int[] cells, Color color)
    {
        ClearHold();
        if (_holdArea == null) return;
        int maxX = 0, maxY = 0;
        foreach (var c in cells) { maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y); }
        int cols = maxX + 1, rows = maxY + 1;
        float ox = -(cols * TetrisConfig.CELL_SIZE) * 0.5f + TetrisConfig.CELL_SIZE * 0.5f;
        float oy =  (rows * TetrisConfig.CELL_SIZE) * 0.5f - TetrisConfig.CELL_SIZE * 0.5f;
        foreach (var c in cells)
            _holdImages.Add(CreateHoldImage(c.x, c.y, ox, oy, color));
    }
    /// <summary>Hold 表示を消去する。</summary>
    public void ClearHold()
    {
        foreach (var img in _holdImages)
            if (img != null) DestroyObject(img.gameObject);
        _holdImages.Clear();
    }

    // ── Next 表示 ──────────────────────────────────────────────────

    /// <summary>Next キューの内容を各 NextArea スロットに一括描画する。</summary>
    public void DrawNextQueue(int[] shapeIndices)
    {
        for (int slot = 0; slot < _nextAreas.Length; slot++)
        {
            ClearNextSlot(slot);
            if (slot < shapeIndices.Length)
                DrawNextSlot(slot, TetrisGame.GetCells(shapeIndices[slot]),
                                   TetrisConfig.COLORS[shapeIndices[slot]]);
        }
    }

    /// <summary>全 Next スロットの表示を消去する。</summary>
    public void ClearNext()
    {
        for (int i = 0; i < _nextImagesList.Length; i++)
            ClearNextSlot(i);
    }

    private void ClearNextSlot(int slot)
    {
        foreach (var img in _nextImagesList[slot])
            if (img != null) DestroyObject(img.gameObject);
        _nextImagesList[slot].Clear();
    }

    private void DrawNextSlot(int slot, Vector2Int[] cells, Color color)
    {
        var area = _nextAreas[slot];
        if (area == null) return;
        int maxX = 0, maxY = 0;
        foreach (var c in cells) { maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y); }
        int cols = maxX + 1, rows = maxY + 1;
        float ox = -(cols * TetrisConfig.CELL_SIZE) * 0.5f + TetrisConfig.CELL_SIZE * 0.5f;
        float oy =  (rows * TetrisConfig.CELL_SIZE) * 0.5f - TetrisConfig.CELL_SIZE * 0.5f;
        foreach (var c in cells)
            _nextImagesList[slot].Add(CreateNextImage(area, c.x, c.y, ox, oy, color));
    }

    private Image CreateNextImage(Transform parent, int col, int row, float ox, float oy, Color color)
    {
        var go = new GameObject($"Next_{col}_{row}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(TetrisConfig.CELL_SIZE - 1f, TetrisConfig.CELL_SIZE - 1f);
        rt.anchoredPosition = new Vector2(ox + col * TetrisConfig.CELL_SIZE,
                                          oy - row * TetrisConfig.CELL_SIZE);

        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Image CreateHoldImage(int col, int row, float ox, float oy, Color color)
    {
        var go = new GameObject($"Hold_{col}_{row}");
        go.transform.SetParent(_holdArea, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(TetrisConfig.CELL_SIZE - 1f, TetrisConfig.CELL_SIZE - 1f);
        rt.anchoredPosition = new Vector2(ox + col * TetrisConfig.CELL_SIZE,
                                          oy - row * TetrisConfig.CELL_SIZE);

        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>
    /// Edit Mode では DestroyImmediate、Play Mode では Destroy を使う。
    /// </summary>
    private static void DestroyObject(Object obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
            return;
        }
#endif
        Object.Destroy(obj);
    }
}
