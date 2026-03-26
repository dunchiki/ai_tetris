using System;
using System.Collections.Generic;
using UnityEngine;

public enum LineClearType
{
    None = 0,
    Single = 1,
    Double = 2,
    Triple = 3,
    Tetris = 4,
}

public enum TSpinType
{
    None = 0,
    Mini = 1,
    MiniSingle = 2,
    Single = 3,
    Double = 4,
    Triple = 5,
}

[Serializable]
public class ScoringConfig
{
    public bool levelMultiplierEnabled = true;

    [Serializable]
    public class LineClearConfig
    {
        public int single = 100;
        public int @double = 300;
        public int triple = 500;
        public int tetris = 800;
    }

    [Serializable]
    public class TSpinConfig
    {
        public int mini = 100;
        public int miniSingle = 200;
        public int single = 800;
        public int @double = 1200;
        public int triple = 1600;
    }

    [Serializable]
    public class B2BConfig
    {
        public float multiplier = 1.5f;
        public bool enabled = true;
    }

    public enum ComboScaling
    {
        Linear,
        Table,
    }

    [Serializable]
    public class ComboConfig
    {
        public int baseScore = 50;
        public ComboScaling scaling = ComboScaling.Linear;
    }

    [Serializable]
    public class DropConfig
    {
        public int soft = 1;
        public int hard = 2;
    }

    [Serializable]
    public class PerfectClearConfig
    {
        public bool enabled = true;
        public int baseScore = 2000;
    }

    [Serializable]
    public class TSpinDetectionConfig
    {
        public bool use3CornerRule = true;
        public bool miniRequiresKick = true;
    }

    public LineClearConfig lineClear = new LineClearConfig();
    public TSpinConfig tSpin = new TSpinConfig();
    public B2BConfig b2b = new B2BConfig();
    public ComboConfig combo = new ComboConfig();
    public DropConfig drop = new DropConfig();
    public PerfectClearConfig perfectClear = new PerfectClearConfig();
    public TSpinDetectionConfig tSpinDetection = new TSpinDetectionConfig();

    public static ScoringConfig CreateDefault() => new ScoringConfig();
}

public readonly struct TSpinDetectionContext
{
    public readonly bool LastActionWasRotate;
    public readonly int OccupiedCornerCount;
    public readonly bool UsedWallKick;
    public readonly int ClearedLines;

    public TSpinDetectionContext(bool lastActionWasRotate, int occupiedCornerCount, bool usedWallKick, int clearedLines)
    {
        LastActionWasRotate = lastActionWasRotate;
        OccupiedCornerCount = occupiedCornerCount;
        UsedWallKick = usedWallKick;
        ClearedLines = clearedLines;
    }
}

public interface ITSpinDetector
{
    TSpinType Detect(TSpinDetectionContext context, ScoringConfig config);
}

public class ThreeCornerTSpinDetector : ITSpinDetector
{
    public TSpinType Detect(TSpinDetectionContext context, ScoringConfig config)
    {
        if (!context.LastActionWasRotate)
            return TSpinType.None;

        if (config.tSpinDetection.use3CornerRule && context.OccupiedCornerCount < 3)
            return TSpinType.None;

        if (context.ClearedLines == 0)
            return config.tSpinDetection.miniRequiresKick && context.UsedWallKick ? TSpinType.Mini : TSpinType.None;

        if (context.ClearedLines == 1)
        {
            if (config.tSpinDetection.miniRequiresKick && context.UsedWallKick)
                return TSpinType.MiniSingle;

            return TSpinType.Single;
        }

        if (context.ClearedLines == 2)
            return TSpinType.Double;

        if (context.ClearedLines == 3)
            return TSpinType.Triple;

        return TSpinType.None;
    }
}

public interface IComboBonusCalculator
{
    int CalculateBonus(int comboCount, int level, ScoringConfig config);
}

public class LinearComboBonusCalculator : IComboBonusCalculator
{
    public int CalculateBonus(int comboCount, int level, ScoringConfig config)
    {
        if (comboCount <= 0)
            return 0;

        int levelFactor = config.levelMultiplierEnabled ? level : 1;
        return config.combo.baseScore * comboCount * levelFactor;
    }
}

