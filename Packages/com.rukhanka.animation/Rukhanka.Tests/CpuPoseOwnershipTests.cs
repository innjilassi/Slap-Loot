using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class CpuPoseOwnershipTests
{
    struct TestRig
    {
        public Entity rig, bone, mesh;
        public float position;
    }

/////////////////////////////////////////////////////////////////////////////////

    static BlobAssetReference<RigDefinitionBlob> MakeRigBlob()
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<RigDefinitionBlob>();
        root.hash = new Hash128(1, 0, 0, 0);
        var bones = builder.Allocate(ref root.bones, 1);
        bones[0] = new RigBoneInfo { hash = 1, parentBoneIndex = -1, refPose = BoneTransform.Identity() };
        return builder.CreateBlobAssetReference<RigDefinitionBlob>(Allocator.Persistent);
    }

/////////////////////////////////////////////////////////////////////////////////

    static BlobAssetReference<SkinnedMeshInfoBlob> MakeMeshBlob()
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<SkinnedMeshInfoBlob>();
        root.hash = new Hash128(2, 0, 0, 0);
        var bones = builder.Allocate(ref root.bones, 1);
        bones[0] = new SkinnedMeshBoneInfo { hash = 1, bindPose = float4x4.identity };
        return builder.CreateBlobAssetReference<SkinnedMeshInfoBlob>(Allocator.Persistent);
    }

/////////////////////////////////////////////////////////////////////////////////

    static TestRig CreateRig(EntityManager manager, BlobAssetReference<RigDefinitionBlob> rigBlob,
        BlobAssetReference<SkinnedMeshInfoBlob> meshBlob, float position)
    {
        var result = new TestRig { rig = manager.CreateEntity(), bone = manager.CreateEntity(), mesh = manager.CreateEntity(), position = position };
        manager.AddComponentData(result.rig, new RigDefinitionComponent { rigBlob = rigBlob });
        manager.AddComponent<GPUAnimationEngineTag>(result.rig);
        manager.SetComponentEnabled<GPUAnimationEngineTag>(result.rig, false);
        manager.AddBuffer<AnimationToProcessComponent>(result.rig);
        manager.AddComponentData(result.bone, new AnimatorEntityRefComponent { animatorEntity = result.rig, boneIndexInAnimationRig = 0 });
        manager.AddComponentData(result.bone, LocalTransform.FromPosition(new float3(position, 0, 0)));
        manager.AddComponentData(result.mesh, new SkinnedMeshRendererComponent { animatedRigEntity = result.rig, smrInfoBlob = meshBlob });
        manager.AddComponentData(result.mesh, LocalTransform.Identity);
        manager.AddBuffer<SkinMatrix>(result.mesh).Add(default);
        return result;
    }

/////////////////////////////////////////////////////////////////////////////////

    static void AssertOwnPose(EntityManager manager, TestRig rig)
    {
        Assert.That(manager.GetBuffer<SkinMatrix>(rig.mesh)[0].Value.c3.x, Is.EqualTo(rig.position).Within(0.0001f));
        Assert.That(manager.GetComponentData<LocalTransform>(rig.bone).Position.x, Is.EqualTo(rig.position).Within(0.0001f));
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(true, 0)]
    [TestCase(false, 0)]
    [TestCase(true, 1)]
    [TestCase(false, 1)]
    [TestCase(true, 2)]
    [TestCase(false, 2)]
    public void RigExcludedDuringPreparationCannotReadRepackedPose(bool anotherCpuRig, int exclusion)
    {
        using var rigBlob = MakeRigBlob();
        using var meshBlob = MakeMeshBlob();
        using var world = new World("CPU pose ownership test");
        var manager = world.EntityManager;
        var process = world.CreateSystem<AnimationProcessSystem>();
        var application = world.CreateSystem<AnimationApplicationSystem>();
        var first = CreateRig(manager, rigBlob, meshBlob, 10);
        var second = anotherCpuRig ? CreateRig(manager, rigBlob, meshBlob, 20) : default;

        process.Update(world.Unmanaged);
        application.Update(world.Unmanaged);
        manager.CompleteAllTrackedJobs();
        AssertOwnPose(manager, first);
        if (anotherCpuRig)
        {
            AssertOwnPose(manager, second);
            if (manager.GetComponentData<RigDefinitionComponent>(first.rig).dynamicFrameData.bonePoseOffset != 0)
                (first, second) = (second, first);
        }
        Assert.AreEqual(0, manager.GetComponentData<RigDefinitionComponent>(first.rig).dynamicFrameData.bonePoseOffset);

        // Remove the first rig from CPU preparation, then repack another rig into its old range.
        if (exclusion == 0)
            manager.SetComponentEnabled<GPUAnimationEngineTag>(first.rig, true);
        else if (exclusion == 1)
            manager.SetComponentEnabled<RigDefinitionComponent>(first.rig, false);
        else
            manager.RemoveComponent<AnimationToProcessComponent>(first.rig);
        process.Update(world.Unmanaged);
        manager.CompleteAllTrackedJobs();
        Assert.IsFalse(manager.GetComponentData<RigDefinitionComponent>(first.rig).dynamicFrameData.IsValid);
        if (anotherCpuRig)
            Assert.AreEqual(0, manager.GetComponentData<RigDefinitionComponent>(second.rig).dynamicFrameData.bonePoseOffset);

        // Switch back AFTER preparation. Skinning and bone propagation must hold this rig's own pose.
        if (exclusion == 0)
            manager.SetComponentEnabled<GPUAnimationEngineTag>(first.rig, false);
        else if (exclusion == 1)
            manager.SetComponentEnabled<RigDefinitionComponent>(first.rig, true);
        else
            manager.AddBuffer<AnimationToProcessComponent>(first.rig);
        application.Update(world.Unmanaged);
        manager.CompleteAllTrackedJobs();
        AssertOwnPose(manager, first);
        if (anotherCpuRig)
            AssertOwnPose(manager, second);

        // The following CPU update prepares a fresh, usable range and resumes normal animation.
        manager.SetComponentData(first.bone, LocalTransform.FromPosition(new float3(first.position + 5, 0, 0)));
        first.position += 5;
        process.Update(world.Unmanaged);
        application.Update(world.Unmanaged);
        manager.CompleteAllTrackedJobs();
        Assert.IsTrue(manager.GetComponentData<RigDefinitionComponent>(first.rig).dynamicFrameData.IsValid);
        AssertOwnPose(manager, first);
        if (anotherCpuRig)
            AssertOwnPose(manager, second);
    }
}
}
