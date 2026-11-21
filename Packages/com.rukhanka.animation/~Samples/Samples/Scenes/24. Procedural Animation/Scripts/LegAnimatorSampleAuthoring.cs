using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Samples
{
public struct LegAnimatorSample: IComponentData
{
    public float3 origin;
    public quaternion rotation;
    public float time;
}

//=====================================================================================//

public class LegAnimatorSampleAuthoring: MonoBehaviour
{
    class SampleBaker: Baker<LegAnimatorSampleAuthoring>
    {
        public override void Bake(LegAnimatorSampleAuthoring a)
        {
            var las = new LegAnimatorSample
            {
                origin = a.transform.position,
                rotation = a.transform.rotation
            };
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), las);
        }
    }
}

//=====================================================================================//

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
public partial class LegAnimatorSampleSystem: SystemBase
{
    protected override void OnUpdate()
    {
        var cfg = ProceduralAnimationsSampleConf.Instance;
        if (cfg == null || cfg.legBodySpeed == null)
            return;
        
        var dt = SystemAPI.Time.DeltaTime;
        
        foreach (var (sample, transform, animator, legs) in SystemAPI.Query<RefRW<LegAnimatorSample>, RefRW<LocalTransform>, RefRW<LegAnimatorComponent>, DynamicBuffer<LegAnimatorLeg>>())
        {
            sample.ValueRW.time += dt * cfg.legBodySpeed.value;
            var t = sample.ValueRO.time;
            
            transform.ValueRW.Position = sample.ValueRO.origin + new float3(math.sin(t) * 0.8f, 0, math.sin(t * 0.7f) * 0.45f);
            transform.ValueRW.Rotation = math.mul(sample.ValueRO.rotation, quaternion.RotateY(math.sin(t * 0.6f) * 0.45f));
            animator.ValueRW.stepHeight = cfg.legStepHeight.value;
            animator.ValueRW.stepTime = cfg.legStepTime.value;
            var legsBuffer = legs;
            
            for (var i = 0; i < legsBuffer.Length; ++i)
            {
                var leg = legsBuffer[i];
                leg.stepThreshold = cfg.legStepThreshold.value;
                legsBuffer[i] = leg;
            }
        }
    }
}
}
