using System.Collections.Generic;
using UnityEngine;
namespace NeonHive {
  /// <summary>Ion Drift tunnel: the 4-unit ring mesh laid along Track, recycled ahead of the camera so it never ends.
  /// curveAmount / twist feed Track (future themes: wilder curves and twist). Rings are tinted cyan, with electric blue and
  /// magenta accents, at HDR intensity for Bloom.</summary>
  public class Tunnel : MonoBehaviour {
    public Mesh ringMesh; public Material ringMat;
    [Range(0f, 3f)] public float curveAmount = 1.5f;
    [Tooltip("Frame roll in degrees per unit of distance")] public float twist = 0f;
    public int ringCount = 26; public float segLen = 4f, intensity = 1.5f;
    public Color primary = new Color32(0x00, 0xF0, 0xFF, 255), secondary = new Color32(0xFF, 0x2B, 0xD6, 255), accent = new Color32(0x3D, 0x7B, 0xFF, 255);
    readonly List<(Transform t, MeshRenderer r, int idx)> rings = new();
    MaterialPropertyBlock mpb;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake(){
      Track.Curve = curveAmount; Track.Twist = twist; mpb = new MaterialPropertyBlock();
      for (int i = 0; i < ringCount; i++){
        var go = new GameObject("Ring"); go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = ringMesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ringMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        rings.Add((go.transform, r, int.MinValue));
      }
    }
    Color TintFor(int idx) => idx % 6 == 5 ? secondary : idx % 3 == 1 ? accent : primary;
    /// <summary>Place rings from just behind `fromS` onward; a ring is only re-laid when its slot changes.</summary>
    public void Follow(float fromS){
      Track.Curve = curveAmount; Track.Twist = twist;
      int first = Mathf.FloorToInt(fromS / segLen) - 2;
      for (int i = 0; i < rings.Count; i++){
        int idx = first + i, slot = ((idx % rings.Count) + rings.Count) % rings.Count;
        var ring = rings[slot]; if (ring.idx == idx) continue;
        float s = idx * segLen;
        ring.t.SetPositionAndRotation(Track.P(s), Track.Frame(s + segLen * 0.5f));
        ring.t.localScale = new Vector3(1f, 1f, Track.Stretch(s + segLen * 0.5f));
        mpb.SetColor(ColorId, TintFor(idx) * intensity); ring.r.SetPropertyBlock(mpb);
        rings[slot] = (ring.t, ring.r, idx);
      }
    }
  }
}
