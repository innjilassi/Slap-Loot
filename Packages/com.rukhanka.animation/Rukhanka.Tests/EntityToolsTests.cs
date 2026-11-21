using NUnit.Framework;
using Rukhanka.Toolbox;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Rukhanka.Tests
{
public class EntityToolsTests
{
    struct TestData : IComponentData
    {
        public int value;
    }

    [DisableAutoCreation]
    partial class LookupSystem : SystemBase
    {
        protected override void OnUpdate() { }
    }

    [BurstCompile]
    struct ReadChunkDataJob : IJob
    {
        [ReadOnly]
        public EntityStorageInfoLookup storageInfoLookup;
        [ReadOnly]
        public ComponentTypeHandle<TestData> typeHandle;
        public Entity entity;
        public NativeArray<int> result;

        public void Execute()
        {
            result[0] = EntityTools.TryGetChunkComponentData(storageInfoLookup, entity, ref typeHandle, out var data) ? 1 : 0;
            result[1] = data.value;
        }
    }

    [TestCase("chunk", 1, 42)]
    [TestCase("ordinary", 0, 0)]
    [TestCase("missing", 0, 0)]
    [TestCase("null", 0, 0)]
    [TestCase("destroyed", 0, 0)]
    public void ReadChunkComponentInBurstJob(string kind, int expectedFound, int expectedValue)
    {
        using var world = new World("EntityToolsTests");
        var manager = world.EntityManager;
        var entity = manager.CreateEntity();
        if (kind == "chunk") {
            manager.AddComponentData(entity, new TestData { value = 7 });
            manager.AddChunkComponentData<TestData>(entity);
            manager.SetChunkComponentData(manager.GetChunk(entity), new TestData { value = 42 });
        } else if (kind == "ordinary") {
            manager.AddComponentData(entity, new TestData { value = 7 });
        } else if (kind == "null") {
            entity = Entity.Null;
        } else if (kind == "destroyed") {
            manager.DestroyEntity(entity);
        }

        var system = world.GetOrCreateSystemManaged<LookupSystem>();
        using var result = new NativeArray<int>(2, Allocator.TempJob);
        new ReadChunkDataJob {
            storageInfoLookup = system.GetEntityStorageInfoLookup(),
            typeHandle = system.GetComponentTypeHandle<TestData>(true),
            entity = entity,
            result = result
        }.Schedule().Complete();

        Assert.That(result[0], Is.EqualTo(expectedFound));
        Assert.That(result[1], Is.EqualTo(expectedValue));
    }
}
}
