using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rukhanka.Hybrid;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
// Referencing saved prefabs exercises Unity's real prefab baking path.
public class SkinnedCatalogTestAuthoring : MonoBehaviour
{
    public GameObject[] prefabs;
}

/////////////////////////////////////////////////////////////////////////////////

public class SkinnedCatalogTestBaker : Baker<SkinnedCatalogTestAuthoring>
{

/////////////////////////////////////////////////////////////////////////////////

    public override void Bake(SkinnedCatalogTestAuthoring authoring)
    {
        foreach (var prefab in authoring.prefabs)
            GetEntity(prefab, TransformUsageFlags.Dynamic);
    }
}

/////////////////////////////////////////////////////////////////////////////////

public class SkinnedMeshRenderArrayBakingTests
{
    const string AssetFolder = "Assets/__RukhankaRenderArrayTests";
    const BindingFlags Methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Object> assets = new();
    World world;
    EntityManager manager;
    BlobAssetStore store;
    Scene scene;
    object settings;
    IDisposable changes;
    Type utility;
    bool createdAssetFolder;

/////////////////////////////////////////////////////////////////////////////////

    [SetUp]
    public void SetUp()
    {
        createdAssetFolder = false;
        world = new World("Native skinned render array baking tests");
        manager = world.EntityManager;
        store = new BlobAssetStore(128);
        scene = EditorSceneManager.NewPreviewScene();
        var assembly = typeof(Baker<>).Assembly;
        utility = assembly.GetType("Unity.Entities.BakingUtility", true);
        var settingsType = assembly.GetType("Unity.Entities.BakingSettings", true);
        var flags = utility.GetNestedType("BakingFlags");
        settings = Activator.CreateInstance(settingsType, Enum.ToObject(flags, 1 | 4 | 64), store);
        settingsType.GetField("SceneGUID").SetValue(settings, new Unity.Entities.Hash128(1, 2, 3, 4));
        changes = (IDisposable)Activator.CreateInstance(assembly.GetType("Unity.Entities.IncrementalBakingChangeTracker", true));
    }

/////////////////////////////////////////////////////////////////////////////////

    [TearDown]
    public void TearDown()
    {
        world.Dispose();
        store.Dispose();
        changes?.Dispose();
        if (scene.IsValid())
            EditorSceneManager.ClosePreviewScene(scene);
        foreach (var asset in assets)
            if (asset != null && !EditorUtility.IsPersistent(asset))
                Object.DestroyImmediate(asset);
        assets.Clear();
        if (createdAssetFolder)
            AssetDatabase.DeleteAsset(AssetFolder);
    }

/////////////////////////////////////////////////////////////////////////////////

    Material Material()
    {
        var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
        assets.Add(material);
        return material;
    }

/////////////////////////////////////////////////////////////////////////////////

    Mesh Mesh()
    {
        var mesh = new Mesh
        {
            vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
            subMeshCount = 2
        };
        mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
        mesh.SetTriangles(new[] { 0, 2, 1 }, 1);
        assets.Add(mesh);
        return mesh;
    }

/////////////////////////////////////////////////////////////////////////////////

    GameObject Root(string name)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

/////////////////////////////////////////////////////////////////////////////////

    SkinnedMeshRenderer Renderer(bool split, Mesh mesh, Material[] materials)
    {
        var root = Root("Skinned renderer");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterials = materials;
        if (split)
            root.AddComponent(typeof(SkinnedMeshBaker).Assembly.GetType("Rukhanka.Hybrid.SkinnedMeshSplitOnSubmeshEntitiesAuthoring", true));
        return renderer;
    }

/////////////////////////////////////////////////////////////////////////////////

    void Bake(bool incremental = false)
    {
        // Reflection confines Unity's internal baking entry points to this test fixture.
        utility.GetMethod("BakeScene", Methods).Invoke(null, new[] { (object)world, scene, settings, incremental, changes });
        changes.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(changes, null);
    }

/////////////////////////////////////////////////////////////////////////////////

