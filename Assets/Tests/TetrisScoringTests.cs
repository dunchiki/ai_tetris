using NUnit.Framework;

public class TetrisScoringTests
{
    [Test]
    public void BackToBack_ContinuesOnConsecutiveEligibleActions_AndBreaksOnSingle()
    {
        var service = new TetrisScoringService(ScoringConfig.CreateDefault());
        var state = new ScoreState();

        var first = service.ApplyScore(
            state,
            new ScoreEvent(
                clearedLines: 4,
                isPerfectClear: false,
                softDropCells: 0,
                hardDropCells: 0,
                tSpinContext: new TSpinDetectionContext(false, 0, false, 4)));

        var second = service.ApplyScore(
            state,
            new ScoreEvent(
                clearedLines: 4,
                isPerfectClear: false,
                softDropCells: 0,
                hardDropCells: 0,
                tSpinContext: new TSpinDetectionContext(false, 0, false, 4)));

        Assert.AreEqual(800, first.B2BScore, "初回テトリスは B2B 倍率が乗らないはず");
        Assert.AreEqual(1200, second.B2BScore, "2回目テトリスは B2B 倍率 1.5 が乗るはず");
        Assert.IsTrue(state.IsBackToBackActive, "連続 B2B 対象後は B2B 継続中のはず");

        service.ApplyScore(
            state,
            new ScoreEvent(
                clearedLines: 1,
                isPerfectClear: false,
                softDropCells: 0,
                hardDropCells: 0,
                tSpinContext: new TSpinDetectionContext(false, 0, false, 1)));

        Assert.IsFalse(state.IsBackToBackActive, "通常シングル消去で B2B は途切れるはず");
    }

    [Test]
    public void Combo_IncreasesAcrossConsecutiveLineClears()
    {
        var service = new TetrisScoringService(ScoringConfig.CreateDefault());
        var state = new ScoreState();

        var first = service.ApplyScore(
            state,
            new ScoreEvent(1, false, 0, 0, new TSpinDetectionContext(false, 0, false, 1)));

        var second = service.ApplyScore(
            state,
            new ScoreEvent(1, false, 0, 0, new TSpinDetectionContext(false, 0, false, 1)));

        var third = service.ApplyScore(
            state,
            new ScoreEvent(1, false, 0, 0, new TSpinDetectionContext(false, 0, false, 1)));

        Assert.AreEqual(50, first.ComboScore, "1連目コンボボーナスは 50 のはず");
        Assert.AreEqual(100, second.ComboScore, "2連目コンボボーナスは 100 のはず");
        Assert.AreEqual(150, third.ComboScore, "3連目コンボボーナスは 150 のはず");
        Assert.AreEqual(3, state.ComboCount, "連続3回ライン消去後のコンボ数は 3 のはず");
    }

    [Test]
    public void TSpinDetection_BoundaryCases_MiniAndNone()
    {
        var detector = new ThreeCornerTSpinDetector();
        var config = ScoringConfig.CreateDefault();

        var miniSingle = detector.Detect(
            new TSpinDetectionContext(
                lastActionWasRotate: true,
                occupiedCornerCount: 3,
                usedWallKick: true,
                clearedLines: 1),
            config);

        var notTSpin = detector.Detect(
            new TSpinDetectionContext(
                lastActionWasRotate: true,
                occupiedCornerCount: 2,
                usedWallKick: true,
                clearedLines: 1),
            config);

        Assert.AreEqual(TSpinType.MiniSingle, miniSingle, "3コーナー + キックありの1ライン消去は MiniSingle 判定のはず");
        Assert.AreEqual(TSpinType.None, notTSpin, "3コーナー未満では T スピンにならないはず");
    }
}
