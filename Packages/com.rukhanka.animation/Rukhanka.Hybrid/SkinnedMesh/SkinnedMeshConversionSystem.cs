using Unity;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateBefore(typeof(RenderMeshPostProcessSystem))]
partial class SkinnedMeshRenderArrayCleanupBakingSystem : SystemBase
{
    EntityQuery rebakedRenderers;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected override void OnCreate()
    {
        rebakedRenderers = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<SkinnedMeshRendererComponent>(),
                ComponentType.ReadOnly<RenderMeshUnmanaged>(),
                ComponentType.ReadOnly<RenderMeshArray>()
            },
            Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                      EntityQueryOptions.IgnoreComponentEnabledState
        });
        RequireForUpdate(rebakedRenderers);
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected override void OnUpdate()
    {
        // RenderMeshUnmanaged is temporary: only rebaked renderers have it.
        // Unity adds the new array by chunk, which skips chunks already carrying
        // that component. Remove their previous array so it matches the new MMI.
        EntityManager.RemoveComponent<RenderMeshArray>(rebakedRenderers);
    }
}

//-----------------------------------------------------------------------------------------------------------------//

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateInGroup(typeof(PostBakingSystemGroup))]
public partial class SkinnedMeshConversionSystem : SystemBase
{
	protected override unsafe void OnUpdate()
	{
		var actualizeSkinnedMeshDataJob = new ActualizeSkinnedMeshDataJob()
		{
			animEntityRefLookup = SystemAPI.GetComponentLookup<AnimatorEntityRefComponent>(true),
		};
		actualizeSkinnedMeshDataJob.ScheduleParallel();

		var ecb = new EntityCommandBuffer(Allocator.Temp);
		foreach (var (rma, e) in SystemAPI.Query<RenderMeshArray>().WithEntityAccess().WithAll<SkinnedMeshSplitSubmeshEntities>())
		{
			var eg = EntityManager.GetComponentData<EntityGuid>(e);
			for (var i = 0; i < rma.MaterialMeshIndices.Length; i++)
			{
				var originalSMR = i == 0;
				//	Modify original skinned mesh renderer to draw only first submesh
				var smrEntity = originalSMR ? e : ecb.Instantiate(e);
				
				var mmiForSubmesh = MaterialMeshInfo.FromMaterialMeshIndexRange(i, 1);
				mmiForSubmesh.Material = MaterialMeshInfo.ArrayIndexToStaticIndex(i);
				mmiForSubmesh.Mesh = MaterialMeshInfo.ArrayIndexToStaticIndex(0);
				ecb.SetComponent(smrEntity, mmiForSubmesh);
				
				if (!originalSMR)
				{
					//	Modify EntityGuid to prevent 'duplicated GUID' exceptions)
				#if UNITY_6000_4_OR_NEWER
					var osei = (int)EntityId.ToULong(eg.OriginatingSubEntityId) + 1;
					eg.OriginatingSubEntityId = *(EntityId*)&osei;
				#endif
					ecb.SetComponent(smrEntity, eg);
				}
				ecb.RemoveComponent<SkinnedMeshSplitSubmeshEntities>(smrEntity);
			}
		}
		ecb.Playback(EntityManager);
	}
}
}
