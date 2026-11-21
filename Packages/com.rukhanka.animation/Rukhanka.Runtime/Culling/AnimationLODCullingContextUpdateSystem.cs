using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rukhanka
{
[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
[UpdateInGroup(typeof(RukhankaDeformationSystemGroup))]
[UpdateBefore(typeof(SkinnedMeshPreparationSystem))]
public partial class AnimationLODCullingContextUpdateSystem: SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<AnimationCullingContext>();
    }

/////////////////////////////////////////////////////////////////////////////////

    protected override void OnUpdate()
    {
        var config = AnimationCullingConfig.Instance;
        if (config == null)
            return;

        // Native lists in singleton components are shared with simulation jobs.
        EntityManager.CompleteDependencyBeforeRW<AnimationCullingContext>();
        ref var context = ref SystemAPI.GetSingletonRW<AnimationCullingContext>().ValueRW;
        context.lodAffectors.Clear();
        context.maximumLODLevelMask = 1 << QualitySettings.maximumLODLevel;
        
        var lodBias = QualitySettings.lodBias;
        foreach (var camera in config.cullingCameras)
        {
            AddCamera(ref context, camera, lodBias);
        }
#if UNITY_EDITOR
        if (config.addEditorSceneCamera)
        {
            AddCamera(ref context, SceneView.lastActiveSceneView?.camera, lodBias);
        }
#endif
    }
    
/////////////////////////////////////////////////////////////////////////////////

    static void AddCamera(ref AnimationCullingContext context, Camera camera, float lodBias)
    {
        if (camera == null)
            return;

        var ccd = new AnimationCullingContextUpdateSystem.CullingCameraData
        {
            pos = camera.transform.position,
            fieldOfView = camera.fieldOfView,
            isOrthographic = camera.orthographic,
            orthographicSize = camera.orthographicSize
        };
        context.AddLODAffector(ccd, lodBias);
    }
}
}
