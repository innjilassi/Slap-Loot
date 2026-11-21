using System;
using Rukhanka.Toolbox;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
    
[UpdateInGroup(typeof(RukhankaAnimationInjectionSystemGroup), OrderFirst = true)]
public partial struct DynamicBoneChainSystem: ISystem
{
[BurstCompile]
partial struct DynamicBoneChainVerletSolverJob : IJobEntity
{
    [ReadOnly]
    public ComponentLookup<RigDefinitionComponent> rigDefLookup;
    [ReadOnly]
    public ComponentLookup<AnimatorEntityRefComponent> boneEntityRefLookup;
    [ReadOnly]
    public ComponentLookup<LocalTransform> localTransformLookup;
    [ReadOnly]
    public ComponentLookup<Parent> parentLookup;
    
    [NativeDisableContainerSafetyRestriction]
    public RuntimeAnimationData runtimeData;
    
    public float deltaTime;
    static readonly float fixedUpdateRate = 1 / 60.0f;

/////////////////////////////////////////////////////////////////////////////////

    void Execute(Entity e, ref DynamicBoneChainComponent dbcc, in AnimatorEntityRefComponent aer, ref DynamicBuffer<DynamicBoneChainNode> dynamicChain)
    {
        var rigDef = rigDefLookup[aer.animatorEntity];
        if (!rigDef.dynamicFrameData.IsValid)
            return;
        using var animStream = AnimationStream.Create(runtimeData, rigDef);
        
        Span<int> boneIndicesInRig = stackalloc int[dynamicChain.Length];
        var dca = dynamicChain.AsNativeArray();
        
        // AnimationStream "world" poses are rig/animator-local, NOT true world.
        // Host inertia and unit motion live in true world — simulate particles there, convert on write.
        var animatorWorld = BoneTransform.Identity();
        IKCommon.GetEntityWorldTransform
        (
            aer.animatorEntity,
            ref animatorWorld,
            runtimeData,
            localTransformLookup,
            parentLookup,
            boneEntityRefLookup,
            rigDefLookup
        );
        var worldToAnimator = BoneTransform.Inverse(animatorWorld);
        
        InitBoneFrameData(dca, boneIndicesInRig, animStream, animatorWorld);
        var worldInertia = ComputeEntityInertia(e, ref dbcc, runtimeData);
        
        // Spawn / teleport / first frame after space fix: reseed particles to rest true-world poses
        // so we never integrate a multi-meter jump as lag velocity.
        const float teleportDistSq = 16f; // 4m
        if (math.lengthsq(worldInertia) > teleportDistSq)
        {
            ReseedParticlesToRestWorld(dca, boneIndicesInRig, animStream, animatorWorld);
            worldInertia = float3.zero;
        }

        dbcc.timeAccumulator += deltaTime;
        var simulationCount = 0;
        while (dbcc.timeAccumulator >= fixedUpdateRate)
        {
            var modInertia = math.select(0, worldInertia * dbcc.inertia, simulationCount == 0);
            Integrate(dbcc, dca, modInertia, boneIndicesInRig, animStream, animatorWorld);
            Elasticity(dbcc, dca, boneIndicesInRig, animStream, animatorWorld);
            ConstrainDistance(dca, boneIndicesInRig, animStream, animatorWorld);
            dbcc.timeAccumulator -= fixedUpdateRate;
            simulationCount++;
        }
        
        if (simulationCount == 0)
        {
            ApplyMovementOffset(worldInertia * dbcc.inertia, dca);
            Elasticity(dbcc, dca, boneIndicesInRig, animStream, animatorWorld);
            ConstrainDistance(dca, boneIndicesInRig, animStream, animatorWorld);
        }
        
        MakeChainRotations(dca, boneIndicesInRig, animStream, animatorWorld, worldToAnimator);
        
        for (var i = 0; i < dynamicChain.Length; ++i)
        {
            var streamPos = BoneTransform.TransformPoint(worldToAnimator, dynamicChain[i].position);
            animStream.SetWorldPosition(boneIndicesInRig[i], streamPos);
        }
    }
    
/////////////////////////////////////////////////////////////////////////////////

