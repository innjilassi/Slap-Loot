using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
public class LegAnimatorBaker: Baker<LegAnimatorAuthoring>
{
    public override void Bake(LegAnimatorAuthoring a)
    {
        if (a.legs == null || a.legs.Length == 0)
            return;

        // Validate before adding components so incomplete setups do not bake a partial gait.
        for (var i = 0; i < a.legs.Length; ++i)
        {
            var leg = a.legs[i];
            if (leg != null)
                DependsOn(leg);
            
            if (leg == null || leg.target == null || leg.mid == null || leg.tip == null)
            {
                Debug.LogError($"LegAnimator '{a.name}' leg {i} requires a two bone IK chain and target.", a);
                return;
            }
            
            for (var j = 0; j < i; ++j)
            {
                if (a.legs[j] == leg || a.legs[j].target == leg.target)
                {
                    Debug.LogError($"LegAnimator '{a.name}' legs must have distinct IK chains and targets.", a);
                    return;
                }
            }
        }

        var e = GetEntity(TransformUsageFlags.Dynamic);
        var legs = AddBuffer<LegAnimatorLeg>(e);
        AddBuffer<LegAnimatorLegInMotion>(e);
        
        var rng = Unity.Mathematics.Random.CreateFromIndex(0);
        for (var i = 0; i < a.legs.Length; ++i)
        {
            var leg = a.legs[i];
            
            var target = GetComponent<Transform>(leg.target);
            var lal = new LegAnimatorLeg
            {
                twoBoneIKEntity = GetEntity(leg, TransformUsageFlags.Dynamic),
                refPosition = a.transform.InverseTransformPoint(target.position),
                stepThreshold = math.max(0.001f, a.stepThreshold + rng.NextFloat(-1, 1) * math.max(0, a.stepVariation)),
                groupId = i & 1
            };
            legs.Add(lal);
        }
        
        var lac = new LegAnimatorComponent
        {
            stepTime = math.max(0.001f, a.stepTime),
            stepHeight = math.max(0, a.stepHeight)
        };
        AddComponent(e, lac);
    }
}
}
