using UnityEngine;
namespace NeonHive {
  public enum BugKind { Mite, Wasp, Spinner, Beetle }
  public enum PowerKind { None, Spread, Laser, Rapid, Homing }

  /// <summary>One authored wave: `count` bugs of `bug`, one every `interval` s, starting `at` s into the level, around a
  /// random base angle ± `spread` degrees.</summary>
  public struct Wave {
    public float at, interval, spread; public BugKind bug; public int count;
    public Wave(float at, BugKind bug, int count, float interval, float spread = 60f){ this.at = at; this.bug = bug; this.count = count; this.interval = interval; this.spread = spread; }
  }
  public class LevelDef { public string name; public float curve, twist, bugSpeed = 1f; public Wave[] waves; }

  /// <summary>A theme's boss on the shared boss framework (Game.BossTick). The pattern cycle is fan, fan, extra
  /// (`extra`: 0 = Mite ring, 1 = brood ring of Mites + Wasps, 2 = radial bullet burst then a Mite ring). Values are the
  /// normal / enraged (below 50% HP) pair.</summary>
  public class BossDef {
    public string name; public float hp;
    public int fanN, fanNRage; public float fanDeg, fanDegRage, fanCd, fanCdRage;
    public int ringN, ringNRage, wasps; public float ringCd, ringCdRage;
    public int extra, burstN, burstNRage;
  }

