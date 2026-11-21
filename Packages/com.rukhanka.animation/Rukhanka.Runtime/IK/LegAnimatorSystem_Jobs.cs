using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public partial struct LegAnimatorSystem
{
    
[BurstCompile]
partial struct LegAnimatorJob: IJobEntity
{
    public ComponentLookup<LocalTransform> localTransformLookup;
    [ReadOnly] public ComponentLookup<Parent> parentLookup;
    [ReadOnly] public ComponentLookup<AnimatorEntityRefComponent> boneEntityRefLookup;
    [ReadOnly] public ComponentLookup<RigDefinitionComponent> rigDefLookup;
    [ReadOnly] public ComponentLookup<TwoBoneIKComponent> twoBoneIKLookup;
    [NativeDisableContainerSafetyRestriction] public RuntimeAnimationData runtimeData;
    
    public float dt;

/////////////////////////////////////////////////////////////////////////////////

    BoneTransform WorldPose(Entity e)
    {
        var pose = BoneTransform.Identity();
        IKCommon.GetEntityWorldTransform(e, ref pose, runtimeData, localTransformLookup, parentLookup, boneEntityRefLookup, rigDefLookup);
        return pose;
    }

/////////////////////////////////////////////////////////////////////////////////

    bool TryGetTarget(in LegAnimatorLeg leg, out Entity target)
    {
        target = Entity.Null;
        if (!twoBoneIKLookup.TryGetComponent(leg.twoBoneIKEntity, out var ik) || ik.weight <= 0)
            return false;
        target = ik.target;
        // Animated targets would be read from the animation stream by TwoBoneIK instead.
        return localTransformLookup.HasComponent(target) && !boneEntityRefLookup.HasComponent(target);
    }

/////////////////////////////////////////////////////////////////////////////////

    void SetTargetPosition(Entity target, float3 worldPosition)
    {
        var local = localTransformLookup[target];
        if (parentLookup.TryGetComponent(target, out var parent))
        {
            var pose = WorldPose(parent.Value);
            local.Position = math.rotate(math.inverse(pose.rot), worldPosition - pose.pos) / pose.scale;
        }
        else local.Position = worldPosition;
        localTransformLookup[target] = local;
    }

/////////////////////////////////////////////////////////////////////////////////

    void Execute(Entity e, ref LegAnimatorComponent animator, DynamicBuffer<LegAnimatorLeg> legs, DynamicBuffer<LegAnimatorLegInMotion> moving)
    {
        if (boneEntityRefLookup.TryGetComponent(e, out var rigRef)
            && (!rigDefLookup.TryGetComponent(rigRef.animatorEntity, out var rig) || !rig.dynamicFrameData.IsValid))
            return;
        var body = WorldPose(e);
        for (var i = 0; i < legs.Length; ++i)
        {
            var leg = legs[i];
            if (!TryGetTarget(leg, out var target)) continue;
            if (!leg.initialized)
            {
                leg.plantedPosition = WorldPose(target).pos;
                leg.initialized = true;
                legs[i] = leg;
            }
        }

        // Select an entire group before advancing any feet, so opposite groups never overlap.
        if (moving.IsEmpty && legs.Length > 0)
        {
            var group = -1;
            for (var k = 0; k < legs.Length; ++k)
            {
                var i = (animator.legStartIndex + k) % legs.Length;
                var leg = legs[i];
                if (!leg.initialized || !TryGetTarget(leg, out _)) continue;
                var rest = BoneTransform.TransformPoint(body, leg.refPosition);
                if (math.distance(rest, leg.plantedPosition) <= leg.stepThreshold) continue;
                group = leg.groupId;
                animator.legStartIndex = (i + 1) % legs.Length;
                break;
            }
            if (group >= 0)
            {
                for (var i = 0; i < legs.Length; ++i)
                {
                    var leg = legs[i];
                    if (leg.groupId != group || !leg.initialized || !TryGetTarget(leg, out _)) continue;
                    var rest = BoneTransform.TransformPoint(body, leg.refPosition);
                    if (math.distance(rest, leg.plantedPosition) <= leg.stepThreshold) continue;
                    moving.Add(new LegAnimatorLegInMotion
                    {
                        legIndex = i,
                        startAnimationPoint = leg.plantedPosition,
                        endAnimationPoint = rest
                    });
                }
            }
        }

        for (var i = 0; i < moving.Length;)
        {
            var step = moving[i];
            var leg = legs[step.legIndex];
            if (!TryGetTarget(leg, out _))
            {
                moving.RemoveAtSwapBack(i);
                continue;
            }
            step.moveFactor = math.saturate(step.moveFactor + math.max(0, dt) / math.max(0.001f, animator.stepTime));
            step.endAnimationPoint = BoneTransform.TransformPoint(body, leg.refPosition);
            var t = step.moveFactor;
            var smoothT = t * t * (3 - 2 * t);
            leg.plantedPosition = math.lerp(step.startAnimationPoint, step.endAnimationPoint, smoothT)
                + math.up() * (4 * t * (1 - t) * math.max(0, animator.stepHeight));
            legs[step.legIndex] = leg;
            if (t >= 1) moving.RemoveAtSwapBack(i);
            else moving[i++] = step;
        }

        // Rewrite planted targets every frame to compensate for their parents' movement.
        for (var i = 0; i < legs.Length; ++i)
        {
            var leg = legs[i];
            if (leg.initialized && TryGetTarget(leg, out var target))
                SetTargetPosition(target, leg.plantedPosition);
        }
    }
}
}
}