public class TableComboBonusCalculator : IComboBonusCalculator
{
    private readonly IReadOnlyList<int> _comboTable;

    public TableComboBonusCalculator(IReadOnlyList<int> comboTable)
    {
        _comboTable = comboTable ?? throw new ArgumentNullException(nameof(comboTable));
    }

    public int CalculateBonus(int comboCount, int level, ScoringConfig config)
    {
        if (comboCount <= 0)
            return 0;

        int index = Mathf.Min(comboCount - 1, _comboTable.Count - 1);
        int levelFactor = config.levelMultiplierEnabled ? level : 1;
        return _comboTable[index] * levelFactor;
    }
}

public interface ILevelProvider
{
    int ResolveLevel(int totalLines);
}

public class DefaultLevelProvider : ILevelProvider
{
    public int ResolveLevel(int totalLines)
    {
        return 1 + Mathf.FloorToInt(totalLines / 10f);
    }
}

public interface IB2BResetPolicy
{
    bool ShouldReset(bool clearedLine, bool currentActionIsB2BEligible);
}

public class DefaultB2BResetPolicy : IB2BResetPolicy
{
    public bool ShouldReset(bool clearedLine, bool currentActionIsB2BEligible)
    {
        if (!clearedLine)
            return true;

        return !currentActionIsB2BEligible;
    }
}

public readonly struct ScoreEvent
{
    public readonly int ClearedLines;
    public readonly bool IsPerfectClear;
    public readonly int SoftDropCells;
    public readonly int HardDropCells;
    public readonly TSpinDetectionContext TSpinContext;

    public ScoreEvent(int clearedLines, bool isPerfectClear, int softDropCells, int hardDropCells, TSpinDetectionContext tSpinContext)
    {
        ClearedLines = clearedLines;
        IsPerfectClear = isPerfectClear;
        SoftDropCells = softDropCells;
        HardDropCells = hardDropCells;
        TSpinContext = tSpinContext;
    }
}

public class ScoreState
{
    public int TotalScore { get; private set; }
    public int TotalClearedLines { get; private set; }
    public int Level { get; private set; } = 1;
    public int ComboCount { get; private set; }
    public bool IsBackToBackActive { get; private set; }

    public void AddScore(int scoreDelta)
    {
        TotalScore += scoreDelta;
    }

    public void AddClearedLines(int lines)
    {
        TotalClearedLines += lines;
    }

    public void SetLevel(int level)
    {
        Level = Mathf.Max(1, level);
    }

    public void SetComboCount(int comboCount)
    {
        ComboCount = Mathf.Max(0, comboCount);
    }

    public void SetBackToBack(bool isActive)
    {
        IsBackToBackActive = isActive;
    }
}

public readonly struct ScoreBreakdown
{
    public readonly int BaseScore;
    public readonly int B2BScore;
    public readonly int ComboScore;
    public readonly int PerfectClearScore;
    public readonly int DropScore;
    public readonly int TotalScore;
    public readonly TSpinType TSpinType;

    public ScoreBreakdown(int baseScore, int b2bScore, int comboScore, int perfectClearScore, int dropScore, int totalScore, TSpinType tSpinType)
    {
        BaseScore = baseScore;
        B2BScore = b2bScore;
        ComboScore = comboScore;
        PerfectClearScore = perfectClearScore;
        DropScore = dropScore;
        TotalScore = totalScore;
        TSpinType = tSpinType;
    }
}

public class TetrisScoringService
{
    private readonly ScoringConfig _config;
    private readonly ITSpinDetector _tSpinDetector;
    private readonly IComboBonusCalculator _comboCalculator;
    private readonly ILevelProvider _levelProvider;
    private readonly IB2BResetPolicy _b2bResetPolicy;

