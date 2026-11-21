using Unity.Entities;
using UnityEngine;
#if RUKHANKA_SAMPLES_WITH_VFX_GRAPH
using UnityEngine.VFX;
#endif

///////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Samples
{
    
struct VFXSkinnedMeshSamplerComponent: IComponentData
{
    public Entity skinnedMeshEntity;
}

///////////////////////////////////////////////////////////////////////////////////////////

[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
partial class VFXSkinnedMeshSamplerSystem: SystemBase
{
    protected override void OnUpdate()
    {
    #if RUKHANKA_SAMPLES_WITH_VFX_GRAPH
        foreach (var (smsc, e) in SystemAPI.Query<VFXSkinnedMeshSamplerComponent>().WithEntityAccess())
        {
        #if UNITY_6000_6_OR_NEWER
            //  Entities 6.6+ stores companion components as CompanionComponent<T>; managed-component access is deprecated
            if (!EntityManager.HasComponent<CompanionComponent<VisualEffect>>(e))
                continue;
        #else
            if (!EntityManager.HasComponent<VisualEffect>(e))
                continue;
        #endif

            if (!EntityManager.HasComponent<DeformedMeshIndex>(smsc.skinnedMeshEntity))
                continue;

        #if UNITY_6000_6_OR_NEWER
            var vfx = EntityManager.GetCompanion<VisualEffect>(e);
        #else
            var vfx = EntityManager.GetComponentObject<VisualEffect>(e);
        #endif
            var deformedMeshIndex = EntityManager.GetComponentData<DeformedMeshIndex>(smsc.skinnedMeshEntity);
        #if RUKHANKA_ENABLE_DEFORMATION_MOTION_VECTORS
            //  Value.z selects current frame deformed mesh index slot (x or y)
            var dmi = deformedMeshIndex.Value[(int)deformedMeshIndex.Value.z];
        #else
            var dmi = deformedMeshIndex.Value;
        #endif
            vfx.SetInt("DeformedMeshIndex", (int)dmi);
        }
    #endif
    }
}
}

