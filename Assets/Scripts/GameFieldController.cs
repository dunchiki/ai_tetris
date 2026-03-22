using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーの入力を受け付け、TetrisGame・TetrisRenderer を統括する MonoBehaviour。
/// DAS (Delayed Auto Shift) を実装した操作受付と Unity ライフサイクル管理を担当する。
/// </summary>
public class GameFieldController : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float fallInterval = TetrisConfig.DEFAULT_FALL_INTERVAL;
    [SerializeField] private float lockDelay    = TetrisConfig.DEFAULT_LOCK_DELAY;

    [Header("Debug (Inspector で有効化)")]
    [SerializeField] private bool debugMode = false;

    private TetrisGame     _game;
    private TetrisRenderer _renderer;
    private GameObject     _gameStartPanel;

    // DAS タイマー (左・右・下)
    private float _dasLeft, _dasRight, _dasDown;

    // ── Unity ライフサイクル ───────────────────────────────────────

    private void Start()
    {
        var holdArea   = GameObject.Find("HoldArea")?.transform;

        // RightPanel 内の NextArea1, NextArea2, ... を順番に検索する
        var rightPanel  = GameObject.Find("RightPanel")?.transform;
        var nextAreaList = new List<Transform>();
        if (rightPanel != null)
        {
            int idx = 1;
            Transform t;
            while ((t = rightPanel.Find($"NextArea{idx}")) != null)
            {
                nextAreaList.Add(t);
                idx++;
            }
        }
        Transform[] nextAreas = nextAreaList.ToArray();

        _renderer = new TetrisRenderer(transform, holdArea, nextAreas);
        _game     = new TetrisGame(_renderer, fallInterval, lockDelay, nextAreas.Length);
        _gameStartPanel = GameObject.Find("GameStartPanel");

        _game.OnGameOver    += () => { if (_gameStartPanel != null) _gameStartPanel.SetActive(true); };
        _game.OnGameStarted += () => { if (_gameStartPanel != null) _gameStartPanel.SetActive(false); };

        var btn = GameObject.Find("GameStartButton")?.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(_game.StartGame);
    }

    private void Update()
    {
        // デバッグキー (Inspector の debugMode を ON にすると使用可)
        if (debugMode)
        {
            if (Input.GetKeyDown(KeyCode.C)) _game.SpawnRandomMino();
            if (Input.GetKeyDown(KeyCode.F)) _game.ForceLock();
        }

        if (!_game.IsPlaying || !_game.HasMino) return;

        // ── 操作キー ──────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.W)) _game.HardDrop();
        if (Input.GetKeyDown(KeyCode.Q)) _game.RotateMino(clockwise: false);
        if (Input.GetKeyDown(KeyCode.E)) _game.RotateMino(clockwise: true);
        if (Input.GetKeyDown(KeyCode.H)) _game.HoldMino();

        HandleDAS(KeyCode.A, -1, 0, ref _dasLeft);
        HandleDAS(KeyCode.D,  1, 0, ref _dasRight);
        HandleDAS(KeyCode.S,  0, 1, ref _dasDown);

        // ── タイマー更新（自動落下・固定遅延）────────────────────
        _game.Tick(Time.deltaTime);
    }

    // ── DAS (Delayed Auto Shift) ──────────────────────────────────
    // 初回 GetKeyDown で即反応し、長押しで DAS_DELAY 後から DAS_REPEAT 間隔で繰り返す。
    private void HandleDAS(KeyCode key, int dx, int dy, ref float timer)
    {
        if (Input.GetKeyDown(key))
        {
            _game.MoveMino(dx, dy);
            timer = -TetrisConfig.DAS_DELAY;  // 初回遅延をタイマーにセット
        }
        else if (Input.GetKey(key))
        {
            timer += Time.deltaTime;
            while (timer >= 0f)
            {
                _game.MoveMino(dx, dy);
                timer -= TetrisConfig.DAS_REPEAT;
            }
        }
        else
        {
            timer = -TetrisConfig.DAS_DELAY;  // キーが離されたらリセット
        }
    }
}