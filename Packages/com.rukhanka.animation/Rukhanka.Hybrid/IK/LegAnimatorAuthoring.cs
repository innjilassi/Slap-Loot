using UnityEngine;

////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
public class LegAnimatorAuthoring: MonoBehaviour
{
    [Min(0.001f)]
    public float stepThreshold = 0.3f;
    [Min(0)]
    public float stepVariation = 0.05f;
    [Min(0.001f)]
    public float stepTime = 0.2f;
    [Min(0)]
    public float stepHeight = 0.15f;

////////////////////////////////////////////////////////////////////////////////////////

    [Tooltip("Legs alternate between two groups by array index. Each leg needs its own non-animated IK target.")]
    public TwoBoneIKAuthoring[] legs;

////////////////////////////////////////////////////////////////////////////////////////

    void OnEnable() { }
}
}
