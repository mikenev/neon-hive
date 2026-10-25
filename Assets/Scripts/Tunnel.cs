using System.Collections.Generic;
using UnityEngine;
namespace NeonHive {
  /// <summary>The tunnel: a ring mesh laid along Track, recycled ahead of the camera so it never ends. One ring mesh +
  /// wall texture per theme (round Ion Drift, hex Acid Hive, square Redline Core); SetTheme swaps them and re-lays every
  /// ring. ringScale widens the hex / square rings so their flat faces stay outside the ship's radius. curveAmount /
  /// twist are the start defaults (levels override them through Track.SetShape). Rings use the theme's primary colour,
  /// with secondary and accent rings, at HDR intensity for Bloom.</summary>
  public class Tunnel : MonoBehaviour {
    public Mesh[] ringMeshes; public Texture2D[] wallTextures; public float[] ringScale = { 1f, 1.08f, 1.3f }, themeIntensity = { 1.5f, 0.75f, 1.5f };   // the honeycomb wall is dense: dimmer so bugs read
    public Material ringMat;
    [Range(0f, 3f)] public float curveAmount = 0.8f;
    [Tooltip("Frame roll in degrees per unit of distance")] public float twist = 0f;
    public int ringCount = 26; public float segLen = 4f, intensity = 1.5f;
    public Color primary = new Color32(0x00, 0xF0, 0xFF, 255), secondary = new Color32(0xFF, 0x2B, 0xD6, 255), accent = new Color32(0x3D, 0x7B, 0xFF, 255);
    readonly List<(Transform t, MeshRenderer r, MeshFilter f, int idx)> rings = new();
    MaterialPropertyBlock mpb; int theme;
    static readonly int ColorId = Shader.PropertyToID("_Color"), TexId = Shader.PropertyToID("_MainTex");

    void Awake(){
      Track.Init(curveAmount, twist); mpb = new MaterialPropertyBlock();
      for (int i = 0; i < ringCount; i++){
        var go = new GameObject("Ring"); go.transform.SetParent(transform, false);
        var f = go.AddComponent<MeshFilter>(); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ringMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        rings.Add((go.transform, r, f, int.MinValue));
      }
      SetTheme(0, primary, secondary, accent);
    }
    /// <summary>Switch ring shape / wall / colours to theme `t` (call while a screen is up; all rings are re-laid).</summary>
    public void SetTheme(int t, Color p, Color s, Color a){
      theme = Mathf.Clamp(t, 0, ringMeshes.Length - 1); primary = p; secondary = s; accent = a;
      for (int i = 0; i < rings.Count; i++){ var x = rings[i]; x.f.sharedMesh = ringMeshes[theme]; rings[i] = (x.t, x.r, x.f, int.MinValue); }
    }
    Color TintFor(int idx) => idx % 6 == 5 ? secondary : idx % 3 == 1 ? accent : primary;
    /// <summary>Place rings from just behind `fromS` onward; a ring is only re-laid when its slot changes.</summary>
    public void Follow(float fromS){
      int first = Mathf.FloorToInt(fromS / segLen) - 2; float k = theme < ringScale.Length ? ringScale[theme] : 1f;
      for (int i = 0; i < rings.Count; i++){
        int idx = first + i, slot = ((idx % rings.Count) + rings.Count) % rings.Count;
        var ring = rings[slot]; if (ring.idx == idx) continue;
        float s = idx * segLen;
        ring.t.SetPositionAndRotation(Track.P(s), Track.Frame(s + segLen * 0.5f));
        ring.t.localScale = new Vector3(k, k, Track.Stretch(s + segLen * 0.5f));
        mpb.SetColor(ColorId, TintFor(idx) * (theme < themeIntensity.Length ? themeIntensity[theme] : intensity));
        var tex = theme < wallTextures.Length ? wallTextures[theme] : null; if (tex) mpb.SetTexture(TexId, tex);
        ring.r.SetPropertyBlock(mpb);
        rings[slot] = (ring.t, ring.r, ring.f, idx);
      }
    }
  }
}