  /// <summary>A theme: its four levels, boss and look (ring shape index into Tunnel's meshes, colours).</summary>
  public class ThemeDef {
    public string name; public LevelDef[] levels; public BossDef boss;
    public Color primary, secondary, accent, wasp, spinner, boss_; public float bugScale = 1f;
  }

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
    public static readonly BossDef Boss = new BossDef{ name = "Prism Moth", hp = 480f,
      fanN = 3, fanNRage = 5, fanDeg = 24f, fanDegRage = 36f, fanCd = 2.9f, fanCdRage = 2.4f, ringN = 3, ringNRage = 4, ringCd = 3.2f, ringCdRage = 2.6f };
  }

  /// <summary>Acid Hive (theme 2, hex tunnel): continues from the tuned L4 (curve 1.8, ×1.42, 99 bugs); adds the armoured
  /// Beetle (6 hp). Boss: Hive Queen.</summary>
  public static class AcidHive {
    static Wave W(float at, BugKind b, int n, float iv, float sp = 60f) => new Wave(at, b, n, iv, sp);
    const BugKind M = BugKind.Mite, Wa = BugKind.Wasp, Sp = BugKind.Spinner, Be = BugKind.Beetle;
    public static readonly LevelDef[] Levels = {
      new LevelDef{ name = "5  Comb Gate", curve = 1.9f, twist = 0.18f, bugSpeed = 1.44f, waves = new[]{
        W(1, M, 18, 0.26f), W(6, Be, 2, 1.5f), W(10, Wa, 12, 0.34f), W(16, M, 22, 0.24f), W(22, Sp, 4, 1f), W(27, Be, 3, 1.2f), W(31, Wa, 12, 0.32f), W(37, M, 24, 0.22f), W(43, Sp, 3, 0.9f) } },
      new LevelDef{ name = "6  Brood Lanes", curve = 2.0f, twist = 0.21f, bugSpeed = 1.47f, waves = new[]{
        W(1, Wa, 12, 0.32f), W(6, M, 22, 0.24f), W(12, Be, 4, 1.1f), W(17, Sp, 4, 0.9f), W(21, M, 24, 0.22f), W(27, Wa, 14, 0.3f), W(33, Be, 4, 1f), W(38, M, 20, 0.22f) } },
      new LevelDef{ name = "7  Wax Spiral", curve = 2.1f, twist = 0.24f, bugSpeed = 1.5f, waves = new[]{
        W(1, M, 24, 0.22f), W(6, Be, 4, 1f), W(10, Sp, 5, 0.9f), W(15, Wa, 14, 0.3f), W(21, M, 24, 0.21f), W(27, Be, 5, 0.9f), W(32, Wa, 14, 0.28f), W(38, Sp, 4, 0.8f), W(42, M, 18, 0.2f) } },
      new LevelDef{ name = "8  Queen's Hall", curve = 2.2f, twist = 0.27f, bugSpeed = 1.53f, waves = new[]{
        W(1, Wa, 14, 0.3f), W(6, M, 26, 0.21f), W(12, Be, 5, 0.9f), W(16, Sp, 5, 0.8f), W(21, M, 26, 0.2f), W(27, Wa, 16, 0.27f), W(33, Be, 6, 0.8f), W(38, Sp, 4, 0.8f), W(42, M, 18, 0.2f) } },
    };
    public static readonly BossDef Boss = new BossDef{ name = "Hive Queen", hp = 560f,
      fanN = 3, fanNRage = 5, fanDeg = 26f, fanDegRage = 38f, fanCd = 2.7f, fanCdRage = 2.25f, ringN = 3, ringNRage = 4, wasps = 1, ringCd = 3.0f, ringCdRage = 2.5f, extra = 1 };
  }

  /// <summary>Redline Core (theme 3, square tunnel): the wildest curves and twist. Boss: Core Scarab.</summary>
  public static class RedlineCore {
    static Wave W(float at, BugKind b, int n, float iv, float sp = 60f) => new Wave(at, b, n, iv, sp);
    const BugKind M = BugKind.Mite, Wa = BugKind.Wasp, Sp = BugKind.Spinner, Be = BugKind.Beetle;
    public static readonly LevelDef[] Levels = {
      new LevelDef{ name = "9  Redline Gate", curve = 2.3f, twist = 0.3f, bugSpeed = 1.55f, waves = new[]{
        W(1, M, 26, 0.21f), W(6, Sp, 5, 0.8f), W(10, Wa, 16, 0.27f), W(16, Be, 5, 0.9f), W(21, M, 26, 0.2f), W(27, Wa, 16, 0.26f), W(33, Sp, 5, 0.8f), W(38, Be, 4, 0.9f), W(42, M, 18, 0.2f) } },
      new LevelDef{ name = "10 Overclock", curve = 2.4f, twist = 0.33f, bugSpeed = 1.57f, waves = new[]{
        W(1, Wa, 16, 0.27f), W(6, M, 28, 0.2f), W(12, Be, 6, 0.8f), W(16, Sp, 6, 0.75f), W(21, M, 26, 0.2f), W(27, Wa, 16, 0.25f), W(33, Be, 5, 0.8f), W(38, M, 22, 0.19f) } },
      new LevelDef{ name = "11 Heat Sink", curve = 2.5f, twist = 0.37f, bugSpeed = 1.59f, waves = new[]{
        W(1, M, 28, 0.2f), W(6, Be, 6, 0.8f), W(10, Sp, 6, 0.75f), W(15, Wa, 18, 0.25f), W(21, M, 28, 0.19f), W(27, Be, 6, 0.75f), W(32, Wa, 16, 0.24f), W(38, Sp, 5, 0.7f), W(42, M, 16, 0.19f) } },
      new LevelDef{ name = "12 Core Breach", curve = 2.6f, twist = 0.4f, bugSpeed = 1.62f, waves = new[]{
        W(1, Wa, 18, 0.25f), W(6, M, 30, 0.19f), W(12, Be, 7, 0.75f), W(16, Sp, 6, 0.7f), W(21, M, 28, 0.19f), W(27, Wa, 18, 0.24f), W(33, Be, 6, 0.75f), W(38, Sp, 5, 0.7f), W(42, M, 14, 0.19f) } },
    };
    public static readonly BossDef Boss = new BossDef{ name = "Core Scarab", hp = 640f,
      fanN = 4, fanNRage = 5, fanDeg = 27f, fanDegRage = 36f, fanCd = 2.6f, fanCdRage = 2.2f, ringN = 3, ringNRage = 4, ringCd = 3.0f, ringCdRage = 2.5f, extra = 2, burstN = 6, burstNRage = 8 };
  }

  public static class Themes {
    static Color H(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    public static readonly ThemeDef[] All = {
      new ThemeDef{ name = IonDrift.Theme, levels = IonDrift.Levels, boss = IonDrift.Boss,
        primary = H(0x00F0FF), secondary = H(0xFF2BD6), accent = H(0x3D7BFF), wasp = H(0x9DFF00), spinner = H(0xFF2E4D), boss_ = H(0xFF2BD6) },
      new ThemeDef{ name = "Acid Hive", levels = AcidHive.Levels, boss = AcidHive.Boss, bugScale = 1.1f,
        primary = H(0x9DFF00), secondary = H(0xFFB300), accent = H(0x00FFA3), wasp = H(0x00FFA3), spinner = H(0xFF2E4D), boss_ = H(0xFFB300) },
      new ThemeDef{ name = "Redline Core", levels = RedlineCore.Levels, boss = RedlineCore.Boss, bugScale = 1.18f,
        primary = H(0xFF2E4D), secondary = H(0xA15CFF), accent = H(0xFFE14D), wasp = H(0xA15CFF), spinner = H(0x00F0FF), boss_ = H(0xFFE14D) },
    };
  }
}
