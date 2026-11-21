using Rukhanka.WaybackMachine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{ 

[DisableAutoCreation]
[BurstCompile]
[UpdateAfter(typeof(AnimatorControllerSystem<AnimatorControllerQuery>))]
public partial struct FillAnimationsFromControllerSystem: ISystem
{
	EntityQuery fillAnimationsBufferQuery;
	
/////////////////////////////////////////////////////////////////////////////////////////////////////
	
	public void OnCreate(ref SystemState ss)
	{
		fillAnimationsBufferQuery = SystemAPI.QueryBuilder()
			.WithAll<AnimatorControllerLayerComponent, AnimationToProcessComponent>()
			.Build();
		
		ss.RequireForUpdate(fillAnimationsBufferQuery);
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////
	
	[BurstCompile]
	public void OnDestroy(ref SystemState ss)
	{
		if (SystemAPI.TryGetSingletonRW<InternalAnimatorDataSingleton>(out var internalDataSingleton))
			internalDataSingleton.ValueRW.Dispose();
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
		var controllerLayersBufferHandleRO = SystemAPI.GetBufferTypeHandle<AnimatorControllerLayerComponent>(true);
		var controllerParametersBufferHandleRO = SystemAPI.GetBufferTypeHandle<AnimatorControllerParameterComponent>(true);
		var animatorOverrideAnimationsLookup = SystemAPI.GetComponentLookup<AnimatorOverrideAnimations>(true);
		var animationToProcessBufferHandle = SystemAPI.GetBufferTypeHandle<AnimationToProcessComponent>();
		var animDBSingleton = SystemAPI.GetSingleton<BlobDatabaseSingleton>();

		var internalDataSingletonQuery = SystemAPI.QueryBuilder().WithAllRW<InternalAnimatorDataSingleton>().Build();
		var internalAnimatorData = GetInternalDataSingleton(internalDataSingletonQuery, ref ss);
		ss.Dependency = JobHandle.CombineDependencies(ss.Dependency, internalAnimatorData.cacheDependency);
		
		var fillAnimationsBufferJob = new FillAnimationsBufferJob()
		{
			controllerLayersBufferHandle = controllerLayersBufferHandleRO,
			controllerParametersBufferHandle = controllerParametersBufferHandleRO,
			animationToProcessBufferHandle = animationToProcessBufferHandle,
			animatorOverrideAnimationLookup = animatorOverrideAnimationsLookup,
			entityTypeHandle = entityTypeHandle,
			animationDatabase = animDBSingleton.animations,
			avatarMaskDatabase = animDBSingleton.avatarMasks,
			animatorOverrideAnimationsMap = internalAnimatorData.animatorOverrideAnimationsMap.AsReadOnly()
		};

		ss.Dependency = fillAnimationsBufferJob.ScheduleParallel(fillAnimationsBufferQuery, ss.Dependency);
		ss.Dependency = CopyEventsForWaybackMachineDuringRecording(ref ss, ss.Dependency);
		internalDataSingletonQuery.GetSingletonRW<InternalAnimatorDataSingleton>().ValueRW.cacheDependency = ss.Dependency;
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////

	internal static InternalAnimatorDataSingleton GetInternalDataSingleton(EntityQuery singletonQuery, ref SystemState ss)
	{
		if (singletonQuery.TryGetSingletonRW<InternalAnimatorDataSingleton>(out var internalAnimatorData))
			return internalAnimatorData.ValueRW;
		
		var iads = InternalAnimatorDataSingleton.MakeDefault();
		ss.EntityManager.CreateSingleton(iads, "Rukhanka.InternalAnimatorDataSingleton");
		return iads;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	internal static BlobAssetReference<ControllerAnimationsBlob> GetControllerAnimationsBlob
	(
		Entity e,
		ComponentLookup<AnimatorOverrideAnimations> animatorOverrideAnimationLookup,
		BlobAssetReference<ControllerAnimationsBlob> cab,
		NativeParallelHashMap<ControllerAnimationsCacheKey, BlobAssetReference<ControllerAnimationsBlob>>.ReadOnly animatorOverrideAnimationsMap
	)
	{
		if (animatorOverrideAnimationLookup.TryGetComponent(e, out var animationOverrides) && animatorOverrideAnimationLookup.IsComponentEnabled(e))
		{
			var key = new ControllerAnimationsCacheKey { controller = cab, overrides = animationOverrides.value };
			return animatorOverrideAnimationsMap[key];
		}
		return cab;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////
 
	JobHandle CopyEventsForWaybackMachineDuringRecording(ref SystemState ss, JobHandle dependsOn)
	{
		if (!SystemAPI.TryGetSingletonRW<RecordComponent>(out var rcd))
			return dependsOn;
		
		var copyEventsToWaybackMachineJob = new CopyAnimatorEventsToWaybackMachineRecordingJob()
		{
			outEvents = rcd.ValueRW.wbData.Value.emittedAnimatorEvents
		};
		var jh = copyEventsToWaybackMachineJob.Schedule(dependsOn);
		return jh;
	}
}
}