    public TetrisScoringService(
        ScoringConfig config,
        ITSpinDetector tSpinDetector = null,
        IComboBonusCalculator comboCalculator = null,
        ILevelProvider levelProvider = null,
        IB2BResetPolicy b2bResetPolicy = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _tSpinDetector = tSpinDetector ?? new ThreeCornerTSpinDetector();
        _comboCalculator = comboCalculator ?? CreateComboCalculator(config.combo);
        _levelProvider = levelProvider ?? new DefaultLevelProvider();
        _b2bResetPolicy = b2bResetPolicy ?? new DefaultB2BResetPolicy();
    }

    private static IComboBonusCalculator CreateComboCalculator(ScoringConfig.ComboConfig comboConfig)
    {
        if (comboConfig.scaling == ScoringConfig.ComboScaling.Table)
            return new TableComboBonusCalculator(new[] { 0, 50, 100, 150, 200, 250, 300, 400, 500, 600 });

        return new LinearComboBonusCalculator();
    }

    public ScoreBreakdown ApplyScore(ScoreState state, ScoreEvent scoreEvent)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        state.AddClearedLines(scoreEvent.ClearedLines);
        state.SetLevel(_levelProvider.ResolveLevel(state.TotalClearedLines));

        int levelFactor = _config.levelMultiplierEnabled ? state.Level : 1;
        TSpinType tSpinType = _tSpinDetector.Detect(scoreEvent.TSpinContext, _config);
        bool lineCleared = scoreEvent.ClearedLines > 0;

        int baseScore = ResolveBaseScore(scoreEvent.ClearedLines, tSpinType) * levelFactor;

        bool b2bEligible = IsB2BEligible(scoreEvent.ClearedLines, tSpinType);
        int b2bScore = baseScore;
        if (_config.b2b.enabled && b2bEligible && state.IsBackToBackActive)
            b2bScore = Mathf.RoundToInt(baseScore * _config.b2b.multiplier);

        if (_b2bResetPolicy.ShouldReset(lineCleared, b2bEligible))
            state.SetBackToBack(false);
        else if (b2bEligible)
            state.SetBackToBack(true);

        if (lineCleared)
            state.SetComboCount(state.ComboCount + 1);
        else
            state.SetComboCount(0);

        int comboScore = _comboCalculator.CalculateBonus(state.ComboCount, state.Level, _config);
        int perfectClearScore = 0;
        if (_config.perfectClear.enabled && scoreEvent.IsPerfectClear)
            perfectClearScore = _config.perfectClear.baseScore * levelFactor;

        int dropScore = (scoreEvent.SoftDropCells * _config.drop.soft) + (scoreEvent.HardDropCells * _config.drop.hard);

        int total = b2bScore + comboScore + perfectClearScore + dropScore;
        state.AddScore(total);

        return new ScoreBreakdown(baseScore, b2bScore, comboScore, perfectClearScore, dropScore, total, tSpinType);
    }

    private int ResolveBaseScore(int clearedLines, TSpinType tSpinType)
    {
        if (tSpinType != TSpinType.None)
            return ResolveTSpinScore(tSpinType);

        LineClearType lineClearType = (LineClearType)Mathf.Clamp(clearedLines, 0, 4);
        return lineClearType switch
        {
            LineClearType.Single => _config.lineClear.single,
            LineClearType.Double => _config.lineClear.@double,
            LineClearType.Triple => _config.lineClear.triple,
            LineClearType.Tetris => _config.lineClear.tetris,
            _ => 0,
        };
    }

    private int ResolveTSpinScore(TSpinType tSpinType)
    {
        return tSpinType switch
        {
            TSpinType.Mini => _config.tSpin.mini,
            TSpinType.MiniSingle => _config.tSpin.miniSingle,
            TSpinType.Single => _config.tSpin.single,
            TSpinType.Double => _config.tSpin.@double,
            TSpinType.Triple => _config.tSpin.triple,
            _ => 0,
        };
    }

    private static bool IsB2BEligible(int clearedLines, TSpinType tSpinType)
    {
        if (tSpinType is TSpinType.Single or TSpinType.Double or TSpinType.Triple or TSpinType.MiniSingle)
            return true;

        return clearedLines == 4;
    }
}
