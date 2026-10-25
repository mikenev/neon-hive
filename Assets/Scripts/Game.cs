using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeonHive {
  public enum GameState { Ready, Playing, Paused, GameOver }

  /// <summary>Neon Hive M1 core loop. Everything lives in tunnel space: s = distance along the Track, ang = degrees around
  /// the axis (-90 = bottom). The ship (billboard) circles the inner wall at ShipR and fires bolts down the tunnel; Mites
  /// come the other way, drifting toward the ship's angle. A bolt kills a Mite (hit spark + explosion); touching one costs
  /// a life (short invulnerability after). IMGUI HUD: lives + score with the UI icons, ready / pause / game over screens.
  /// Input (new Input System, devices read directly): keys A/D or arrows, Space, Esc; gamepad stick / d-pad, A or RT, Start;
  /// touch: drag left/right to move, auto-fire while touching.</summary>
  public class Game : MonoBehaviour {
    public static Game I;
    [Header("Refs (set by BuildWeb.SetupScene)")]
    public Tunnel tunnel; public Camera cam; public Material spriteMat;
    public Sprite shipSprite, miteSprite, bulletSprite; public Sprite[] explosion, spark;
    public Texture2D iconLife, iconScore;
    [Header("Tuning")]
    public float speed = 14f, shipR = 4.1f, turnSpeed = 170f, camBack = 9f, lookAhead = 22f;
    public float fireInterval = 0.13f, boltSpeed = 62f, boltLife = 1.3f;
    public float miteSpeed = 13f, miteHoming = 22f, spawnAhead = 75f, spawnStart = 1.0f, spawnMin = 0.38f, spawnRamp = 0.012f;
    public int startLives = 3; public float invulnTime = 1.6f, hitDs = 1.1f, hitArc = 1.05f;
    public Color shipTint = Color.white, miteTint = new Color32(0x00, 0xF0, 0xFF, 255), boltTint = new Color32(0xFF, 0x2B, 0xD6, 255), fxTint = new Color32(0x3D, 0x7B, 0xFF, 255);

    public GameState State { get; private set; } = GameState.Ready;
    public int Score { get; private set; } public int Lives { get; private set; }
    public float S { get; private set; } public float Ang { get; private set; } = -90f;
    class Mite { public Transform t; public SpriteRenderer sr; public float s, ang, speed; }
    class Bolt { public Transform t; public float s, ang, age; }
    class Fx { public Transform t; public SpriteRenderer sr; public Sprite[] frames; public float age, fps, s, ang, r; }
    readonly List<Mite> mites = new(); readonly List<Bolt> bolts = new(); readonly List<Fx> fxs = new();
    Transform ship; SpriteRenderer shipSr;
    float fireCd, spawnCd, playTime, invuln, overAt, touchMoved; int shots; bool touching;

    void Awake(){ I = this; Lives = startLives; }
    void Start(){
      ship = MakeSprite("Ship", shipSprite, shipTint, 1.4f, out shipSr).transform;
      S = 0f; Place(); Debug.Log($"[NH] ready curve={tunnel.curveAmount} twist={tunnel.twist} rings={tunnel.ringCount}");
    }

    GameObject MakeSprite(string n, Sprite sp, Color c, float size, out SpriteRenderer sr){
      var go = new GameObject(n); sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sharedMaterial = spriteMat; sr.color = c;
      float w = sp ? Mathf.Max(sp.bounds.size.x, sp.bounds.size.y) : 1f; go.transform.localScale = Vector3.one * size / w; return go;
    }
    /// <summary>Billboard facing the camera, rolled so the sprite's +Y points at the tunnel centre.</summary>
    Quaternion Bill(float ang) => cam.transform.rotation * Quaternion.Euler(0, 0, ang + 90f);

    // ---------------- input ----------------
    float MoveAxis(){
      float m = 0f; var kb = Keyboard.current; var gp = Gamepad.current;
      if (kb != null){ if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) m -= 1f; if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) m += 1f; }
      if (gp != null){ float x = gp.leftStick.x.ReadValue() + gp.dpad.x.ReadValue(); if (Mathf.Abs(x) > 0.2f) m += x; }
      return Mathf.Clamp(m, -1f, 1f);
    }
    bool FireHeld(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      return (kb != null && kb.spaceKey.isPressed) || (gp != null && (gp.buttonSouth.isPressed || gp.rightTrigger.ReadValue() > 0.5f)) || touching;
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
        case GameState.Ready: if (FirePressed()){ State = GameState.Playing; Debug.Log("[NH] start"); } break;
        case GameState.Paused: if (PausePressed()){ State = GameState.Playing; Time.timeScale = 1f; Debug.Log("[NH] resumed"); } break;
        case GameState.GameOver:
          TouchTurn();
          if (Time.unscaledTime - overAt > 0.8f && FirePressed()) Restart();
          break;
        case GameState.Playing:
          if (PausePressed()){ State = GameState.Paused; Time.timeScale = 0f; Debug.Log("[NH] paused"); break; }
          Tick(Time.deltaTime); break;
      }
      Place(); Animate(Time.deltaTime);
    }

    void Tick(float dt){
      if (dt <= 0f) return;   // frozen (dev showcase)
      playTime += dt; S += speed * dt;
      Ang = Mathf.Repeat(Ang + MoveAxis() * turnSpeed * dt + TouchTurn() + 180f, 360f) - 180f;
      fireCd -= dt; if (FireHeld() && fireCd <= 0f){ fireCd = fireInterval; Fire(); }
      spawnCd -= dt;
      if (spawnCd <= 0f){ spawnCd = Mathf.Max(spawnMin, spawnStart - playTime * spawnRamp); SpawnMite(S + spawnAhead, Random.Range(-180f, 180f)); }
      invuln -= dt;
      for (int i = bolts.Count - 1; i >= 0; i--){
        var b = bolts[i]; b.s += boltSpeed * dt; b.age += dt;
        Mite hit = null; foreach (var m in mites) if (Mathf.Abs(m.s - b.s) < hitDs * 1.4f && Arc(m.ang, b.ang) < hitArc){ hit = m; break; }
        if (hit != null){ Kill(hit); Destroy(b.t.gameObject); bolts.RemoveAt(i); continue; }
        if (b.age > boltLife){ Destroy(b.t.gameObject); bolts.RemoveAt(i); }
      }
      for (int i = mites.Count - 1; i >= 0; i--){
        var m = mites[i]; m.s -= m.speed * dt; m.ang = Mathf.MoveTowardsAngle(m.ang, Ang, miteHoming * dt);
        if (invuln <= 0f && Mathf.Abs(m.s - S) < hitDs && Arc(m.ang, Ang) < hitArc){ PlayerHit(m); continue; }
        if (m.s < S - camBack){ Destroy(m.t.gameObject); mites.RemoveAt(i); }
      }
    }
    float Arc(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b)) * Mathf.Deg2Rad * shipR;

    void Fire(){
      var go = MakeSprite("Bolt", bulletSprite, boltTint, 1.7f, out _);
      bolts.Add(new Bolt{ t = go.transform, s = S + 0.8f, ang = Ang });
      shots++; GameAudio.I?.Play("shot");
      if (shots == 1 || shots % 25 == 0) Debug.Log($"[NH] fired shots={shots}");
    }
    void SpawnMite(float s, float ang){
      var go = MakeSprite("Mite", miteSprite, miteTint, 1.7f, out var sr);
      mites.Add(new Mite{ t = go.transform, sr = sr, s = s, ang = ang, speed = miteSpeed * Random.Range(0.85f, 1.2f) });
    }
    void Kill(Mite m){
      mites.Remove(m); Destroy(m.t.gameObject);
      AddFx(spark, 24f, m.s, m.ang, shipR, 0.9f); AddFx(explosion, 20f, m.s, m.ang, shipR, 2.0f);
      GameAudio.I?.Play("bugHit"); GameAudio.I?.Play("bugExplode");
      Score += 100; Debug.Log($"[NH] mite killed score={Score}");
    }
    void PlayerHit(Mite m){
      mites.Remove(m); Destroy(m.t.gameObject); AddFx(explosion, 20f, m.s, m.ang, shipR, 2.2f);
      Lives--; invuln = invulnTime; GameAudio.I?.Play("playerHit"); GameAudio.I?.Play("lifeLost");
      Debug.Log($"[NH] life lost lives={Lives}");
      if (Lives <= 0){ State = GameState.GameOver; overAt = Time.unscaledTime; Debug.Log($"[NH] game over score={Score}"); }
    }
    void AddFx(Sprite[] frames, float fps, float s, float ang, float r, float size){
      if (frames == null || frames.Length == 0) return;
      var go = MakeSprite("Fx", frames[0], fxTint, size, out var sr);
      fxs.Add(new Fx{ t = go.transform, sr = sr, frames = frames, fps = fps, s = s, ang = ang, r = r });
    }
    void Restart(){
      foreach (var m in mites) Destroy(m.t.gameObject); foreach (var b in bolts) Destroy(b.t.gameObject); foreach (var f in fxs) Destroy(f.t.gameObject);
      mites.Clear(); bolts.Clear(); fxs.Clear();
      Score = 0; Lives = startLives; playTime = 0f; spawnCd = 1f; invuln = invulnTime; Time.timeScale = 1f; State = GameState.Playing;
      Debug.Log("[NH] restart");
    }

    // ---------------- presentation ----------------
    void Place(){
      float cs = S - camBack;
      cam.transform.SetPositionAndRotation(Track.P(cs), Quaternion.LookRotation(Track.P(S + lookAhead) - Track.P(cs), Track.Frame(cs) * Vector3.up));
      tunnel.Follow(cs);
      ship.SetPositionAndRotation(Track.At(S, Ang, shipR), Bill(Ang));
      shipSr.enabled = invuln <= 0f || State != GameState.Playing || Mathf.Repeat(Time.time * 10f, 1f) > 0.4f;
      foreach (var m in mites) m.t.SetPositionAndRotation(Track.At(m.s, m.ang, shipR), Bill(m.ang));
      foreach (var b in bolts) b.t.SetPositionAndRotation(Track.At(b.s, b.ang, shipR), Bill(b.ang));
      foreach (var f in fxs) f.t.SetPositionAndRotation(Track.At(f.s, f.ang, f.r), Bill(f.ang));
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
        big = new GUIStyle(GUI.skin.label){ fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; big.normal.textColor = miteTint;
        mid = new GUIStyle(GUI.skin.label){ fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; mid.normal.textColor = Color.white;
      }
      var old = GUI.color;
      GUI.color = boltTint; for (int i = 0; i < Lives; i++) if (iconLife) GUI.DrawTexture(new Rect(24 + i * 52, 20, 44, 44), iconLife);
      GUI.color = miteTint; if (iconScore) GUI.DrawTexture(new Rect(W - 250, 20, 44, 44), iconScore);
      GUI.color = old; GUI.Label(new Rect(W - 198, 18, 190, 48), Score.ToString("000000"), mid);
      var c = new GUIStyle(mid){ alignment = TextAnchor.MiddleCenter };
      if (State == GameState.Ready){ GUI.Label(new Rect(0, H * 0.3f, W, 90), "NEON HIVE", big); GUI.Label(new Rect(0, H * 0.3f + 100, W, 50), "Space / A / tap to start", c); }
      else if (State == GameState.Paused){ GUI.Label(new Rect(0, H * 0.35f, W, 90), "PAUSED", big); GUI.Label(new Rect(0, H * 0.35f + 100, W, 50), "Esc / Start to resume", c); }
      else if (State == GameState.GameOver){
        GUI.Label(new Rect(0, H * 0.3f, W, 90), "GAME OVER", big); GUI.Label(new Rect(0, H * 0.3f + 100, W, 50), $"Score {Score}", c);
        GUI.Label(new Rect(0, H * 0.3f + 150, W, 50), "Space / A / tap to restart", c);
      }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /// <summary>Dev only. F8: stage and freeze a showcase frame (Mites ahead of the ship, bolts in flight, an explosion);
    /// F8 again resumes.</summary>
    void DevKeys(){
      var kb = Keyboard.current; if (kb == null || !kb.f8Key.wasPressedThisFrame) return;
      if (Time.timeScale == 0f && State == GameState.Playing){ Time.timeScale = 1f; Debug.Log("[NH] dev showcase resumed"); return; }
      if (State != GameState.Playing) return;
      for (int i = 0; i < 6; i++) SpawnMite(S + 7f + i * 4f, Ang + (i % 2 == 0 ? 1 : -1) * (14f + i * 9f));
      SpawnMite(S + 12f, Ang + 6f);
      for (int i = 0; i < 3; i++){ Fire(); bolts[bolts.Count - 1].s += 2.5f + i * 3f; }
      AddFx(explosion, 20f, S + 10f, Ang - 35f, shipR, 2f); fxs[fxs.Count - 1].age = 0.12f;
      invuln = 0f; Place(); Animate(0f); Time.timeScale = 0f; Debug.Log("[NH] dev showcase frozen");
    }
#endif
  }
}
