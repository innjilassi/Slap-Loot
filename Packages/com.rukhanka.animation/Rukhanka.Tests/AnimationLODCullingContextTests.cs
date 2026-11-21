using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class AnimationLODCullingContextTests
{
    // MonoBehaviour.Awake is not invoked by these EditMode tests.
    static void SetConfiguration(AnimationCullingConfig config) =>
        typeof(AnimationCullingConfig).GetProperty(nameof(AnimationCullingConfig.Instance)).SetValue(null, config);

    [Test]
    public void PresentationRefreshCompletesPendingSimulationContextBuild()
    {
        if (AnimationCullingConfig.Instance != null)
            Assert.Ignore("Requires a scene without an active animation culling configuration.");

        var owner = new GameObject("LOD culling dependency test");
        owner.SetActive(false);
        var camera = owner.AddComponent<Camera>();
        var config = owner.AddComponent<AnimationCullingConfig>();
        config.cullingCameras = new[] { camera };
        config.shadowCastingLights = new Light[0];
        config.addEditorSceneCamera = false;
        owner.SetActive(true);
        SetConfiguration(config);
        try
        {
            using var world = new World("LOD culling dependency test");
            var simulation = world.GetOrCreateSystemManaged<AnimationCullingContextUpdateSystem>();
            simulation.Update();
            // Do not complete simulation manually: presentation must synchronize the shared context.
            camera.transform.position = new Vector3(0, 0, 15);
            world.GetOrCreateSystemManaged<AnimationLODCullingContextUpdateSystem>().Update();

            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AnimationCullingContext>());
            var context = query.GetSingleton<AnimationCullingContext>();
            Assert.AreEqual(6, context.cullingPlanes.Length);
            Assert.AreEqual(1, context.lodAffectors.Length);
            Assert.AreEqual((float3)camera.transform.position, context.lodAffectors[0].cameraPos);
        }
        finally
        {
            SetConfiguration(null);
            Object.DestroyImmediate(owner);
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void PresentationUsesCurrentCameraParametersAndPreservesSimulationFrustum(bool orthographic)
    {
        // Keep the fixture independent of a scene's existing culling configuration.
        if (AnimationCullingConfig.Instance != null)
            Assert.Ignore("Requires a scene without an active animation culling configuration.");

        var owner = new GameObject("LOD culling test");
        owner.SetActive(false);
        var camera = owner.AddComponent<Camera>();
        var config = owner.AddComponent<AnimationCullingConfig>();
        config.cullingCameras = new[] { camera };
        config.shadowCastingLights = new Light[0];
        config.addEditorSceneCamera = false;
        owner.SetActive(true);
        SetConfiguration(config);

        using var world = new World("LOD culling context test");
        var context = new AnimationCullingContext
        {
            cullingPlanes = new NativeList<float4>(1, Allocator.Persistent),
            cullingVolumePlaneRanges = new NativeList<int2>(1, Allocator.Persistent),
            lodAffectors = new NativeList<LODGroupExtensions.LODParams>(1, Allocator.Persistent)
        };
        var oldLodBias = QualitySettings.lodBias;
        var oldMaximumLODLevel = QualitySettings.maximumLODLevel;
        try
        {
            var plane = new float4(1, 2, 3, 4);
            var planeRange = new int2(0, 1);
            context.cullingPlanes.Add(plane);
            context.cullingVolumePlaneRanges.Add(planeRange);
            context.AddLODAffector(new AnimationCullingContextUpdateSystem.CullingCameraData
            {
                pos = new float3(0, 0, 3), fieldOfView = 60,
                isOrthographic = orthographic, orthographicSize = 2
            }, 1);
            world.EntityManager.CreateSingleton(context);

            // Simulate camera changes in LateUpdate, after simulation captured its inputs.
            camera.transform.position = new Vector3(0, 0, 15);
            camera.fieldOfView = 75;
            camera.orthographic = orthographic;
            camera.orthographicSize = 8;
            QualitySettings.lodBias = 1.5f;
            QualitySettings.maximumLODLevel = 1;

            var presentation = world.GetOrCreateSystemManaged<AnimationLODCullingContextUpdateSystem>();
            presentation.Update();

            using var contextQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AnimationCullingContext>());
            Assert.AreEqual(2, contextQuery.GetSingleton<AnimationCullingContext>().maximumLODLevelMask);

            Assert.AreEqual(1, context.lodAffectors.Length);
            var actual = context.lodAffectors[0];
            var expected = LODGroupExtensions.CalculateLODParams(new LODParameters
            {
                cameraPosition = camera.transform.position,
                fieldOfView = camera.fieldOfView,
                isOrthographic = orthographic,
                orthoSize = camera.orthographicSize
            });
            Assert.AreEqual(expected.cameraPos, actual.cameraPos);
            Assert.AreEqual(expected.isOrtho, actual.isOrtho);
            Assert.AreEqual(expected.orthosize, actual.orthosize);
            Assert.That(actual.distanceScale, Is.EqualTo(expected.distanceScale).Within(0.00001f));
            Assert.AreEqual(plane, context.cullingPlanes[0]);
            Assert.AreEqual(planeRange, context.cullingVolumePlaneRanges[0]);

            // Repeated presentation updates replace the snapshot rather than accumulating cameras.
            camera.transform.position = new Vector3(0, 0, 4);
            QualitySettings.maximumLODLevel = 2;
            presentation.Update();
            Assert.AreEqual(4, contextQuery.GetSingleton<AnimationCullingContext>().maximumLODLevelMask);
            Assert.AreEqual(1, context.lodAffectors.Length);
            Assert.AreEqual((float3)camera.transform.position, context.lodAffectors[0].cameraPos);
        }
        finally
        {
            QualitySettings.lodBias = oldLodBias;
            QualitySettings.maximumLODLevel = oldMaximumLODLevel;
            context.Dispose();
            SetConfiguration(null);
            Object.DestroyImmediate(owner);
        }
    }
}
}
