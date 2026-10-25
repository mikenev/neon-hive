namespace NeonHive {
  public enum BugKind { Mite, Wasp, Spinner }
  public enum PowerKind { None, Spread, Laser, Rapid, Homing }

  /// <summary>One authored wave: `count` bugs of `bug`, one every `interval` s, starting `at` s into the level, around a
  /// random base angle ± `spread` degrees.</summary>
  public struct Wave {
    public float at, interval, spread; public BugKind bug; public int count;
    public Wave(float at, BugKind bug, int count, float interval, float spread = 60f){ this.at = at; this.bug = bug; this.count = count; this.interval = interval; this.spread = spread; }
  }
  public class LevelDef { public string name; public float curve, twist, bugSpeed = 1f; public Wave[] waves; }

  /// <summary>Ion Drift (theme 1): four authored levels, curves gentle → stronger (and a little twist late), then the
  /// Prism Moth. Data only; Game runs it.</summary>
  public static class IonDrift {
    public const string Theme = "Ion Drift";
    static Wave W(float at, BugKind b, int n, float iv, float sp = 60f) => new Wave(at, b, n, iv, sp);
    const BugKind M = BugKind.Mite, Wa = BugKind.Wasp, Sp = BugKind.Spinner;
    public static readonly LevelDef[] Levels = {
      new LevelDef{ name = "1  Drift Gate", curve = 0.8f, twist = 0f, waves = new[]{
        W(1, M, 6, 0.8f), W(7, M, 8, 0.55f), W(14, Wa, 3, 1.0f), W(19, M, 10, 0.45f), W(26, Wa, 4, 0.8f), W(31, M, 12, 0.4f) } },
      new LevelDef{ name = "2  Static Reach", curve = 1.1f, twist = 0f, bugSpeed = 1.08f, waves = new[]{
        W(1, M, 8, 0.5f), W(6, Wa, 5, 0.7f), W(12, Sp, 1, 0f), W(15, M, 12, 0.4f), W(22, Wa, 6, 0.6f), W(28, Sp, 2, 2f), W(33, M, 14, 0.35f) } },
      new LevelDef{ name = "3  Arc Coil", curve = 1.4f, twist = 0.08f, bugSpeed = 1.3f, waves = new[]{
        W(1, Wa, 8, 0.5f), W(6, M, 16, 0.3f), W(11, Sp, 3, 1.2f), W(16, Wa, 10, 0.45f), W(23, M, 20, 0.25f), W(29, Sp, 4, 1.2f), W(35, Wa, 10, 0.4f) } },
      new LevelDef{ name = "4  Prism Run", curve = 1.8f, twist = 0.15f, bugSpeed = 1.42f, waves = new[]{
        W(1, M, 18, 0.26f), W(6, Sp, 3, 1f), W(10, Wa, 12, 0.35f), W(17, M, 22, 0.24f), W(23, Sp, 4, 1f), W(28, Wa, 12, 0.32f), W(34, M, 24, 0.22f), W(40, Sp, 4, 0.9f) } },
    };
  }
}
