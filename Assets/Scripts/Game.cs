using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeonHive {
  public enum GameState { Ready, Playing, Paused, GameOver, ThemeClear }

  /// <summary>Neon Hive core loop (M2: the Ion Drift theme start to finish). Everything lives in tunnel space: s = distance
  /// along the Track, ang = degrees around the axis (-90 = bottom). The ship (billboard) circles the inner wall at shipR
  /// and fires bolts down the tunnel. Four authored levels (IonDrift.Levels: timed waves of Mites, Wasps and Spinners, the
  /// curve / twist per level), each ending in a level clear, then the Prism Moth boss (bullet fans + Mite spawns) and a
  /// Theme clear screen. Power-ups (spread / laser / rapid / homing) drop from bugs and last PowerTime or until a life is
  /// lost. Score per bug + boss bonus, an extra life every ExtraLifeEvery points, a local high score (PlayerPrefs).
  /// Input (new Input System, devices read directly): keys A/D or arrows, Space, Esc; gamepad stick / d-pad, A or RT, Start;
  /// touch: drag to turn, auto-fire while touching, on-screen pause button. Start / resume / restart inputs never fire.</summary>
  public class Game : MonoBehaviour {
    public static Game I;
    [Header("Refs (set by BuildWeb.SetupScene)")]
    public Tunnel tunnel; public Camera cam; public Material spriteMat, miteMat;
    public Sprite shipSprite, miteSprite, waspSprite, spinnerSprite, bossSprite, bulletSprite, enemyBulletSprite, glowSprite;
    public Sprite spreadSprite, laserSprite, rapidSprite, homingSprite; public Sprite[] explosion, spark;
    public Texture2D iconLife, iconScore, iconPause, logo;
    [Header("Tuning")]
    public float speed = 14f, shipR = 4.1f, turnSpeed = 170f, camBack = 9f, lookAhead = 22f;
    public float fireInterval = 0.13f, boltSpeed = 62f, boltLife = 1.3f, spawnAhead = 75f;
    public int startLives = 3, extraLifeEvery = 10000; public float invulnTime = 1.6f, hitDs = 1.1f, hitArc = 1.05f, powerTime = 12f;
    public float bossMaxHp = 480f, bossDist = 26f;
    public Color shipTint = Color.white, uiTint = new Color32(0x00, 0xF0, 0xFF, 255), boltTint = new Color32(0xFF, 0x2B, 0xD6, 255), fxTint = new Color32(0x3D, 0x7B, 0xFF, 255);
    public Color miteTint = new Color32(0xFF, 0xE1, 0x4D, 255), waspTint = new Color32(0x9D, 0xFF, 0x00, 255), spinnerTint = new Color32(0xFF, 0x2E, 0x4D, 255);
    public Color enemyBulletTint = new Color32(0xFF, 0x8A, 0x3D, 255), bossTint = new Color32(0xFF, 0x2B, 0xD6, 255);

    public GameState State { get; private set; } = GameState.Ready;
    public int Score { get; private set; } public int Lives { get; private set; } public int High { get; private set; }
    public float S { get; private set; } public float Ang { get; private set; } = -90f;
    public PowerKind Power { get; private set; }
    enum Phase { Intro, Waves, Clear, BossIntro, Boss, BossDying }
    Phase phase; int level; float phaseT, levelTime, powerLeft; int nextExtra; bool newHigh;
    class Bug { public Transform t; public SpriteRenderer sr; public BugKind kind; public float s, ang, speed, hp, age, phase, holdT, fireCd; public bool held; }
    class Bolt { public Transform t; public float s, ang, age, speed, dmg; public bool pierce, homing; public List<Bug> hit; }
    class EBullet { public Transform t; public float s, ang, r0, s0, sEnd; }
    class Pickup { public Transform t, glow; public Vector3 glowScale; public PowerKind kind; public float s, ang, age; }
    class Fx { public Transform t; public SpriteRenderer sr; public Sprite[] frames; public float age, fps, s, ang, r; }
    readonly List<Bug> bugs = new(); readonly List<Bolt> bolts = new(); readonly List<EBullet> ebullets = new();
    readonly List<Pickup> pickups = new(); readonly List<Fx> fxs = new();
    readonly List<(float t, BugKind kind, float ang)> pending = new();
    readonly HashSet<string> once = new();
    Transform ship; SpriteRenderer shipSr; Transform boss; SpriteRenderer bossSr;
    float bossHp, bossS, bossAge, bossCd, bossFlash, bossTime; int bossPattern; bool bossAlive;
    float fireCd, invuln, overAt, touchMoved; int shots; bool touching;
    bool touchConsumed;   // this touch started/ended a screen or hit the pause button: no turning or auto-fire until released
    bool holdFire;        // after start / restart / resume: no fire until every fire input has been released
    static Sprite fallback;
    const string HighKey = "nh_highscore";

    void Awake(){ I = this; Lives = startLives; High = PlayerPrefs.GetInt(HighKey, 0); }
    void Start(){
      ship = MakeSprite("Ship", shipSprite, shipTint, 1.4f, out shipSr).transform;
      boss = MakeSprite("PrismMoth", bossSprite, bossTint, 8f, out bossSr).transform; boss.gameObject.SetActive(false);
      S = 0f; Place(); Debug.Log($"[NH] ready high={High} levels={IonDrift.Levels.Length}");
    }
    void LogOnce(string key, string msg){ if (once.Add(key)) Debug.Log(msg); }

    GameObject MakeSprite(string n, Sprite sp, Color c, float size, out SpriteRenderer sr){
      if (!sp){   // missing art: a plain glowing square instead of nothing
        if (!fallback) fallback = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        sp = fallback; size *= 0.4f;
      }
      var go = new GameObject(n); sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sharedMaterial = spriteMat; sr.color = c;
      float w = Mathf.Max(sp.bounds.size.x, sp.bounds.size.y); go.transform.localScale = Vector3.one * size / w; return go;
    }
    /// <summary>Billboard facing the camera, rolled so the sprite's +Y points at the tunnel centre.</summary>
    Quaternion Bill(float ang) => cam.transform.rotation * Quaternion.Euler(0, 0, ang + 90f);
    bool BotOn =>
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      bot;
#else
      false;
#endif

    // ---------------- input ----------------
    float MoveAxis(){
      float m = 0f; var kb = Keyboard.current; var gp = Gamepad.current;
      if (kb != null){ if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) m -= 1f; if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) m += 1f; }
      if (gp != null){ float x = gp.leftStick.x.ReadValue() + gp.dpad.x.ReadValue(); if (Mathf.Abs(x) > 0.2f) m += x; }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (bot) m += botMove;
#endif
      return Mathf.Clamp(m, -1f, 1f);
    }
    bool FireHeld(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      return (kb != null && kb.spaceKey.isPressed) || (gp != null && (gp.buttonSouth.isPressed || gp.rightTrigger.ReadValue() > 0.5f)) || touching
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        || bot
#endif
        ;
    }
    bool AnyFireDown(){
      var ts = Touchscreen.current;
      return FireHeld() || (ts != null && ts.primaryTouch.press.isPressed) || (Keyboard.current != null && Keyboard.current.enterKey.isPressed);
    }
    /// <summary>Top-right on-screen pause button, in GUI units (720 high).</summary>
    Rect PauseRect => new Rect(Screen.width / (Screen.height / 720f) - 92f, 14f, 76f, 76f);
    /// <summary>A touch / click that began this frame on the pause button (it is consumed: no shot, no turn).</summary>
    bool PauseButtonPressed(){
      Vector2 p; var ts = Touchscreen.current; var ms = Mouse.current;
      if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) p = ts.primaryTouch.position.ReadValue();
      else if (ms != null && ms.leftButton.wasPressedThisFrame) p = ms.position.ReadValue();
      else return false;
      float sc = Screen.height / 720f; var g = new Vector2(p.x / sc, (Screen.height - p.y) / sc);
      return PauseRect.Contains(g);
    }
    bool TouchBegan(){ var ts = Touchscreen.current; return ts != null && ts.primaryTouch.press.wasPressedThisFrame; }
    /// <summary>Enter Playing from a screen; the input that did it never fires or turns the ship.</summary>
    void GoPlay(string what){
      State = GameState.Playing; Time.timeScale = 1f; holdFire = true; if (TouchBegan()) touchConsumed = true;
      Debug.Log($"[NH] {what} shots={shots} ang={Ang:0}");
    }
    bool FirePressed(){
      var kb = Keyboard.current; var gp = Gamepad.current; var ts = Touchscreen.current; var ms = Mouse.current;
      return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
             (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) ||
             (ts != null && ts.primaryTouch.press.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame);
    }
    bool PausePressed(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      return (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
    }
    /// <summary>Touch: horizontal drag turns the ship (screen-width drag ≈ 300°); any touch held = auto-fire.</summary>
    float TouchTurn(){
      var ts = Touchscreen.current; bool now = ts != null && ts.primaryTouch.press.isPressed;
      if (!now) touchConsumed = false;
      if (touchConsumed){ touching = false; touchMoved = 0f; return 0f; }
      float d = 0f;
      if (now){ float dx = ts.primaryTouch.delta.ReadValue().x; d = dx / Mathf.Max(1, Screen.width) * 300f; touchMoved += d; }
      if (touching && !now){ if (Mathf.Abs(touchMoved) > 1f) Debug.Log($"[NH] touch drag turned {touchMoved:0} deg ang={Ang:0}"); touchMoved = 0f; }
      touching = now; return d;
    }

    // ---------------- loop ----------------
    void Update(){
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      DevKeys();
#endif
      switch (State){
        case GameState.Ready: if (FirePressed()){ StartRun(); GoPlay("start"); } break;
        case GameState.Paused: if (PausePressed() || PauseButtonPressed() || TouchBegan()) GoPlay("resumed"); break;
        case GameState.GameOver: case GameState.ThemeClear:
          TouchTurn();
          if (Time.unscaledTime - overAt > 0.8f && FirePressed()){ StartRun(); GoPlay("restart"); }
          break;
        case GameState.Playing:
          if (PausePressed() || PauseButtonPressed()){
            if (TouchBegan()) touchConsumed = true;
            State = GameState.Paused; Time.timeScale = 0f; Debug.Log($"[NH] paused shots={shots} ang={Ang:0}"); break;
          }
          Tick(Time.deltaTime); break;
      }
      Place(); Animate(Time.deltaTime);
    }

    // ---------------- run / levels / boss ----------------
    void StartRun(){
      ClearField(); Score = 0; Lives = startLives; nextExtra = extraLifeEvery; Power = PowerKind.None; invuln = invulnTime; newHigh = false;
      once.Clear(); BeginLevel(0);
    }
    void ClearField(){
      foreach (var x in bugs) Destroy(x.t.gameObject); foreach (var x in bolts) Destroy(x.t.gameObject); foreach (var x in ebullets) Destroy(x.t.gameObject);
      foreach (var x in pickups){ Destroy(x.t.gameObject); Destroy(x.glow.gameObject); } foreach (var x in fxs) Destroy(x.t.gameObject);
      bugs.Clear(); bolts.Clear(); ebullets.Clear(); pickups.Clear(); fxs.Clear(); pending.Clear();
      bossAlive = false; boss.gameObject.SetActive(false);
    }
    void BeginLevel(int i){
      level = i; phase = Phase.Intro; phaseT = 0f; levelTime = 0f; pending.Clear();
      var L = IonDrift.Levels[i]; Track.SetShape(L.curve, L.twist, S + 110f);
      foreach (var w in L.waves){
        float baseAng = Random.Range(-180f, 180f);
        for (int k = 0; k < w.count; k++) pending.Add((w.at + k * w.interval, w.bug, baseAng + Random.Range(-w.spread, w.spread)));
      }
      pending.Sort((a, b) => a.t.CompareTo(b.t));
      GameAudio.I?.Play("levelStart"); Debug.Log($"[NH] level {i + 1} start \"{L.name}\" curve={L.curve} twist={L.twist} bugs={pending.Count}");
    }
    void BeginBoss(){
      phase = Phase.BossIntro; phaseT = 0f; GameAudio.I?.Play("bossWarning"); Debug.Log("[NH] boss warning");
    }
    void SpawnBoss(){
      phase = Phase.Boss; bossAlive = true; bossHp = bossMaxHp; bossS = S + bossDist + 25f; bossAge = 0f; bossCd = 2.5f; bossPattern = 0; bossTime = 0f;
      boss.gameObject.SetActive(true); Debug.Log($"[NH] boss start hp={bossMaxHp}");
    }
    void Tick(float dt){
      if (dt <= 0f) return;   // frozen (dev showcase)
      S += speed * dt; phaseT += dt;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (bot) BotThink(dt);
#endif
      Ang = Mathf.Repeat(Ang + MoveAxis() * turnSpeed * dt + TouchTurn() + 180f, 360f) - 180f;
      if (holdFire && (!AnyFireDown() || BotOn)) holdFire = false;
      fireCd -= dt; if (!holdFire && FireHeld() && fireCd <= 0f) Fire();
      if (Power != PowerKind.None && (powerLeft -= dt) <= 0f){ Debug.Log($"[NH] powerup expired {Power}"); Power = PowerKind.None; }
      invuln -= dt;
      switch (phase){
        case Phase.Intro: if (phaseT > 2f){ phase = Phase.Waves; levelTime = 0f; } break;
        case Phase.Waves:
          levelTime += dt;
          while (pending.Count > 0 && pending[0].t <= levelTime){ SpawnBug(pending[0].kind, S + spawnAhead, pending[0].ang); pending.RemoveAt(0); }
          if (pending.Count == 0 && bugs.Count == 0){
            phase = Phase.Clear; phaseT = 0f; AddScore(500); GameAudio.I?.Play("levelClear");
            Debug.Log($"[NH] level {level + 1} clear score={Score} lives={Lives} time={levelTime:0}s");
          }
          break;
        case Phase.Clear: if (phaseT > 2.5f){ if (level < IonDrift.Levels.Length - 1) BeginLevel(level + 1); else BeginBoss(); } break;
        case Phase.BossIntro: if (phaseT > 3f) SpawnBoss(); break;
        case Phase.Boss: BossTick(dt); break;
        case Phase.BossDying:
          if (Random.value < dt * 10f) AddFx(explosion, 20f, bossS + Random.Range(-2f, 2f), Random.Range(-180f, 180f), Random.Range(0f, 3f), 3f);
          if (phaseT > 2.2f){
            boss.gameObject.SetActive(false); State = GameState.ThemeClear; overAt = Time.unscaledTime; SaveHigh();
            Debug.Log($"[NH] theme clear score={Score} high={High} new={newHigh} lives={Lives}");
          }
          break;
      }
      TickBolts(dt); TickBugs(dt); TickBullets(dt); TickPickups(dt);
    }

    void BossTick(float dt){
      bossAge += dt; bossTime += dt; bossFlash -= dt;
      bossS = Mathf.Lerp(bossS, S + bossDist + Mathf.Sin(bossAge * 0.7f) * 3f, 1f - Mathf.Exp(-dt * 1.5f));
      if ((bossCd -= dt) > 0f) return;
      bool enraged = bossHp < bossMaxHp * 0.5f;
      if (bossPattern % 3 < 2){   // bullet fan around the ship's angle
        int n = enraged ? 5 : 3; float spreadDeg = enraged ? 36f : 24f, off = Random.Range(-8f, 8f);
        for (int k = 0; k < n; k++) FireEnemyBullet(bossS - 1f, Ang + off + Mathf.Lerp(-spreadDeg, spreadDeg, k / (n - 1f)), 0f);
        GameAudio.I?.Play("enemyShot"); LogOnce("bossfan", $"[NH] boss pattern fan n={n}");
        bossCd = enraged ? 2.4f : 2.9f;
      } else {                    // spawn a ring of Mites
        int n = enraged ? 4 : 3; float a0 = Random.Range(-180f, 180f);
        for (int k = 0; k < n; k++) SpawnBug(BugKind.Mite, bossS - 2f, a0 + k * 360f / n);
        LogOnce("bossmites", $"[NH] boss pattern mites n={n}"); bossCd = enraged ? 2.6f : 3.2f;
      }
      bossPattern++;
    }
    void DamageBoss(float d){
      if (!bossAlive) return;
      int q0 = Mathf.CeilToInt(bossHp / bossMaxHp * 4f); bossHp -= d; bossFlash = 0.04f;
      if (bossHp > 0f && Mathf.CeilToInt(bossHp / bossMaxHp * 4f) < q0) Debug.Log($"[NH] boss hp={bossHp:0} t={bossTime:0.0}s"); GameAudio.I?.Play("bossHit"); LogOnce("bosshit", "[NH] boss hit");
      if (bossHp <= 0f){
        bossAlive = false; phase = Phase.BossDying; phaseT = 0f; GameAudio.I?.Play("bossExplode"); AddScore(5000);
        foreach (var x in ebullets) Destroy(x.t.gameObject); ebullets.Clear();
        for (int i = bugs.Count - 1; i >= 0; i--) Kill(bugs[i], false);
        Debug.Log($"[NH] boss defeated time={bossTime:0.0}s score={Score} lives={Lives}");
      }
    }

    // ---------------- bugs ----------------
    void SpawnBug(BugKind k, float s, float ang){
      Sprite sp = k == BugKind.Wasp ? waspSprite : k == BugKind.Spinner ? spinnerSprite : miteSprite;
      Color c = k == BugKind.Wasp ? waspTint : k == BugKind.Spinner ? spinnerTint : miteTint;
      var go = MakeSprite(k.ToString(), sp, c, k == BugKind.Spinner ? 2.0f : 1.7f, out var sr); if (miteMat) sr.sharedMaterial = miteMat;
      float spd = k == BugKind.Wasp ? 19f : k == BugKind.Spinner ? 9f : 13f;
      bugs.Add(new Bug{ t = go.transform, sr = sr, kind = k, s = s, ang = ang, speed = spd * (phase >= Phase.BossIntro ? 1.1f : IonDrift.Levels[level].bugSpeed) * Random.Range(0.85f, 1.15f), hp = k == BugKind.Spinner ? 4f : 1f,
                        phase = Random.Range(0f, 6.28f), fireCd = 0.6f });
      LogOnce("bug" + k, $"[NH] bug spawned {k}");
    }
    void TickBugs(float dt){
      for (int i = bugs.Count - 1; i >= 0; i--){
        var b = bugs[i]; b.age += dt;
        switch (b.kind){
          case BugKind.Mite: b.s -= b.speed * dt; b.ang = Mathf.MoveTowardsAngle(b.ang, Ang, 22f * dt); break;
          case BugKind.Wasp: b.s -= b.speed * dt; b.ang += Mathf.Cos(b.age * 2.6f + b.phase) * 95f * dt; b.ang = Mathf.MoveTowardsAngle(b.ang, Ang, 8f * dt); break;
          case BugKind.Spinner:
            if (!b.held && b.s - S > 30f) b.s -= b.speed * dt;
            else if (b.holdT < 5f){
              b.held = true; b.holdT += dt; b.s = S + 30f; b.ang = Mathf.MoveTowardsAngle(b.ang, Ang, 12f * dt);
              if ((b.fireCd -= dt) <= 0f){ b.fireCd = 1.3f; FireEnemyBullet(b.s, Ang, shipR); GameAudio.I?.Play("enemyShot"); LogOnce("spinfire", "[NH] spinner fired"); }
            } else b.s -= b.speed * dt;
            break;
        }
        if (invuln <= 0f && Mathf.Abs(b.s - S) < hitDs && Arc(b.ang, Ang) < hitArc){ bugs.RemoveAt(i); Destroy(b.t.gameObject); AddFx(explosion, 20f, b.s, b.ang, shipR, 2.2f); PlayerHit($"{b.kind}"); continue; }
        if (b.s < S - camBack){ Destroy(b.t.gameObject); bugs.RemoveAt(i); }
      }
    }
    void HitBug(Bug b, float dmg){
      b.hp -= dmg;
      if (b.hp > 0f){ AddFx(spark, 24f, b.s, b.ang, shipR, 0.9f); GameAudio.I?.Play("bugHit"); return; }
      Kill(b, true);
    }
    void Kill(Bug b, bool score){
      bugs.Remove(b); Destroy(b.t.gameObject);
      AddFx(spark, 24f, b.s, b.ang, shipR, 0.9f); AddFx(explosion, 20f, b.s, b.ang, shipR, 2.0f);
      GameAudio.I?.Play("bugHit"); GameAudio.I?.Play("bugExplode");
      if (!score) return;
      AddScore(b.kind == BugKind.Wasp ? 150 : b.kind == BugKind.Spinner ? 300 : 100);
      LogOnce("kill" + b.kind, $"[NH] {b.kind.ToString().ToLower()} killed score={Score}");
      float drop = b.kind == BugKind.Spinner ? 0.25f : b.kind == BugKind.Wasp ? 0.08f : 0.04f;
      if (pickups.Count == 0 && phase != Phase.Boss && Random.value < drop) SpawnPickup(b.s, b.ang);
    }

    // ---------------- player fire ----------------
    void Fire(){
      switch (Power){
        case PowerKind.Spread: for (int k = -1; k <= 1; k++) AddBolt(Ang + k * 11f, boltSpeed, 1f, false, false, boltTint, 1.7f); fireCd = 0.16f; GameAudio.I?.Play("spread"); break;
        case PowerKind.Laser: AddBolt(Ang, 100f, 2f, true, false, uiTint, 3.4f); fireCd = 0.18f; GameAudio.I?.Play("laser"); break;
        case PowerKind.Rapid: AddBolt(Ang + Random.Range(-2f, 2f), boltSpeed, 1f, false, false, boltTint, 1.6f); fireCd = 0.065f; GameAudio.I?.Play("plasma"); break;
        case PowerKind.Homing: AddBolt(Ang, 52f, 1f, false, true, waspTint, 1.7f); fireCd = 0.15f; GameAudio.I?.Play("homing"); break;
        default: AddBolt(Ang, boltSpeed, 1f, false, false, boltTint, 1.7f); fireCd = fireInterval; GameAudio.I?.Play("shot"); break;
      }
      shots++; if (shots == 1 || shots % 25 == 0) Debug.Log($"[NH] fired shots={shots}");
      if (Power != PowerKind.None) LogOnce("pfire" + Power, $"[NH] powerup fired {Power}");
    }
    void AddBolt(float ang, float spd, float dmg, bool pierce, bool homing, Color c, float size){
      var go = MakeSprite("Bolt", bulletSprite, c, size, out _);
      bolts.Add(new Bolt{ t = go.transform, s = S + 0.8f, ang = ang, speed = spd, dmg = dmg, pierce = pierce, homing = homing, hit = pierce ? new List<Bug>() : null });
    }
    void TickBolts(float dt){
      for (int i = bolts.Count - 1; i >= 0; i--){
        var b = bolts[i]; b.s += b.speed * dt; b.age += dt;
        if (b.homing){   // steer toward the nearest bug ahead
          Bug best = null; float bd = 40f; foreach (var g in bugs){ float d = g.s - b.s; if (d > 0f && d < bd){ bd = d; best = g; } }
          if (best != null) b.ang = Mathf.MoveTowardsAngle(b.ang, best.ang, 240f * dt);
        }
        bool gone = false;
        if (bossAlive && b.s >= bossS - 1f){ DamageBoss(b.dmg); gone = true; }
        else foreach (var g in bugs){
          if (Mathf.Abs(g.s - b.s) < hitDs * 1.4f && Arc(g.ang, b.ang) < hitArc && (b.hit == null || !b.hit.Contains(g))){
            if (b.pierce){ b.hit.Add(g); HitBug(g, b.dmg); break; } HitBug(g, b.dmg); gone = true; break;
          }
        }
        if (gone || b.age > boltLife){ Destroy(b.t.gameObject); bolts.RemoveAt(i); }
      }
    }

    // ---------------- enemy bullets ----------------
    void FireEnemyBullet(float s0, float ang, float r0){
      var go = MakeSprite("EBullet", enemyBulletSprite, enemyBulletTint, 0.9f, out _);
      float meet = (s0 - S) / (9f + speed);   // bullets drift back at 9 u/s while the ship closes at `speed`
      ebullets.Add(new EBullet{ t = go.transform, s = s0, ang = ang, r0 = r0, s0 = s0, sEnd = s0 - 9f * meet });
    }
    float BulletR(EBullet e) => Mathf.Lerp(e.r0, shipR, Mathf.Clamp01((e.s0 - e.s) / Mathf.Max(0.5f, e.s0 - e.sEnd)));
    void TickBullets(float dt){
      for (int i = ebullets.Count - 1; i >= 0; i--){
        var e = ebullets[i]; e.s -= 9f * dt;
        if (invuln <= 0f && Mathf.Abs(e.s - S) < 0.8f && Arc(e.ang, Ang) < 0.55f && BulletR(e) > shipR * 0.8f){ Destroy(e.t.gameObject); ebullets.RemoveAt(i); PlayerHit($"bullet da={Mathf.DeltaAngle(Ang, e.ang):0}"); continue; }
        if (e.s < S - 3f){ Destroy(e.t.gameObject); ebullets.RemoveAt(i); }
      }
    }

    // ---------------- power-ups ----------------
    Sprite PowerSprite(PowerKind k) => k == PowerKind.Spread ? spreadSprite : k == PowerKind.Laser ? laserSprite : k == PowerKind.Rapid ? rapidSprite : homingSprite;
    Color PowerTint(PowerKind k) => k == PowerKind.Spread ? miteTint : k == PowerKind.Laser ? uiTint : k == PowerKind.Rapid ? boltTint : waspTint;
    void SpawnPickup(float s, float ang, PowerKind k = PowerKind.None){
      if (k == PowerKind.None){ do k = (PowerKind)Random.Range(1, 5); while (k == Power); }
      var glow = MakeSprite("PickupGlow", glowSprite, PowerTint(k), 2.2f, out _).transform;
      var go = MakeSprite("Pickup" + k, PowerSprite(k), PowerTint(k), 1.5f, out _);
      pickups.Add(new Pickup{ t = go.transform, glow = glow, glowScale = glow.localScale, kind = k, s = s, ang = ang });
      Debug.Log($"[NH] powerup dropped {k}");
    }
    void TickPickups(float dt){
      for (int i = pickups.Count - 1; i >= 0; i--){
        var p = pickups[i]; p.age += dt;
        bool got = Mathf.Abs(p.s - S) < 1.3f && Arc(p.ang, Ang) < 1.4f;
        if (got) GrantPower(p.kind);
        if (got || p.s < S - 3f){ Destroy(p.t.gameObject); Destroy(p.glow.gameObject); pickups.RemoveAt(i); }
      }
    }
    void GrantPower(PowerKind k){ Power = k; powerLeft = powerTime; GameAudio.I?.Play("pickup"); Debug.Log($"[NH] powerup picked {k}"); }

    // ---------------- score / lives ----------------
    float Arc(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b)) * Mathf.Deg2Rad * shipR;
    void AddScore(int n){
      Score += n;
      while (Score >= nextExtra){ nextExtra += extraLifeEvery; Lives++; GameAudio.I?.Play("extraLife"); Debug.Log($"[NH] extra life lives={Lives} score={Score}"); }
    }
    void PlayerHit(string by){
      Lives--; invuln = invulnTime; GameAudio.I?.Play("playerHit"); GameAudio.I?.Play("lifeLost");
      if (Power != PowerKind.None){ Debug.Log($"[NH] powerup lost {Power}"); Power = PowerKind.None; }
      Debug.Log($"[NH] life lost lives={Lives} by={by} level={level + 1} phase={phase}");
      if (Lives <= 0){ State = GameState.GameOver; overAt = Time.unscaledTime; SaveHigh(); Debug.Log($"[NH] game over score={Score} high={High} new={newHigh} level={level + 1} phase={phase}"); }
    }
    void SaveHigh(){ if (Score > High){ High = Score; newHigh = true; PlayerPrefs.SetInt(HighKey, High); PlayerPrefs.Save(); } }
    void AddFx(Sprite[] frames, float fps, float s, float ang, float r, float size){
      if (frames == null || frames.Length == 0) return;
      var go = MakeSprite("Fx", frames[0], fxTint, size, out var sr);
      fxs.Add(new Fx{ t = go.transform, sr = sr, frames = frames, fps = fps, s = s, ang = ang, r = r });
    }

    // ---------------- presentation ----------------
    void Place(){
      float cs = S - camBack;
      cam.transform.SetPositionAndRotation(Track.P(cs), Quaternion.LookRotation(Track.P(S + lookAhead) - Track.P(cs), Track.Frame(cs) * Vector3.up));
      tunnel.Follow(cs);
      ship.SetPositionAndRotation(Track.At(S, Ang, shipR), Bill(Ang));
      shipSr.enabled = invuln <= 0f || State != GameState.Playing || Mathf.Repeat(Time.time * 10f, 1f) > 0.4f;
      foreach (var b in bugs) b.t.SetPositionAndRotation(Track.At(b.s, b.ang, shipR), Bill(b.ang) * (b.kind == BugKind.Spinner ? Quaternion.Euler(0, 0, b.age * 220f) : Quaternion.identity));
      foreach (var b in bolts) b.t.SetPositionAndRotation(Track.At(b.s, b.ang, shipR), Bill(b.ang));
      foreach (var e in ebullets) e.t.SetPositionAndRotation(Track.At(e.s, e.ang, BulletR(e)), Bill(e.ang));
      foreach (var p in pickups){
        var pos = Track.At(p.s, p.ang, shipR * 0.9f); p.t.SetPositionAndRotation(pos, cam.transform.rotation); p.glow.SetPositionAndRotation(pos, cam.transform.rotation);
        p.glow.localScale = p.glowScale * (1f + 0.15f * Mathf.Sin(p.age * 6f));
      }
      foreach (var f in fxs) f.t.SetPositionAndRotation(Track.At(f.s, f.ang, f.r), Bill(f.ang));
      if (boss.gameObject.activeSelf){
        boss.SetPositionAndRotation(Track.At(bossS, 0f, 0f), cam.transform.rotation * Quaternion.Euler(0, 0, Mathf.Sin(bossAge * 0.5f) * 12f));
        bossSr.color = bossFlash > 0f ? Color.Lerp(bossTint, Color.white, 0.45f) : bossTint;
      }
    }
    void Animate(float dt){
      for (int i = fxs.Count - 1; i >= 0; i--){
        var f = fxs[i]; f.age += dt; int k = (int)(f.age * f.fps);
        if (k >= f.frames.Length){ Destroy(f.t.gameObject); fxs.RemoveAt(i); continue; }
        f.sr.sprite = f.frames[k];
      }
    }

    // ---------------- HUD ----------------
    GUIStyle big, mid;
    void OnGUI(){
      float sc = Screen.height / 720f; GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f)); float W = Screen.width / sc, H = 720f;
      if (big == null){
        big = new GUIStyle(GUI.skin.label){ fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; big.normal.textColor = uiTint;
        mid = new GUIStyle(GUI.skin.label){ fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; mid.normal.textColor = Color.white;
      }
      var old = GUI.color; var c = new GUIStyle(mid){ alignment = TextAnchor.MiddleCenter };
      GUI.color = boltTint; for (int i = 0; i < Lives; i++) if (iconLife) GUI.DrawTexture(new Rect(24 + i * 52, 20, 44, 44), iconLife);
      GUI.color = uiTint; if (iconScore) GUI.DrawTexture(new Rect(W - 350, 20, 44, 44), iconScore);
      GUI.color = old; GUI.Label(new Rect(W - 298, 18, 190, 48), Score.ToString("000000"), mid);
      if (Power != PowerKind.None && State != GameState.Ready){   // active power-up: its badge + time left
        var sp = PowerSprite(Power); GUI.color = PowerTint(Power);
        if (sp) GUI.DrawTexture(new Rect(24, 74, 56, 56), sp.texture);
        GUI.DrawTexture(new Rect(86, 98, 120f * Mathf.Clamp01(powerLeft / powerTime), 8), Texture2D.whiteTexture); GUI.color = old;
      }
      if (State == GameState.Playing || State == GameState.Paused){
        var pr = PauseRect; GUI.color = new Color(uiTint.r, uiTint.g, uiTint.b, State == GameState.Paused ? 0.35f : 0.18f);
        GUI.DrawTexture(pr, Texture2D.whiteTexture); GUI.color = uiTint;
        if (iconPause) GUI.DrawTexture(new Rect(pr.x + 10, pr.y + 10, pr.width - 20, pr.height - 20), iconPause); GUI.color = old;
        var lv = new GUIStyle(c){ fontSize = 22 }; lv.normal.textColor = uiTint;
        GUI.Label(new Rect(0, 14, W, 30), phase >= Phase.BossIntro ? $"{IonDrift.Theme.ToUpper()}  -  PRISM MOTH" : $"{IonDrift.Theme.ToUpper()}  {level + 1}/4", lv);
        if (phase == Phase.Boss){   // boss health bar
          GUI.color = new Color(1, 1, 1, 0.25f); GUI.DrawTexture(new Rect(W * 0.5f - 300, 50, 600, 16), Texture2D.whiteTexture);
          GUI.color = bossTint; GUI.DrawTexture(new Rect(W * 0.5f - 300, 50, 600 * Mathf.Clamp01(bossHp / bossMaxHp), 16), Texture2D.whiteTexture); GUI.color = old;
        }
        if (State == GameState.Playing){
          if (phase == Phase.Intro) { GUI.Label(new Rect(0, H * 0.3f, W, 90), $"LEVEL {level + 1}", big); GUI.Label(new Rect(0, H * 0.3f + 90, W, 50), IonDrift.Levels[level].name.Substring(3), c); }
          else if (phase == Phase.Clear) GUI.Label(new Rect(0, H * 0.3f, W, 90), "LEVEL CLEAR", big);
          else if (phase == Phase.BossIntro && Mathf.Repeat(Time.time * 2.5f, 1f) > 0.3f){ var wst = new GUIStyle(big); wst.normal.textColor = spinnerTint; GUI.Label(new Rect(0, H * 0.3f, W, 90), "WARNING", wst); }
        }
      }
      if (State == GameState.Ready){
        if (logo) GUI.DrawTexture(new Rect(W * 0.5f - 420, H * 0.3f - 120, 840, 210), logo); else GUI.Label(new Rect(0, H * 0.3f, W, 90), "NEON HIVE", big); GUI.Label(new Rect(0, H * 0.3f + 100, W, 50), "Space / A / tap to start", c);
        if (High > 0) GUI.Label(new Rect(0, H * 0.3f + 150, W, 50), $"High score {High}", c);
      }
      else if (State == GameState.Paused){ GUI.Label(new Rect(0, H * 0.35f, W, 90), "PAUSED", big); GUI.Label(new Rect(0, H * 0.35f + 100, W, 50), "Tap or Esc / Start to resume", c); }
      else if (State == GameState.GameOver || State == GameState.ThemeClear){
        GUI.Label(new Rect(0, H * 0.28f, W, 90), State == GameState.GameOver ? "GAME OVER" : "THEME CLEAR", big);
        if (State == GameState.ThemeClear) GUI.Label(new Rect(0, H * 0.28f + 80, W, 44), $"{IonDrift.Theme} complete", c);
        GUI.Label(new Rect(0, H * 0.28f + 125, W, 44), $"Score {Score}", c);
        GUI.Label(new Rect(0, H * 0.28f + 170, W, 44), newHigh ? $"NEW HIGH SCORE {High}" : $"High score {High}", c);
        GUI.Label(new Rect(0, H * 0.28f + 225, W, 44), "Space / A / tap to play again", c);
      }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    // ---------------- dev only (compiled out of release) ----------------
    bool bot; float botMove, botThinkT, botAim, botDodgeT, botDodge;
    float Danger(float dir){
      float d = 0f;
      void Check(float ds, float closing, float ang){
        if (ds < -0.3f || ds > 18f) return; float t = Mathf.Max(0f, ds) / closing;
        if (t < 0.8f && Arc(ang, Ang + dir * turnSpeed * Mathf.Min(t, 0.5f)) < 1.2f) d += 1f / (t + 0.15f);
      }
      foreach (var b in bugs) Check(b.s - S, speed + b.speed, b.ang);
      foreach (var e in ebullets) Check(e.s - S, speed + 9f, e.ang);
      return d;
    }
    /// <summary>F1-F4: jump to level 1-4. F5: jump to the boss. F6: grant the next power-up. F8: freeze / unfreeze.
    /// F9: toggle the autopilot bot (an "active average player": always firing, dodges what's close, aims at the next bug
    /// with a reaction delay and aim noise).</summary>
    void DevKeys(){
      var kb = Keyboard.current; if (kb == null) return;
      if (kb.f9Key.wasPressedThisFrame){ bot = !bot; Debug.Log($"[NH] dev bot {(bot ? "on" : "off")}"); }
      if (kb.f8Key.wasPressedThisFrame && State == GameState.Playing){ Time.timeScale = Time.timeScale == 0f ? 1f : 0f; Debug.Log($"[NH] dev freeze {Time.timeScale == 0f}"); }
      if (State != GameState.Playing) return;
      for (int i = 0; i < 5; i++){
        var k = i == 0 ? kb.f1Key : i == 1 ? kb.f2Key : i == 2 ? kb.f3Key : i == 3 ? kb.f4Key : kb.f5Key;
        if (!k.wasPressedThisFrame) continue;
        ClearField(); Time.timeScale = 1f;
        if (i < 4) BeginLevel(i); else { level = 3; Track.SetShape(IonDrift.Levels[3].curve, IonDrift.Levels[3].twist, S + 110f); BeginBoss(); }
        Debug.Log($"[NH] dev jump {(i < 4 ? "level " + (i + 1) : "boss")}");
      }
      if (kb.f6Key.wasPressedThisFrame) GrantPower(Power == PowerKind.Homing ? PowerKind.Spread : (PowerKind)((int)Power + 1));
    }
    void BotThink(float dt){
      // dodge: every ~0.15 s, look at what reaches the ship in the next ~0.6 s and pick the move (left / stay / right)
      // whose predicted angle stays clear of it
      if ((botDodgeT -= dt) <= 0f){
        botDodgeT = 0.15f; float keep = Mathf.Abs(botDodge) > 0.5f ? botDodge : (Random.value < 0.5f ? -1f : 1f); botDodge = 0f; float best = Danger(0f);
        if (best > 0f){ foreach (float dir in new[]{ keep, -keep }){ float dg = Danger(dir); if (dg < best){ best = dg; botDodge = dir; } } if (botDodge == 0f) botDodge = 0.0001f; }
      }
      if (botDodge != 0f){ botMove = Mathf.Sign(botDodge) * (Mathf.Abs(botDodge) < 0.5f ? 0f : 1f); return; }
      if ((botThinkT -= dt) <= 0f){   // re-aim with a reaction delay and some noise
        botThinkT = 0.22f; float best = 999f; botAim = Ang;
        foreach (var p in pickups){ float ds = p.s - S; if (ds > 0f && ds < 30f){ best = -1f; botAim = p.ang; } }
        if (best > 0f) foreach (var b in bugs){ float ds = b.s - S; if (ds > 4f && ds < 50f && ds < best){ best = ds; botAim = b.ang; } }
        botAim += Random.Range(-7f, 7f);
      }
      float d = Mathf.DeltaAngle(Ang, botAim); botMove = Mathf.Abs(d) < 3f ? 0f : Mathf.Clamp(d / 25f, -1f, 1f);
    }
#endif
  }
}