    void MarkChanged(Component component)
    {
        changes.GetType().GetMethod("MarkComponentChanged").Invoke(changes, new object[] { component });
    }

/////////////////////////////////////////////////////////////////////////////////

    void MarkRemoved(GameObject root)
    {
        var method = changes.GetType().GetMethod("MarkRemoved");
        // Entities 1.4 uses instance IDs; newer versions use EntityId.
    #if UNITY_6000_4_OR_NEWER
        object id = root.GetEntityId();
        if (method.GetParameters()[0].ParameterType == typeof(int))
            id = (int)EntityId.ToULong(root.GetEntityId());
    #else
        object id = root.GetInstanceID();
    #endif
        method.Invoke(changes, new[] { id });
        Object.DestroyImmediate(root);
    }

/////////////////////////////////////////////////////////////////////////////////

    EntityQuery Renderers()
    {
        return manager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<SkinnedMeshRendererComponent>(), ComponentType.ReadOnly<RenderMeshArray>(), ComponentType.ReadOnly<MaterialMeshInfo>() },
            Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IgnoreComponentEnabledState
        });
    }

/////////////////////////////////////////////////////////////////////////////////

    RenderMeshArray Array(Entity entity)
    {
    #if UNITY_6000_6_OR_NEWER
        return manager.GetSharedComponent<RenderMeshArray>(entity);
    #else
        return manager.GetSharedComponentManaged<RenderMeshArray>(entity);
    #endif
    }

