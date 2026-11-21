using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[DisableAutoCreation]
[UpdateAfter(typeof(RukhankaAnimationInjectionSystemGroup))]
partial struct AnimationApplicationSystem: ISystem
{
	private EntityQuery
		boneObjectEntitiesWithParentQuery,
		boneObjectEntitiesNoParentQuery;

	NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;
	int newRemapTablesCounter;

	struct NewTableCreationCommand
	{
		public Hash128 hash;
		public Entity smrEntity;
	}

/////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnCreate(ref SystemState ss)
	{
		var eqb0 = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent, Parent>()
			.WithAllRW<LocalTransform>();
		boneObjectEntitiesWithParentQuery = ss.GetEntityQuery(eqb0);

		var eqb1 = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent>()
			.WithNone<Parent>()
			.WithAllRW<LocalTransform>();
		boneObjectEntitiesNoParentQuery = ss.GetEntityQuery(eqb1);
		
		var rq = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent>()
			.Build(ref ss);
		ss.RequireForUpdate(rq);

		rigToSkinnedMeshRemapTables = new (128, Allocator.Persistent);
	}
	
/////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnDestroy(ref SystemState ss)
	{
		foreach (var kv in rigToSkinnedMeshRemapTables)
			kv.Value.Dispose();	
		rigToSkinnedMeshRemapTables.Dispose();
	}

/////////////////////////////////////////////////////////////////////////////////