    void ApplyMovementOffset(float3 inertia, NativeArray<DynamicBoneChainNode> dynamicChain)
    {
        for (var i = 1; i < dynamicChain.Length; ++i)
        {
            var dc = dynamicChain[i];
            dc.position += inertia;
            dc.prevPosition += inertia;
            dynamicChain[i] = dc;
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    void ReseedParticlesToRestWorld(NativeArray<DynamicBoneChainNode> dynamicChain, Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld)
    {
        for (var i = 0; i < dynamicChain.Length; ++i)
        {
            var streamPos = animStream.GetWorldPosition(boneIndicesInRig[i]);
            var worldPos = BoneTransform.TransformPoint(animatorWorld, streamPos);
            var dc = dynamicChain[i];
            dc.position = worldPos;
            dc.prevPosition = worldPos;
            dynamicChain[i] = dc;
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    void InitBoneFrameData(NativeArray<DynamicBoneChainNode> dynamicChain, Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld)
    {
        for (var i = 0; i < dynamicChain.Length; ++i)
        {
            var dbe = dynamicChain[i];
            if (!boneEntityRefLookup.TryGetComponent(dbe.boneEntity, out var boneEntityRef))
                return;
            
            // Drive chain from rest local pose; secondary motion replaces clip on these bones
            animStream.SetLocalPose(boneEntityRef.boneIndexInAnimationRig, dbe.referenceLocalPose);
            boneIndicesInRig[i] = boneEntityRef.boneIndexInAnimationRig;
        }
        
        // Root is kinematic: snap to true-world rest pose under current animator transform
        {
            var streamRootPos = animStream.GetWorldPosition(boneIndicesInRig[0]);
            var worldRootPos = BoneTransform.TransformPoint(animatorWorld, streamRootPos);
            var dbe = dynamicChain[0];
            dbe.position = worldRootPos;
            dbe.prevPosition = worldRootPos;
            dynamicChain[0] = dbe;
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    float3 ComputeEntityInertia(Entity e, ref DynamicBoneChainComponent dbcc, RuntimeAnimationData runtimeAnimationData)
    {
        BoneTransform bt = BoneTransform.Identity();
        IKCommon.GetEntityWorldTransform
        (
            e,
            ref bt,
            runtimeAnimationData,
            localTransformLookup,
            parentLookup,
            boneEntityRefLookup,
            rigDefLookup
        );
        var rv = bt.pos - dbcc.prevPosition;
        dbcc.prevPosition = bt.pos;
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////

    void Integrate(DynamicBoneChainComponent dbcc, NativeArray<DynamicBoneChainNode> dynamicChain, float3 inertia,Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld)
    {
        // Root bone is kinematic — follows animated true-world position
        var dc0 = dynamicChain[0];
        dc0.prevPosition = dc0.position;
        var streamRootPos = animStream.GetWorldPosition(boneIndicesInRig[0]);
        dc0.position = BoneTransform.TransformPoint(animatorWorld, streamRootPos);
        dynamicChain[0] = dc0;
        
        for (var i = 1; i < dynamicChain.Length; ++i)
        {
            var dc = dynamicChain[i];
            var dv = dc.position - dc.prevPosition;
            dc.prevPosition = dc.position + inertia;
            dc.position += inertia + dv * (1 - dbcc.damping);
            dynamicChain[i] = dc;
        }
    }

/////////////////////////////////////////////////////////////////////////////////
    
    void ConstrainDistance(NativeArray<DynamicBoneChainNode> dynamicChain, Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld)
    {
        for (var i = 1; i < dynamicChain.Length; ++i)
        {
            var dc = dynamicChain[i];
            // Rest bone length from stream rest pose, expressed in true world (uniform scale)
            var streamBonePos = animStream.GetWorldPosition(boneIndicesInRig[i]);
            var streamParentPos = animStream.GetWorldPosition(boneIndicesInRig[dc.parentIndex]);
            var refDeltaPos = BoneTransform.TransformPoint(animatorWorld, streamParentPos)
                - BoneTransform.TransformPoint(animatorWorld, streamBonePos);
            var refDeltaPosLen = math.length(refDeltaPos);
            
            var curDeltaPos = dynamicChain[dc.parentIndex].position - dc.position;
            var curDeltaPosLenSq = math.lengthsq(curDeltaPos);
            
            if (curDeltaPosLenSq > 0)
            {
                var curDeltaPosLen = math.sqrt(curDeltaPosLenSq);
                var slm = math.rcp(curDeltaPosLen);
                dc.position += curDeltaPos * slm * (curDeltaPosLen - refDeltaPosLen);
                dynamicChain[i] = dc;
            }
        }
    }
    
/////////////////////////////////////////////////////////////////////////////////

    void Elasticity(DynamicBoneChainComponent dbcc, NativeArray<DynamicBoneChainNode> dynamicChain, Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld)
    {
        for (var i = 1; i < dynamicChain.Length; ++i)
        {
            var dc = dynamicChain[i];
            var targetPos = ComputeRigidPosWorld(i, dynamicChain, boneIndicesInRig, animStream, animatorWorld);
            var delta = targetPos - dc.position;
            dc.position += delta * dbcc.elasticity;
            dc.position += Stiffness(targetPos, dc.position, dbcc.stiffness);
            dynamicChain[i] = dc;
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    float3 Stiffness(float3 rigidPos, float3 simPos, float stiffnessFactor)
    {
        if (stiffnessFactor <= 0)
            return float3.zero;
        
        var delta = rigidPos - simPos;
        var deltaLengthSq = math.lengthsq(delta);
        var deltaLength = math.sqrt(deltaLengthSq);
        
        if (deltaLengthSq < math.EPSILON)
            return float3.zero;
        
        var rcpDeltaLength = math.rcp(deltaLength);
        var stiffness = (2 - 2 * stiffnessFactor) * deltaLength;
        var dl = math.max(deltaLength - stiffness, 0);
        var rv = dl * delta * rcpDeltaLength;
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////

    float3 ComputeRigidPosWorld(int boneIndex, NativeArray<DynamicBoneChainNode> dynamicChain, Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld)
    {
        var dc1 = dynamicChain[boneIndex];
        var dc0 = dynamicChain[dc1.parentIndex];
        
        // Parent orientation from stream rest/anim, lifted to true world; parent position from sim
        var parentStreamPose = animStream.GetWorldPose(boneIndicesInRig[dc1.parentIndex]);
        var parentWorld = BoneTransform.Multiply(animatorWorld, parentStreamPose);
        parentWorld.pos = dc0.position;
        
        var childLocalPos = animStream.GetLocalPosition(boneIndicesInRig[boneIndex]);
        return BoneTransform.TransformPoint(parentWorld, childLocalPos);
    }

/////////////////////////////////////////////////////////////////////////////////
    
    void MakeChainRotations(NativeArray<DynamicBoneChainNode> dynamicChain, Span<int> boneIndicesInRig, AnimationStream animStream, BoneTransform animatorWorld, BoneTransform worldToAnimator)
    {
        // Snapshot rest world rotations (stream is still at rest locals for chain bones)
        Span<quaternion> restWorldRots = stackalloc quaternion[dynamicChain.Length];
        for (var i = 0; i < dynamicChain.Length; ++i)
        {
            var streamPose = animStream.GetWorldPose(boneIndicesInRig[i]);
            var worldPose = BoneTransform.Multiply(animatorWorld, streamPose);
            restWorldRots[i] = worldPose.rot;
        }
        
        // Aim every parent at its simulated child (includes chain root)
        for (var i = 1; i < dynamicChain.Length; ++i)
        {
            var parentIndex = dynamicChain[i].parentIndex;
            if (parentIndex < 0)
                continue;
            
            var parentWorldPos = dynamicChain[parentIndex].position;
            var childWorldPos = dynamicChain[i].position;
            var toChild = childWorldPos - parentWorldPos;
            var toChildLenSq = math.lengthsq(toChild);
            if (toChildLenSq < 1e-12f)
                continue;
            var ndv = toChild * math.rsqrt(toChildLenSq);
            
            // Rest bone axis: child local offset rotated by REST parent world rotation
            var childLocalPos = animStream.GetLocalPosition(boneIndicesInRig[i]);
            var restDir = math.rotate(restWorldRots[parentIndex], childLocalPos);
            var restLenSq = math.lengthsq(restDir);
            if (restLenSq < 1e-12f)
                continue;
            var nRest = restDir * math.rsqrt(restLenSq);
            
            // Safe FromTo (handles near-opposite vectors); applied to rest orientation
            var q = MathUtils.FromToRotation(nRest, ndv);
            var newWorldRot = math.mul(q, restWorldRots[parentIndex]);
            
            // Write stream-space world rotation
            var streamRot = math.mul(worldToAnimator.rot, newWorldRot);
            animStream.SetWorldRotation(boneIndicesInRig[parentIndex], streamRot);
        }
    }
}

/////////////////////////////////////////////////////////////////////////////////

[BurstCompile]
public void OnCreate(ref SystemState ss)
{
    var q = SystemAPI.QueryBuilder()
        .WithAll<DynamicBoneChainComponent, AnimatorEntityRefComponent, DynamicBoneChainNode>()
        .Build();
    
    ss.RequireForUpdate(q);
}

/////////////////////////////////////////////////////////////////////////////////

[BurstCompile]
public void OnUpdate(ref SystemState ss)
{
    var rigDefLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);
    var aerLookup = SystemAPI.GetComponentLookup<AnimatorEntityRefComponent>(true);
    var ltLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
    var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
    ref var runtimeData = ref SystemAPI.GetSingletonRW<RuntimeAnimationData>().ValueRW;
    
    var ikJob = new DynamicBoneChainVerletSolverJob()
    {
        runtimeData = runtimeData,
        rigDefLookup = rigDefLookup,
        boneEntityRefLookup = aerLookup,
        parentLookup = parentLookup,
        localTransformLookup = ltLookup,
        deltaTime = SystemAPI.Time.DeltaTime
    };

    ikJob.ScheduleParallel();
}
}
}
