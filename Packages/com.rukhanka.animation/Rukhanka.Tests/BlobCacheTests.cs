using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rukhanka.Hybrid;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class BlobCacheTests
{
    string assetFolder;
    readonly List<string> cacheFiles = new List<string>();

/////////////////////////////////////////////////////////////////////////////////

    [SetUp]
    public void SetUp()
    {
#if RUKHANKA_NO_BLOB_CACHE
        Assert.Ignore("Blob caching is disabled.");
#endif
        var folderName = "RukhankaBlobCacheTests_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folderName);
        assetFolder = "Assets/" + folderName;
    }

/////////////////////////////////////////////////////////////////////////////////

    [TearDown]
    public void TearDown()
    {
        foreach (var path in cacheFiles)
            File.Delete(path);
        cacheFiles.Clear();
        if (assetFolder != null)
            AssetDatabase.DeleteAsset(assetFolder);
    }

/////////////////////////////////////////////////////////////////////////////////

    AnimationClip CreateClip(string name)
    {
        var clip = new AnimationClip { name = name };
        AssetDatabase.CreateAsset(clip, assetFolder + "/" + name + ".anim");
        return clip;
    }

/////////////////////////////////////////////////////////////////////////////////

    static BlobAssetReference<T> MakeBlob<T>() where T: unmanaged
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        builder.ConstructRoot<T>();
        return builder.CreateBlobAssetReference<T>(Allocator.Persistent);
    }

/////////////////////////////////////////////////////////////////////////////////

    static void ChangeClip(AnimationClip clip)
    {
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
            AnimationCurve.Linear(0, 0, 1, 2));
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(clip), ImportAssetOptions.ForceSynchronousImport);
    }

/////////////////////////////////////////////////////////////////////////////////

    static void AssertCacheMiss<T>(BlobAssetReference<T> blob) where T: unmanaged
    {
        try
        {
            Assert.IsFalse(blob.IsCreated);
        }
        finally
        {
            if (blob.IsCreated)
                blob.Dispose();
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void AnimationCacheValidatesLazilyAndIgnoresUnrelatedImports()
    {
        var clip = CreateClip("clip");
        var unrelated = CreateClip("unrelated");
        var path = BlobCache.ConstructCachedAnimationBlobPath(clip, null);
        cacheFiles.Add(path);
        using var blob = MakeBlob<AnimationClipBlob>();
        BlobCache.SaveBakedAnimationToCache(clip, null, blob);
        using (var loaded = BlobCache.LoadBakedAnimationFromCache(clip, null))
            Assert.IsTrue(loaded.IsCreated);

        ChangeClip(unrelated);
        using (var loaded = BlobCache.LoadBakedAnimationFromCache(clip, null))
            Assert.IsTrue(loaded.IsCreated);

        ChangeClip(clip);
        Assert.IsTrue(File.Exists(path), "Imports should no longer eagerly delete cache files.");
        AssertCacheMiss(BlobCache.LoadBakedAnimationFromCache(clip, null));

        BlobCache.SaveBakedAnimationToCache(clip, null, blob);
        Assert.AreEqual(path, BlobCache.ConstructCachedAnimationBlobPath(clip, null));
        using (var loaded = BlobCache.LoadBakedAnimationFromCache(clip, null))
            Assert.IsTrue(loaded.IsCreated);
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void AvatarImporterChangeInvalidatesAnimationCache()
    {
        var clip = CreateClip("clip");
        var root = new GameObject("Root");
        Avatar avatar;
        try
        {
            avatar = AvatarBuilder.BuildGenericAvatar(root, "");
            avatar.name = "avatar";
            AssetDatabase.CreateAsset(avatar, assetFolder + "/avatar.asset");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
        var path = BlobCache.ConstructCachedAnimationBlobPath(clip, avatar);
        cacheFiles.Add(path);
        using var blob = MakeBlob<AnimationClipBlob>();
        BlobCache.SaveBakedAnimationToCache(clip, avatar, blob);
        using (var loaded = BlobCache.LoadBakedAnimationFromCache(clip, avatar))
            Assert.IsTrue(loaded.IsCreated);

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(avatar));
        importer.userData = "Changed avatar import settings";
        importer.SaveAndReimport();
        Assert.AreEqual(path, BlobCache.ConstructCachedAnimationBlobPath(clip, avatar));
        AssertCacheMiss(BlobCache.LoadBakedAnimationFromCache(clip, avatar));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void ReferencedClipChangeInvalidatesControllerCache()
    {
        var clip = CreateClip("clip");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(assetFolder + "/controller.controller");
        controller.AddMotion(clip);
        AssetDatabase.SaveAssets();
        var path = BlobCache.ConstructCachedControllerBlobPath(controller);
        cacheFiles.Add(path);
        using var blob = MakeBlob<ControllerBlob>();
        BlobCache.SaveBakedControllerToCache(controller, blob);
        using (var loaded = BlobCache.LoadBakedControllerFromCache(controller))
            Assert.IsTrue(loaded.IsCreated);

        ChangeClip(clip);
        Assert.IsTrue(File.Exists(path));
        AssertCacheMiss(BlobCache.LoadBakedControllerFromCache(controller));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void LegacyCacheVersionIsRejected()
    {
        var clip = CreateClip("clip");
        var path = BlobCache.ConstructCachedAnimationBlobPath(clip, null);
        cacheFiles.Add(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var writer = new System.IO.BinaryWriter(File.Create(path)))
        {
            writer.Write(11);
            writer.Write(new byte[64]);
        }
        AssertCacheMiss(BlobCache.LoadBakedAnimationFromCache(clip, null));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void TransientClipsBypassPersistentCache()
    {
        var clip = new AnimationClip { name = Guid.NewGuid().ToString("N") };
        try
        {
            var path = BlobCache.ConstructCachedAnimationBlobPath(clip, null);
            cacheFiles.Add(path);
            using var blob = MakeBlob<AnimationClipBlob>();
            BlobCache.SaveBakedAnimationToCache(clip, null, blob);
            Assert.IsFalse(File.Exists(path));
            AssertCacheMiss(BlobCache.LoadBakedAnimationFromCache(clip, null));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
}