/////////////////////////////////////////////////////////////////////////////////

    void AssertAssignments(Dictionary<Mesh, Material[]> expected, bool split, bool oneArray)
    {
        using var query = Renderers();
        using var entities = query.ToEntityArray(Allocator.Temp);
        var count = 0;
        foreach (var materials in expected.Values)
            count += split ? Math.Max(1, materials.Length) : 1;
        Assert.AreEqual(count, entities.Length);
        var seen = new Dictionary<Mesh, HashSet<int>>();
        var hashes = new HashSet<Unity.Mathematics.uint4>();
        foreach (var entity in entities)
        {
            var array = Array(entity);
            hashes.Add(array.GetHash128());
            var info = manager.GetComponentData<MaterialMeshInfo>(entity);
            var mesh = array.GetMesh(info);
            Assert.IsTrue(expected.ContainsKey(mesh), "Unexpected or stale mesh assignment");
            var materials = expected[mesh];
            if (!seen.TryGetValue(mesh, out var submeshes))
                seen.Add(mesh, submeshes = new HashSet<int>());
            if (split || materials.Length <= 1)
            {
                Assert.IsFalse(info.HasMaterialMeshIndexRange);
                Assert.IsTrue(submeshes.Add(info.SubMesh), "Duplicate submesh");
                CollectionAssert.AreEqual(new[] { materials.Length > 0 ? materials[info.SubMesh] : null }, array.GetMaterials(info));
            }
            else
            {
                Assert.IsTrue(info.HasMaterialMeshIndexRange);
                CollectionAssert.AreEqual(materials, array.GetMaterials(info));
                var range = info.MaterialMeshIndexRange;
                for (var i = 0; i < range.length; ++i)
                {
                    var entry = array.MaterialMeshIndices[range.start + i];
                    Assert.AreEqual(i, entry.SubMeshIndex);
                    Assert.AreSame(mesh, array.MeshReferences[entry.MeshIndex].Value);
                    Assert.IsTrue(submeshes.Add(i));
                }
            }
            Assert.IsTrue(manager.HasComponent<DeformedMeshIndex>(entity));
            Assert.IsFalse(manager.HasComponent<RenderMeshUnmanaged>(entity), "Temporary baking input must be stripped");
        }
        foreach (var pair in expected)
            Assert.AreEqual(Math.Max(1, pair.Value.Length), seen[pair.Key].Count);
        if (oneArray)
            Assert.AreEqual(1, hashes.Count, "Clean baking should consolidate the section");
        TestContext.WriteLine($"Renderers: {entities.Length}; referenced arrays: {hashes.Count}");
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void CleanBakeSharesArrayAndPreservesSubmeshAssignments(bool split)
    {
        var red = Material();
        var blue = Material();
        var a = Mesh();
        var b = Mesh();
        Renderer(split, a, new[] { red, blue });
        Renderer(split, b, new[] { blue, red });
        Bake();
        AssertAssignments(new Dictionary<Mesh, Material[]> { { a, new[] { red, blue } }, { b, new[] { blue, red } } }, split, true);
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(0)]
    [TestCase(1)]
    public void ZeroOrOneMaterialUsesDirectIndices(int materialCount)
    {
        var mesh = Mesh();
        var materials = materialCount == 0 ? new Material[0] : new[] { Material() };
        Renderer(false, mesh, materials);
        Bake();
        AssertAssignments(new Dictionary<Mesh, Material[]> { { mesh, materials } }, false, true);
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void IncrementalMeshMaterialEditAndDeletionPreserveOtherRenderers(bool split)
    {
        var red = Material();
        var blue = Material();
        var a = Mesh();
        var b = Mesh();
        Renderer(split, a, new[] { red, blue });
        var changed = Renderer(split, b, new[] { blue, red });
        Bake();
        var expected = new Dictionary<Mesh, Material[]> { { a, new[] { red, blue } }, { b, new[] { blue, red } } };
        AssertAssignments(expected, split, true);
        using (var query = Renderers())
        using (var entities = query.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
                if (Array(entity).GetMesh(manager.GetComponentData<MaterialMeshInfo>(entity)) == a)
                    manager.SetComponentEnabled<MaterialMeshInfo>(entity, false);
        var replacement = Mesh();
        changed.sharedMesh = replacement;
        changed.sharedMaterials = new[] { red, red };
        MarkChanged(changed);
        Bake(true);
        expected.Remove(b);
        expected.Add(replacement, new[] { red, red });
        AssertAssignments(expected, split, false);
        // Change cardinality to exercise removal of the extra-material buffer/additional entities.
        changed.sharedMaterials = new[] { blue };
        MarkChanged(changed);
        Bake(true);
        expected[replacement] = new[] { blue };
        AssertAssignments(expected, split, false);
        changed.sharedMaterials = new[] { blue, red };
        MarkChanged(changed);
        Bake(true);
        expected[replacement] = new[] { blue, red };
        AssertAssignments(expected, split, false);
        MarkRemoved(changed.gameObject);
        Bake(true);
        expected.Remove(replacement);
        AssertAssignments(expected, split, false);
        using (var query = Renderers())
        using (var entities = query.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
                Assert.IsFalse(manager.IsComponentEnabled<MaterialMeshInfo>(entity));
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void InactiveRenderersAreConsolidated(bool split)
    {
        var material = Material();
        var a = Mesh();
        var b = Mesh();
        Renderer(split, a, new[] { material, material }).gameObject.SetActive(false);
        Renderer(split, b, new[] { material, material });
        Bake();
        AssertAssignments(new Dictionary<Mesh, Material[]> { { a, new[] { material, material } }, { b, new[] { material, material } } }, split, true);
        using var query = Renderers();
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
            Assert.AreEqual(Array(entity).GetMesh(manager.GetComponentData<MaterialMeshInfo>(entity)) == a, manager.HasComponent<Disabled>(entity));
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void DifferentSceneSectionsKeepSeparateArrays(bool split)
    {
        var material = Material();
        var a = Mesh();
        var b = Mesh();
        Renderer(split, a, new[] { material, material });
        Renderer(split, b, new[] { material, material }).gameObject.AddComponent<SceneSectionComponent>().SectionIndex = 1;
        Bake();
        AssertAssignments(new Dictionary<Mesh, Material[]> { { a, new[] { material, material } }, { b, new[] { material, material } } }, split, false);
        using var query = Renderers();
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var array = Array(entity);
            Assert.AreEqual(1, array.MeshReferences.Length);
            var section = manager.GetSharedComponent<SceneSection>(entity);
            Assert.AreEqual(array.GetMesh(manager.GetComponentData<MaterialMeshInfo>(entity)) == a ? 0 : 1, section.Section);
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void ConsolidationPreservesLODGroupAndMasks(bool split)
    {
        var material = Material();
        var a = Mesh();
        var b = Mesh();
        var first = Renderer(split, a, new[] { material, material });
        var second = Renderer(split, b, new[] { material, material });
        var root = Root("LOD group");
        first.transform.SetParent(root.transform);
        second.transform.SetParent(root.transform);
        var group = root.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.6f, new UnityEngine.Renderer[] { first }), new LOD(0.2f, new UnityEngine.Renderer[] { second }) });
        Bake();
        AssertAssignments(new Dictionary<Mesh, Material[]> { { a, new[] { material, material } }, { b, new[] { material, material } } }, split, true);
        using var query = Renderers();
        using var entities = query.ToEntityArray(Allocator.Temp);
        Entity lodGroup = Entity.Null;
        foreach (var entity in entities)
        {
            var lod = manager.GetComponentData<MeshLODComponent>(entity);
            Assert.AreNotEqual(Entity.Null, lod.Group);
            if (lodGroup == Entity.Null)
                lodGroup = lod.Group;
            Assert.AreEqual(lodGroup, lod.Group);
            var mesh = Array(entity).GetMesh(manager.GetComponentData<MaterialMeshInfo>(entity));
            Assert.AreEqual(mesh == a ? 1 : 2, lod.LODMask);
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void CleanBakeSharesArrayWithOrdinaryMeshRenderer()
    {
        var material = Material();
        var skinnedMesh = Mesh();
        var ordinaryMesh = Mesh();
        Renderer(false, skinnedMesh, new[] { material });
        var ordinary = Root("Ordinary renderer");
        ordinary.AddComponent<MeshFilter>().sharedMesh = ordinaryMesh;
        ordinary.AddComponent<MeshRenderer>().sharedMaterial = material;
        Bake();
        AssertAssignments(new Dictionary<Mesh, Material[]> { { skinnedMesh, new[] { material } } }, false, true);
        using var query = Renderers();
        using var entities = query.ToEntityArray(Allocator.Temp);
        var shared = Array(entities[0]);
        Assert.AreEqual(2, shared.MeshReferences.Length);
        Assert.AreEqual(1, shared.MaterialReferences.Length);
        using var allRenderers = manager.CreateEntityQuery(typeof(RenderMeshArray), typeof(MaterialMeshInfo));
        using var all = allRenderers.ToEntityArray(Allocator.Temp);
        Assert.AreEqual(2, all.Length);
        foreach (var entity in all)
            Assert.AreEqual(shared, Array(entity));
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(false)]
    [TestCase(true)]
    public void PrefabOnlyCatalogSharesOneArrayAcross128DistinctMeshes(bool split)
    {
        Assert.IsFalse(AssetDatabase.IsValidFolder(AssetFolder), "Test asset folder already exists");
        AssetDatabase.CreateFolder("Assets", "__RukhankaRenderArrayTests");
        createdAssetFolder = true;
        var material = Material();
        AssetDatabase.CreateAsset(material, AssetFolder + "/Material.mat");
        var expected = new Dictionary<Mesh, Material[]>();
        var prefabs = new GameObject[128];
        for (var i = 0; i < prefabs.Length; ++i)
        {
            var mesh = Mesh();
            AssetDatabase.CreateAsset(mesh, $"{AssetFolder}/Mesh{i}.asset");
            var renderer = Renderer(split, mesh, new[] { material, material });
            prefabs[i] = PrefabUtility.SaveAsPrefabAsset(renderer.gameObject, $"{AssetFolder}/Renderer{i}.prefab");
            Object.DestroyImmediate(renderer.gameObject);
            expected.Add(mesh, new[] { material, material });
        }
        Root("Catalog").AddComponent<SkinnedCatalogTestAuthoring>().prefabs = prefabs;
        Bake();
        AssertAssignments(expected, split, true);
        using var query = Renderers();
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
            Assert.IsTrue(manager.HasComponent<Prefab>(entity));
    }
}
}
