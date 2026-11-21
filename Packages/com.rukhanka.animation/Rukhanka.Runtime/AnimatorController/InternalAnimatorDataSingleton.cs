
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
internal struct ControllerAnimationsCacheKey: IEquatable<ControllerAnimationsCacheKey>
{
    internal BlobAssetReference<ControllerAnimationsBlob> controller;
    internal BlobAssetReference<ControllerAnimationsBlob> overrides;

    public bool Equals(ControllerAnimationsCacheKey other) => controller.Equals(other.controller) && overrides.Equals(other.overrides);
    public override int GetHashCode() => unchecked(controller.GetHashCode() * 397 ^ overrides.GetHashCode());
}

//=================================================================================================================//

public struct InternalAnimatorDataSingleton: IComponentData
{
    internal NativeParallelHashMap<ControllerAnimationsCacheKey, BlobAssetReference<ControllerAnimationsBlob>> animatorOverrideAnimationsMap;
    // The native cache is shared by regular, predicted, and fill systems.
    internal JobHandle cacheDependency;

/////////////////////////////////////////////////////////////////////////////////

	public static InternalAnimatorDataSingleton MakeDefault()
	{
		var rv = new InternalAnimatorDataSingleton()
		{
			animatorOverrideAnimationsMap = new (0xff, Allocator.Persistent)
		};
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////

	public void Dispose()
	{
		cacheDependency.Complete();
		foreach (var kv in animatorOverrideAnimationsMap)
			kv.Value.Dispose();
		animatorOverrideAnimationsMap.Dispose();
	}
}
}