    [BurstCompile]
    public void OnUpdate(ref SystemState ss)
    {
		ref var runtimeData = ref SystemAPI.GetSingletonRW<RuntimeAnimationData>().ValueRW;

		var fillRigToSkinnedMeshRemapTablesJH = FillRigToSkinBonesRemapTableCache(ref ss, ss.Dependency);
		
		//	Propagate local animated transforms to the entities with and without parents
		var propagateTRSToEntitiesWithParentsJH = PropagateAnimatedBonesToEntitiesTRS(ref ss, runtimeData, boneObjectEntitiesWithParentQuery, true, ss.Dependency);
		var propagateTRSToEntitiesNoParentsJH = PropagateAnimatedBonesToEntitiesTRS(ref ss, runtimeData, boneObjectEntitiesNoParentQuery, false, propagateTRSToEntitiesWithParentsJH);
		
		//	Make corresponding skin matrices for all skinned meshes
		var jh = JobHandle.CombineDependencies(fillRigToSkinnedMeshRemapTablesJH, propagateTRSToEntitiesNoParentsJH);
		var applySkinJH = ApplySkinning(ref ss, runtimeData, jh);

		//	Update render bounds for meshes that request this
		var updateRenderBoundsJH = UpdateRenderBounds(ref ss, runtimeData, ss.Dependency);

		ss.Dependency = JobHandle.CombineDependencies(applySkinJH, updateRenderBoundsJH);
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe JobHandle FillRigToSkinBonesRemapTableCache(ref SystemState ss, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);
		var skinnedMeshRendererComponentLookup = SystemAPI.GetComponentLookup<SkinnedMeshRendererComponent>(true);

		newRemapTablesCounter = 0;
		var newTablesCounterPtr = (int*)UnsafeUtility.AddressOf(ref newRemapTablesCounter);
		var frameNewRemapTables = new NativeParallelHashMap<Hash128, Entity>(0xff, ss.WorldUpdateAllocator);
		var frameNewRemapTablesArray = new NativeList<NewTableCreationCommand>(0xff, ss.WorldUpdateAllocator);
		
		//	Count new remap tables count
		var countNumberOfNewRemapTablesJob = new CountNumberOfNewRemapTablesJob()
		{
			rigDefinitionArr = rigDefinitionComponentLookup,
			numberOfNewRemapTables = new UnsafeAtomicCounter32(UnsafeUtility.AddressOf(ref newRemapTablesCounter)),
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables
		};
		var countNumberOfNewRemapTablesJH = countNumberOfNewRemapTablesJob.ScheduleParallel(dependsOn);
		
		//	Reserve necessary space in remap table cache
		var increaseRigRemapTableCapacityJob = new IncreaseRigRemapTableCapacityJob()
		{
			numberOfNewRemapTables = newTablesCounterPtr,
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
			frameNewRemapTables = frameNewRemapTables
		};
		var	increaseRigRemapTableCapacityJH = increaseRigRemapTableCapacityJob.Schedule(countNumberOfNewRemapTablesJH);
		
		//	Fill new tables creation commands
		var createTableCreationCommandsJob = new CreateTableCreationCommandsJob()
		{
			frameNewRemapTables = frameNewRemapTables.AsParallelWriter(),
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
			rigDefinitionArr = rigDefinitionComponentLookup,
			newRemapTablesCounter = newTablesCounterPtr,
		};
		var createTableCreationCommandsJH = createTableCreationCommandsJob.ScheduleParallel(increaseRigRemapTableCapacityJH);
		
		//	Copy creation requests to array
		var copyTableCreateCommandsToArraysJob = new CopyTableCreateCommandsToArraysJob()
		{
			frameNewRemapTables = frameNewRemapTables,
			newRemapTableCreateCommands = frameNewRemapTablesArray
		};
		var copyTableCreateCommandsToArraysJH = copyTableCreateCommandsToArraysJob.Schedule(createTableCreationCommandsJH);
		
		//	Fill table cache with new tables
		var fillRigToSkinBonesRemapTableCacheJob = new FillRigToSkinBonesRemapTableCacheJob()
		{
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables.AsParallelWriter(),
			newRemapTableCreateCommands = frameNewRemapTablesArray,
			rigDefinitionLookup = rigDefinitionComponentLookup,
			skinnedMeshRendererLookup = skinnedMeshRendererComponentLookup
		};

		var rv = fillRigToSkinBonesRemapTableCacheJob.Schedule(frameNewRemapTablesArray, 4, copyTableCreateCommandsToArraysJH);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle UpdateRenderBounds(ref SystemState ss, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var updateSkinnedMeshBoundsJob = new UpdateSkinnedMeshBoundsJob()
		{
			worldBonePoses = runtimeData.worldSpaceBonesBuffer,
			rigDefLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true)
		};
		
		var updateSkinnedMeshBoundsJH = updateSkinnedMeshBoundsJob.ScheduleParallel(dependsOn);
		return updateSkinnedMeshBoundsJH;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle PropagateAnimatedBonesToEntitiesTRS(ref SystemState ss, in RuntimeAnimationData runtimeData, EntityQuery eq, bool withParents, JobHandle dependsOn)
	{
		var propagateAnimationJob = new PropagateBoneTransformToEntityTRSJob()
		{
			boneTransforms = withParents ? runtimeData.animatedBonesBuffer : runtimeData.worldSpaceBonesBuffer,
			postTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(),
			rigDefLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true),
			gpuEngineTagLookup = SystemAPI.GetComponentLookup<GPUAnimationEngineTag>(true)
		};

		var jh = propagateAnimationJob.ScheduleParallel(eq, dependsOn);
		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle ApplySkinning(ref SystemState ss, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);
		var cullAnimationsTagComponentLookup = SystemAPI.GetComponentLookup<CullAnimationsTag>(true);
		var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
		var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
		var rigBoneLookup = SystemAPI.GetComponentLookup<AnimatorEntityRefComponent>(true);
		var gpuAnimationEngineLookup = SystemAPI.GetComponentLookup<GPUAnimationEngineTag>(true);

		var animationApplyJob = new ApplyAnimationToSkinnedMeshJob()
		{
			boneTransforms = runtimeData.worldSpaceBonesBuffer,
			rigDefinitionLookup = rigDefinitionComponentLookup,
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
			cullAnimationsTagLookup = cullAnimationsTagComponentLookup,
			localTransformLookup = localTransformLookup,
			parentLookup = parentLookup,
			rigBoneLookup = rigBoneLookup,
			gpuAnimationEngineTagLookup = gpuAnimationEngineLookup
		};
		
		var jh = animationApplyJob.ScheduleParallel(dependsOn);
		return jh;
	}
}
}
