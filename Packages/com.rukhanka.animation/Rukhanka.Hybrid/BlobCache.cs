#if UNITY_EDITOR

using System;
using System.IO;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
public class BlobCache
{
    sealed class CacheReader : Unity.Entities.Serialization.BinaryReader
    {
        readonly FileStream stream;

        public CacheReader(string path)
        {
            stream = File.OpenRead(path);
        }

        public long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public unsafe void ReadBytes(void* data, int bytes)
        {
            var remaining = new Span<byte>(data, bytes);
            
            while (!remaining.IsEmpty)
            {
                var count = stream.Read(remaining);
                if (count == 0)
                    throw new EndOfStreamException();
                remaining = remaining.Slice(count);
            }
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }

//------------------------------------------------------------------------------------------------------------------//

    sealed class CacheWriter : Unity.Entities.Serialization.BinaryWriter
    {
        readonly FileStream stream;

        public CacheWriter(string path)
        {
            stream = File.Create(path);
        }

        public long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public unsafe void WriteBytes(void* data, int bytes)
        {
            stream.Write(new ReadOnlySpan<byte>(data, bytes));
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }

//------------------------------------------------------------------------------------------------------------------//

    //  Need to have two separate versions because blobs are different depending on RUKHANKA_DEBUG_INFO
#if RUKHANKA_DEBUG_INFO
    static readonly int BLOB_VERSION = 12;
#else
    static readonly int BLOB_VERSION = 13;
#endif
    public static string GetBlobCacheDirPath() => $"{Environment.CurrentDirectory.Replace('\\', '/')}/Library/Rukhanka.Animation";
    public static string GetAnimationBlobCacheDirPath() => GetBlobCacheDirPath() + "/AnimationCache";
    public static string GetControllerBlobCacheDirPath() => GetBlobCacheDirPath() + "/ControllerCache";
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static string ConstructCachedAnimationBlobPath(AnimationClip animationClip, Avatar avatar)
    {
        var animationHash = BakingUtils.ComputeAnimationHash(animationClip, avatar);
        var avatarName = avatar != null ? avatar.name : "NO_AVATAR";
        var cacheFileName = $"{animationClip.name}_{avatarName}_{animationHash}.blob";
        cacheFileName = string.Join("_", cacheFileName.Split(Path.GetInvalidFileNameChars()));
        var cacheFilePath = $"{GetAnimationBlobCacheDirPath()}/{cacheFileName}";
        return cacheFilePath;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static BlobAssetReference<AnimationClipBlob> LoadBakedAnimationFromCache(AnimationClip animationClip, Avatar avatar)
	{
    #if RUKHANKA_NO_BLOB_CACHE
        var dummy = BLOB_VERSION;
        return default;
    #else
        if (!TryGetDependencyHash(animationClip, avatar, out var dependencyHash))
            return default;
		var cachedBlobPath = ConstructCachedAnimationBlobPath(animationClip, avatar);
        return LoadCachedBlob<AnimationClipBlob>(cachedBlobPath, dependencyHash);
    #endif
	}
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static void SaveBakedAnimationToCache(AnimationClip animationClip, Avatar avatar, BlobAssetReference<AnimationClipBlob> bar)
	{
    #if !RUKHANKA_NO_BLOB_CACHE
        if (!TryGetDependencyHash(animationClip, avatar, out var dependencyHash))
            return;
		var cachedBlobPath = ConstructCachedAnimationBlobPath(animationClip, avatar);
        Directory.CreateDirectory(GetAnimationBlobCacheDirPath());
        SaveCachedBlob(cachedBlobPath, dependencyHash, bar);
    #endif
	}
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static string ConstructCachedControllerBlobPath(AnimatorController controller)
    {
        var controllerHash = BakingUtils.ComputeControllerHash(controller);
        var cacheFilePath = $"{GetControllerBlobCacheDirPath()}/{controller.name}_{controllerHash}.blob";
        return cacheFilePath;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static BlobAssetReference<ControllerBlob> LoadBakedControllerFromCache(AnimatorController controller)
	{
    #if RUKHANKA_NO_BLOB_CACHE
        return default;
    #else
        if (!TryGetDependencyHash(controller, null, out var dependencyHash))
            return default;
		var cachedBlobPath = ConstructCachedControllerBlobPath(controller);
        return LoadCachedBlob<ControllerBlob>(cachedBlobPath, dependencyHash);
    #endif
	}
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static void SaveBakedControllerToCache(AnimatorController controller, BlobAssetReference<ControllerBlob> bar)
	{
    #if !RUKHANKA_NO_BLOB_CACHE
        if (!TryGetDependencyHash(controller, null, out var dependencyHash))
            return;
		var cachedBlobPath = ConstructCachedControllerBlobPath(controller);
        Directory.CreateDirectory(GetControllerBlobCacheDirPath());
        SaveCachedBlob(cachedBlobPath, dependencyHash, bar);
    #endif
	}
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // Validate only requested entries. Dependency hashes include imported sub-assets,
    // importer settings and referenced assets, without loading objects during imports.
    static bool TryGetDependencyHash(UnityEngine.Object source, UnityEngine.Object additionalSource, out UnityEngine.Hash128 hash)
    {
        hash = default;
        var sourcePath = AssetDatabase.GetAssetPath(source);
        
        // Transient objects have no persistent dependency identity.
        if (string.IsNullOrEmpty(sourcePath))
            return false;
        
        var sourcePaths = new[] { sourcePath };
        if (additionalSource != null)
        {
            var additionalPath = AssetDatabase.GetAssetPath(additionalSource);
            if (string.IsNullOrEmpty(additionalPath))
                return false;
            sourcePaths = new[] { sourcePath, additionalPath };
        }
        
        // A controller's imported hash alone does not reliably change when the
        // contents of a referenced clip change. Include the referenced files too.
        var dependencies = AssetDatabase.GetDependencies(sourcePaths, true);
        Array.Sort(dependencies, StringComparer.Ordinal);
        
        foreach (var dependency in dependencies)
            hash.Append(AssetDatabase.GetAssetDependencyHash(dependency).ToString());
        return true;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    static unsafe BlobAssetReference<T> LoadCachedBlob<T>(string path, UnityEngine.Hash128 dependencyHash) where T: unmanaged
    {
        if (!File.Exists(path) || new FileInfo(path).Length < sizeof(int) + sizeof(UnityEngine.Hash128))
            return default;
        
        using var reader = new CacheReader(path);
        if (reader.ReadInt() != BLOB_VERSION)
            return default;
        
        UnityEngine.Hash128 storedHash;
        reader.ReadBytes(&storedHash, sizeof(UnityEngine.Hash128));
        
        if (storedHash != dependencyHash)
            return default;
        
        return reader.Read<T>();
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    static unsafe void SaveCachedBlob<T>(string path, UnityEngine.Hash128 dependencyHash, BlobAssetReference<T> blob) where T: unmanaged
    {
        using var writer = new CacheWriter(path);
        writer.Write(BLOB_VERSION);
        writer.WriteBytes(&dependencyHash, sizeof(UnityEngine.Hash128));
        writer.Write(blob);
    }
}
}

#endif

