using Unity.Entities;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Toolbox
{
public static class EntityTools
{
	public static bool TryGetChunkComponentData<T>(EntityStorageInfoLookup storageInfoLookup, Entity entity, ref ComponentTypeHandle<T> typeHandle, out T outChunkData) where T : unmanaged, IComponentData
    {
	    outChunkData = default;
		if (!storageInfoLookup.Exists(entity)) return false;
		var otherChunk = storageInfoLookup[entity].Chunk;
		if (!otherChunk.HasChunkComponent<T>()) {
			return false;
		}
		outChunkData = otherChunk.GetChunkComponentData(ref typeHandle);
		return true;
	}

}
}
