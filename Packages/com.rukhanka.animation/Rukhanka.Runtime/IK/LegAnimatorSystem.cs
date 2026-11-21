using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
[UpdateBefore(typeof(TwoBoneIKSystem))]
[UpdateInGroup(typeof(RukhankaAnimationInjectionSystemGroup))]
public partial struct LegAnimatorSystem: ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState ss)
    {
        ss.RequireForUpdate<LegAnimatorComponent>();
        ss.RequireForUpdate<RuntimeAnimationData>();
    }

/////////////////////////////////////////////////////////////////////////////////

    [BurstCompile]
    public void OnUpdate(ref SystemState ss)
    {
        var job = new LegAnimatorJob
        {
            localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
            parentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            boneEntityRefLookup = SystemAPI.GetComponentLookup<AnimatorEntityRefComponent>(true),
            rigDefLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true),
            twoBoneIKLookup = SystemAPI.GetComponentLookup<TwoBoneIKComponent>(true),
            runtimeData = SystemAPI.GetSingleton<RuntimeAnimationData>(),
            dt = SystemAPI.Time.DeltaTime
        };
        // Targets can share parents. Serial scheduling avoids concurrent lookup reads/writes.
        ss.Dependency = job.Schedule(ss.Dependency);
    }
}
}
