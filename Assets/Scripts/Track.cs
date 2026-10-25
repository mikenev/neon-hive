using UnityEngine;
namespace NeonHive {
  /// <summary>The tunnel centre line as a smooth endless spline-like curve, parametrised by s (≈ distance along +Z).
  /// Curve scales the sideways/vertical wander (0 = straight); Twist rolls the frame by Twist degrees per unit of s.
  /// Everything in the game lives in tunnel space (s, angle, radius) and is mapped to the world through here.</summary>
  public static class Track {
    public static float Curve = 1f, Twist = 0f;
    public static Vector3 P(float s) => new Vector3(
      Curve * (16f * Mathf.Sin(s * 0.021f) + 6f * Mathf.Sin(s * 0.047f + 1.3f)),
      Curve * (10f * Mathf.Sin(s * 0.017f + 0.7f) + 4f * Mathf.Sin(s * 0.039f + 2.1f)), s);
    public static Vector3 T(float s) => (P(s + 0.5f) - P(s - 0.5f)).normalized;
    /// <summary>World length of one unit of s (used to stretch ring segments so they meet on curves).</summary>
    public static float Stretch(float s) => (P(s + 0.5f) - P(s - 0.5f)).magnitude;
    public static Quaternion Frame(float s) => Quaternion.LookRotation(T(s), Vector3.up) * Quaternion.Euler(0, 0, Twist * s);
    /// <summary>World point at distance s, angle (degrees, 0 = right, -90 = bottom) and radius from the centre line.</summary>
    public static Vector3 At(float s, float angDeg, float r){
      float a = angDeg * Mathf.Deg2Rad;
      return P(s) + Frame(s) * new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
    }
  }
}
