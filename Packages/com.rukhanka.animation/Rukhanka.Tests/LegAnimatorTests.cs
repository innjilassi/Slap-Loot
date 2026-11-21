using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class LegAnimatorTests
{
    World world;
    EntityManager em;
    RuntimeAnimationData runtimeData;
    SystemHandle system;
    Entity body;
    Entity[] targets;
    double elapsed;

/////////////////////////////////////////////////////////////////////////////////

    [SetUp]
    public void SetUp()
    {
        world = new World("Leg animator tests");
        em = world.EntityManager;
        runtimeData = RuntimeAnimationData.MakeDefault();
        em.CreateSingleton(runtimeData);
        system = world.GetOrCreateSystem<LegAnimatorSystem>();
        body = em.CreateEntity(typeof(LocalTransform), typeof(LegAnimatorComponent));
        em.SetComponentData(body, LocalTransform.Identity);
        em.SetComponentData(body, new LegAnimatorComponent { stepTime = 0.2f, stepHeight = 0.3f });
        em.AddBuffer<LegAnimatorLegInMotion>(body);
        em.AddBuffer<LegAnimatorLeg>(body);
        targets = new Entity[4];
        for (var i = 0; i < targets.Length; ++i)
        {
            var position = new float3((i & 1) == 0 ? -1 : 1, 0, i < 2 ? -1 : 1);
            targets[i] = em.CreateEntity(typeof(LocalTransform), typeof(Parent));
            em.SetComponentData(targets[i], LocalTransform.FromPosition(position));
            em.SetComponentData(targets[i], new Parent { Value = body });
            var ik = em.CreateEntity(typeof(TwoBoneIKComponent));
            em.SetComponentData(ik, new TwoBoneIKComponent { target = targets[i], weight = 1 });
            em.GetBuffer<LegAnimatorLeg>(body).Add(new LegAnimatorLeg
            {
                twoBoneIKEntity = ik, refPosition = position, stepThreshold = 0.4f, groupId = i & 1
            });
        }
        Update(0);
    }

/////////////////////////////////////////////////////////////////////////////////

    [TearDown]
    public void TearDown()
    {
        em.CompleteAllTrackedJobs();
        runtimeData.Dispose();
        world.Dispose();
    }

/////////////////////////////////////////////////////////////////////////////////

    void Update(float dt)
    {
        elapsed += dt;
        world.SetTime(new TimeData(elapsed, dt));
        system.Update(world.Unmanaged);
        em.CompleteAllTrackedJobs();
    }

/////////////////////////////////////////////////////////////////////////////////

    float3 WorldPosition(int index)
    {
        var parent = em.GetComponentData<LocalTransform>(body);
        return parent.TransformPoint(em.GetComponentData<LocalTransform>(targets[index]).Position);
    }

/////////////////////////////////////////////////////////////////////////////////

    static void AssertPosition(float3 expected, float3 actual) =>
        Assert.That(math.distance(expected, actual), Is.LessThan(0.0001f));

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void ParentedFeetStayPlantedBelowThreshold()
    {
        var before = WorldPosition(0);
        em.SetComponentData(body, LocalTransform.FromPositionRotationScale(new float3(0.1f, 0, 0), quaternion.RotateY(0.05f), 1.05f));
        Update(0.05f);
        AssertPosition(before, WorldPosition(0));
        Assert.That(em.GetBuffer<LegAnimatorLegInMotion>(body).Length, Is.Zero);
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void GroupsAlternateWithLiftAndLandExactly()
    {
        var stationary = WorldPosition(1);
        em.SetComponentData(body, LocalTransform.FromPosition(new float3(0.6f, 0, 0)));
        Update(0.1f);
        var moving = em.GetBuffer<LegAnimatorLegInMotion>(body);
        Assert.That(moving.Length, Is.EqualTo(2));
        foreach (var step in moving) Assert.That(step.legIndex & 1, Is.Zero);
        Assert.That(WorldPosition(0).y, Is.EqualTo(0.3f).Within(0.0001f));
        AssertPosition(stationary, WorldPosition(1));
        Update(0.1f);
        Assert.That(em.GetBuffer<LegAnimatorLegInMotion>(body).Length, Is.Zero);
        AssertPosition(new float3(-0.4f, 0, -1), WorldPosition(0));
        Update(0.1f);
        moving = em.GetBuffer<LegAnimatorLegInMotion>(body);
        Assert.That(moving.Length, Is.EqualTo(2));
        foreach (var step in moving) Assert.That(step.legIndex & 1, Is.EqualTo(1));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void RotationTriggersStepsAndLargeDeltaCompletesThem()
    {
        em.SetComponentData(body, LocalTransform.FromRotation(quaternion.RotateY(0.6f)));
        Update(1);
        Assert.That(em.GetBuffer<LegAnimatorLegInMotion>(body).Length, Is.Zero);
        var rest = em.GetBuffer<LegAnimatorLeg>(body)[0].refPosition;
        AssertPosition(math.rotate(quaternion.RotateY(0.6f), rest), WorldPosition(0));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void MissingTargetDoesNotPreventOtherLegsFromStepping()
    {
        em.DestroyEntity(targets[0]);
        em.SetComponentData(body, LocalTransform.FromPosition(new float3(1, 0, 0)));
        Update(0.1f);
        Assert.That(em.GetBuffer<LegAnimatorLegInMotion>(body).Length, Is.GreaterThan(0));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void DisabledAnimatorDoesNotMoveTargets()
    {
        var local = em.GetComponentData<LocalTransform>(targets[0]);
        em.SetComponentEnabled<LegAnimatorComponent>(body, false);
        em.SetComponentData(body, LocalTransform.FromPosition(new float3(1, 0, 0)));
        Update(0.1f);
        AssertPosition(local.Position, em.GetComponentData<LocalTransform>(targets[0]).Position);
    }
}
}
