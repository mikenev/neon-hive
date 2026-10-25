using UnityEngine;
namespace NeonHive {
  /// <summary>The tunnel centre line as a smooth endless spline-like curve, parametrised by s (≈ distance along +Z).
  /// Curve scales the sideways/vertical wander (0 = straight); Twist rolls the frame by Twist degrees per unit of s.
  /// Per-level changes (SetShape) take effect from a distance `at` (chosen beyond the rings already laid) and the curve
  /// amount blends in over BlendLen, so the path never jumps. Everything in the game lives in tunnel space (s, angle,
  /// radius) and is mapped to the world through here.</summary>
  public static class Track {
    const float BlendLen = 80f;
    static float prevCurve = 1f, curve = 1f, prevTwist = 0f, twist = 0f, at = float.NegativeInfinity, rollAt = 0f;
    public static float Curve => curve; public static float Twist => twist;
    /// <summary>Reset to a shape for the whole track (start of a run).</summary>
    public static void Init(float c, float t){ prevCurve = curve = c; prevTwist = twist = t; at = 0f; rollAt = 0f; }
    /// <summary>Switch to a new curve / twist from distance `fromS` on (blended).</summary>
    public static void SetShape(float c, float t, float fromS){
      float r = Roll(fromS); prevCurve = CurveAt(fromS); prevTwist = twist; curve = c; twist = t; at = fromS; rollAt = r;
    }
    static float CurveAt(float s){
      if (s <= at) return prevCurve;
      float k = Mathf.Clamp01((s - at) / BlendLen); return Mathf.Lerp(prevCurve, curve, k * k * (3f - 2f * k));
    }
    static float Roll(float s) => s <= at ? rollAt + prevTwist * (s - at) : rollAt + twist * (s - at);
    public static Vector3 P(float s){
      float c = CurveAt(s);
      return new Vector3(c * (16f * Mathf.Sin(s * 0.021f) + 6f * Mathf.Sin(s * 0.047f + 1.3f)),
                         c * (10f * Mathf.Sin(s * 0.017f + 0.7f) + 4f * Mathf.Sin(s * 0.039f + 2.1f)), s);
    }
    public static Vector3 T(float s) => (P(s + 0.5f) - P(s - 0.5f)).normalized;
    /// <summary>World length of one unit of s (used to stretch ring segments so they meet on curves).</summary>
    public static float Stretch(float s) => (P(s + 0.5f) - P(s - 0.5f)).magnitude;
    public static Quaternion Frame(float s) => Quaternion.LookRotation(T(s), Vector3.up) * Quaternion.Euler(0, 0, Roll(s));
    /// <summary>World point at distance s, angle (degrees, 0 = right, -90 = bottom) and radius from the centre line.</summary>
    public static Vector3 At(float s, float angDeg, float r){
      float a = angDeg * Mathf.Deg2Rad;
      return P(s) + Frame(s) * new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
    }
  }
}
