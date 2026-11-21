using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
#if RUKHANKA_WITH_NETCODE
using Unity.NetCode;
#endif
using UnityEngine;
using static Rukhanka.AnimatorControllerSystemJobs;

/////////////////////////////////////////////////////////////////////////////////////////////////////

[assembly: RegisterGenericSystemType(typeof(AnimatorControllerSystem<PredictedAnimatorControllerQuery>))]
[assembly: RegisterGenericSystemType(typeof(AnimatorControllerSystem<AnimatorControllerQuery>))]

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[DisableAutoCreation]
public partial struct AnimatorControllerSystem<T>: ISystem where T: AnimatorControllerQueryCreator, new()
{
	EntityQuery animatorControllerQuery;
	EntityQuery prepareControllerAnimationsQuery;

/////////////////////////////////////////////////////////////////////////////////////////////////////

	public void OnCreate(ref SystemState ss)
	{
		var queryCreator = new T();
		animatorControllerQuery = queryCreator.CreateQuery(ref ss);
		prepareControllerAnimationsQuery = SystemAPI.QueryBuilder()
			.WithAll<AnimatorControllerLayerComponent, AnimatorOverrideAnimations>()
			.Build();
		
		// The regular controller system also prepares overrides for fill-only ghosts.
		if (typeof(T) == typeof(AnimatorControllerQuery))
			ss.RequireAnyForUpdate(animatorControllerQuery, prepareControllerAnimationsQuery);
		else
			ss.RequireForUpdate(animatorControllerQuery);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle SchedulePrepareControllerAnimations(ref SystemState ss, EntityQuery query, in InternalAnimatorDataSingleton internalAnimatorData, JobHandle dependency)
	{
		var job = new PrepareControllerAnimationsJob
		{
			controllerLayersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerLayerComponent>(true),
			animatorOverrideAnimationHandle = ss.GetComponentTypeHandle<AnimatorOverrideAnimations>(true),
			cache = internalAnimatorData.animatorOverrideAnimationsMap
		};
		return job.Schedule(query, dependency);
	}
/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		var dt = SystemAPI.Time.DeltaTime;

		var controllerLayersBufferHandle = SystemAPI.GetBufferTypeHandle<AnimatorControllerLayerComponent>();
		var controllerParametersBufferHandle = SystemAPI.GetBufferTypeHandle<AnimatorControllerParameterComponent>();
		var animatorOverrideAnimationsLookup = SystemAPI.GetComponentLookup<AnimatorOverrideAnimations>(true);
		var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
		var controllerEventsBufferLookup = SystemAPI.GetBufferLookup<AnimatorControllerEventComponent>();
		var animDBSingleton = SystemAPI.GetSingleton<BlobDatabaseSingleton>();
		
		var internalDataSingletonQuery = SystemAPI.QueryBuilder().WithAllRW<InternalAnimatorDataSingleton>().Build();
		var internalAnimatorData = FillAnimationsFromControllerSystem.GetInternalDataSingleton(internalDataSingletonQuery, ref ss);
		ss.Dependency = JobHandle.CombineDependencies(ss.Dependency, internalAnimatorData.cacheDependency);
		ss.Dependency = SchedulePrepareControllerAnimations(ref ss, prepareControllerAnimationsQuery, internalAnimatorData, ss.Dependency);
		
		var stateMachineProcessJob = new StateMachineProcessJob()
		{
			controllerLayersBufferHandle = controllerLayersBufferHandle,
			controllerParametersBufferHandle = controllerParametersBufferHandle,
			dt = dt,
			entityTypeHandle = entityTypeHandle,
			controllerEventsBufferLookup = controllerEventsBufferLookup,
			animationDatabase = animDBSingleton.animations,
			animatorOverrideAnimationLookup = animatorOverrideAnimationsLookup,
			animatorOverrideAnimationsMap = internalAnimatorData.animatorOverrideAnimationsMap.AsReadOnly()
		};

		ss.Dependency = stateMachineProcessJob.ScheduleParallel(animatorControllerQuery, ss.Dependency);
		internalDataSingletonQuery.GetSingletonRW<InternalAnimatorDataSingleton>().ValueRW.cacheDependency = ss.Dependency;
	}
}

/////////////////////////////////////////////////////////////////////////////////////////////////////

public interface AnimatorControllerQueryCreator
{
	EntityQuery CreateQuery(ref SystemState ss);
}

/////////////////////////////////////////////////////////////////////////////////////////////////////

public struct PredictedAnimatorControllerQuery: AnimatorControllerQueryCreator
{
	public EntityQuery CreateQuery(ref SystemState ss)
	{
		var eqBuilder0 = new EntityQueryBuilder(Allocator.Temp)
		.WithAllRW<AnimatorControllerLayerComponent>()
	#if RUKHANKA_WITH_NETCODE
		.WithAll<Simulate, PredictedGhost>()
	#endif
		;
		var animatorControllerQuery = ss.GetEntityQuery(eqBuilder0);
		return animatorControllerQuery;
	}
}

/////////////////////////////////////////////////////////////////////////////////////////////////////

public struct AnimatorControllerQuery: AnimatorControllerQueryCreator
{
	public EntityQuery CreateQuery(ref SystemState ss)
	{
		var eqBuilder0 = new EntityQueryBuilder(Allocator.Temp)
		.WithAllRW<AnimatorControllerLayerComponent>()
	#if RUKHANKA_WITH_NETCODE
		.WithNone<GhostInstance>()
	#endif
		;
		var animatorControllerQuery = ss.GetEntityQuery(eqBuilder0);
		return animatorControllerQuery;
	}
}
}
