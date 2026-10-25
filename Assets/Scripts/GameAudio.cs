using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeonHive {
  /// <summary>Music + one-shot SFX (GameAudio pattern): sfx source volume stays at 1 (one-shots scale by PlayOneShot volume),
  /// nothing plays until the first key / click / touch / gamepad button (WebGL autoplay; the unlock persists across restarts),
  /// per-id minimum gap, and a voice cap (MaxSame of one clip, MaxTotal at once) so busy fights don't clip.</summary>
  public class GameAudio : MonoBehaviour {
    public static GameAudio I;
    public AudioClip music, shot, bugHit, bugExplode, playerHit, lifeLost;
    public AudioClip spread, laser, plasma, homing, pickup, extraLife, levelStart, levelClear, bossWarning, bossHit, bossExplode, enemyShot, menuMove, menuSelect;
    [Range(0, 1)] public float musicVolume = 0.4f, sfxVolume = 0.8f;
    const int MaxSame = 3, MaxTotal = 8;
    static bool unlocked;
    AudioSource mus, sfx, shotSrc;   // shots get their own source so a per-shot random pitch does not bend other SFX
    struct Rule { public AudioClip clip; public float vol, gap; }
    readonly Dictionary<string, Rule> rules = new(); readonly Dictionary<string, float> last = new();
    readonly List<(float end, AudioClip clip)> voices = new();

    void Awake(){
      I = this;
      mus = gameObject.AddComponent<AudioSource>(); mus.loop = true; mus.playOnAwake = false; mus.clip = music; mus.volume = musicVolume;
      sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.volume = 1f;
      shotSrc = gameObject.AddComponent<AudioSource>(); shotSrc.playOnAwake = false; shotSrc.volume = 1f;
      void R(string id, AudioClip c, float v, float g){ rules[id] = new Rule{ clip = c, vol = v, gap = g }; }
      R("shot", shot, 0.315f, 0.06f);   // -30% vs M1, random pitch 0.95-1.05
      R("bugHit", bugHit, 0.6f, 0.04f); R("bugExplode", bugExplode, 0.7f, 0.05f);   // (M1 had these two swallowed by the comment above)
      R("spread", spread, 0.3f, 0.12f); R("laser", laser, 0.3f, 0.14f); R("plasma", plasma, 0.28f, 0.1f); R("homing", homing, 0.3f, 0.14f);
      R("pickup", pickup, 0.8f, 0.2f); R("extraLife", extraLife, 0.85f, 0.5f); R("levelStart", levelStart, 0.7f, 1f); R("levelClear", levelClear, 0.75f, 1f);
      R("bossWarning", bossWarning, 0.85f, 1f); R("bossHit", bossHit, 0.45f, 0.09f); R("bossExplode", bossExplode, 0.9f, 1f); R("enemyShot", enemyShot, 0.4f, 0.1f);
      R("menuMove", menuMove, 0.5f, 0.05f); R("menuSelect", menuSelect, 0.6f, 0.1f);
      R("playerHit", playerHit, 0.9f, 0.3f); R("lifeLost", lifeLost, 0.8f, 0.5f);
    }
    void Start(){ if (unlocked && mus.clip) mus.Play(); }
    /// <summary>Switch the music loop (menu / theme); no-op if it is already playing.</summary>
    public void SetMusic(AudioClip c){
      if (!c || mus.clip == c) return;
      mus.clip = c; if (unlocked) mus.Play();
    }
    static bool AnyInput(){
      var kb = Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
      var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) return true;
      var t = Touchscreen.current; if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
      var g = Gamepad.current; return g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame);
    }
    void Update(){
      if (unlocked || !AnyInput()) return;
      unlocked = true; if (mus.clip) mus.Play();
      Debug.Log("[Audio] unlocked on first input");
    }
    public void Play(string id){
      if (!unlocked || !rules.TryGetValue(id, out var r) || !r.clip) return;
      float now = Time.unscaledTime;
      if (last.TryGetValue(id, out var t) && now - t < r.gap) return;
      voices.RemoveAll(v => v.end <= now);
      int same = 0; foreach (var v in voices) if (v.clip == r.clip) same++;
      if (same >= MaxSame || voices.Count >= MaxTotal) return;
      voices.Add((now + r.clip.length, r.clip)); last[id] = now;
      if (id == "shot" || id == "spread" || id == "laser" || id == "plasma" || id == "homing"){ shotSrc.pitch = Random.Range(0.95f, 1.05f); shotSrc.PlayOneShot(r.clip, r.vol * sfxVolume); }
      else sfx.PlayOneShot(r.clip, r.vol * sfxVolume);
    }
  }
}
