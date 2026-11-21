using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class ControllerAnimationsCacheTests
{
    static BlobAssetReference<ControllerAnimationsBlob> MakeBlob(Hash128 first, Hash128 second)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<ControllerAnimationsBlob>();
        var animations = builder.Allocate(ref root.animations, 2);
        animations[0] = first;
        animations[1] = second;
        return builder.CreateBlobAssetReference<ControllerAnimationsBlob>(Allocator.Persistent);
    }

/////////////////////////////////////////////////////////////////////////////////

    [BurstCompile]
    struct ReadJob: IJob
    {
        public NativeParallelHashMap<ControllerAnimationsCacheKey, BlobAssetReference<ControllerAnimationsBlob>>.ReadOnly cache;
        public ControllerAnimationsCacheKey key;
        public NativeArray<Hash128> result;

        public void Execute()
        {
            var blob = cache[key];
            result[0] = blob.Value.animations[0];
            result[1] = blob.Value.animations[1];
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void PreparationSharesCacheAcrossSystemsAndSkipsDisabledOverridesAndLayers()
    {
        var hash = new Hash128(1, 0, 0, 0);
        var controller = MakeBlob(hash, hash);
        var overrides = MakeBlob(default, new Hash128(2, 0, 0, 0));
        var unusedOverrides = MakeBlob(hash, default);
        using var world = new World("Controller animations cache test");
        var manager = world.EntityManager;
        try
        {
            for (var i = 0; i < 5; ++i)
            {
                var entity = manager.CreateEntity();
                manager.AddBuffer<AnimatorControllerLayerComponent>(entity).Add(new AnimatorControllerLayerComponent { animations = controller });
                if (i != 4)
                    manager.AddComponentData(entity, new AnimatorOverrideAnimations { value = i < 2 ? overrides : unusedOverrides });
                if (i == 2)
                    manager.SetComponentEnabled<AnimatorOverrideAnimations>(entity, false);
                if (i == 3)
                    manager.SetComponentEnabled<AnimatorControllerLayerComponent>(entity, false);
            }
            var firstSystem = world.CreateSystemManaged<ControllerAnimationsCacheTestSystem>();
            var secondSystem = world.CreateSystemManaged<ControllerAnimationsCacheTestSystem>();
            firstSystem.Update();
            secondSystem.Update();
            using var query = manager.CreateEntityQuery(ComponentType.ReadWrite<InternalAnimatorDataSingleton>());
            var data = query.GetSingleton<InternalAnimatorDataSingleton>();
            data.cacheDependency.Complete();
            Assert.AreEqual(1, data.animatorOverrideAnimationsMap.Count());
            Assert.IsTrue(data.animatorOverrideAnimationsMap.ContainsKey(new ControllerAnimationsCacheKey { controller = controller, overrides = overrides }));
        }
        finally
        {
            using var query = manager.CreateEntityQuery(ComponentType.ReadWrite<InternalAnimatorDataSingleton>());
            if (!query.IsEmptyIgnoreFilter)
                query.GetSingleton<InternalAnimatorDataSingleton>().Dispose();
            unusedOverrides.Dispose();
            overrides.Dispose();
            controller.Dispose();
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void PreparationReusesEntriesGrowsMapAndPreservesReadOnlyView()
    {
        var original = new Hash128(1, 0, 0, 0);
        var replacement = new Hash128(2, 0, 0, 0);
        var controller = MakeBlob(original, original);
        var overrides = MakeBlob(default, replacement);
        var data = new InternalAnimatorDataSingleton
        {
            animatorOverrideAnimationsMap = new NativeParallelHashMap<ControllerAnimationsCacheKey, BlobAssetReference<ControllerAnimationsBlob>>(1, Allocator.Persistent)
        };
        var result = new NativeArray<Hash128>(2, Allocator.TempJob);
        using var world = new World("Controller animations cache growth test");
        var manager = world.EntityManager;
        manager.CreateSingleton(data);
        using var query = manager.CreateEntityQuery(ComponentType.ReadWrite<InternalAnimatorDataSingleton>());
        try
        {
            var first = new ControllerAnimationsCacheKey { controller = controller, overrides = overrides };
            // These two pairs collided with the old XOR-only key.
            var second = new ControllerAnimationsCacheKey { controller = overrides, overrides = controller };
            Assert.IsFalse(first.Equals(second));
            var readOnly = data.animatorOverrideAnimationsMap.AsReadOnly();
            // Exercise duplicate layers and reversed pairs through the actual preparation job.
            foreach (var key in new[] { first, second })
            {
                var entity = manager.CreateEntity();
                var layers = manager.AddBuffer<AnimatorControllerLayerComponent>(entity);
                layers.Add(new AnimatorControllerLayerComponent { animations = key.controller });
                layers.Add(new AnimatorControllerLayerComponent { animations = key.controller });
                manager.AddComponentData(entity, new AnimatorOverrideAnimations { value = key.overrides });
            }
            var system = world.CreateSystemManaged<ControllerAnimationsCacheTestSystem>();
            system.Update();
            var preparation = query.GetSingleton<InternalAnimatorDataSingleton>().cacheDependency;
            new ReadJob { cache = readOnly, key = first, result = result }.Schedule(preparation).Complete();

            Assert.AreEqual(2, data.animatorOverrideAnimationsMap.Count());
            Assert.AreEqual(original, result[0]);
            Assert.AreEqual(replacement, result[1]);
            Assert.AreEqual(original, readOnly[second].Value.animations[1]);
            var cached = readOnly[first];
            system.Update();
            query.GetSingleton<InternalAnimatorDataSingleton>().cacheDependency.Complete();
            Assert.AreEqual(cached, readOnly[first]);
        }
        finally
        {
            query.GetSingleton<InternalAnimatorDataSingleton>().cacheDependency.Complete();
            data.Dispose();
            result.Dispose();
            overrides.Dispose();
            controller.Dispose();
        }
    }
}

//---------------------------------------------------------------------------------------//

[DisableAutoCreation]
public partial class ControllerAnimationsCacheTestSystem: SystemBase
{
    EntityQuery controllers;
    EntityQuery cacheQuery;

/////////////////////////////////////////////////////////////////////////////////

    protected override void OnCreate()
    {
        controllers = GetEntityQuery(ComponentType.ReadOnly<AnimatorControllerLayerComponent>(), ComponentType.ReadOnly<AnimatorOverrideAnimations>());
        cacheQuery = GetEntityQuery(ComponentType.ReadWrite<InternalAnimatorDataSingleton>());
    }

/////////////////////////////////////////////////////////////////////////////////

    protected override void OnUpdate()
    {
        var data = FillAnimationsFromControllerSystem.GetInternalDataSingleton(cacheQuery, ref CheckedStateRef);
        Dependency = JobHandle.CombineDependencies(Dependency, data.cacheDependency);
        var job = new AnimatorControllerSystemJobs.PrepareControllerAnimationsJob
        {
            controllerLayersBufferHandle = GetBufferTypeHandle<AnimatorControllerLayerComponent>(true),
            animatorOverrideAnimationHandle = GetComponentTypeHandle<AnimatorOverrideAnimations>(true),
            cache = data.animatorOverrideAnimationsMap
        };
        Dependency = job.Schedule(controllers, Dependency);
        cacheQuery.GetSingletonRW<InternalAnimatorDataSingleton>().ValueRW.cacheDependency = Dependency;
    }
}
}
