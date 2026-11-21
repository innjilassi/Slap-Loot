using Unity.Entities;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public struct LegAnimatorLegInMotion: IBufferElementData
{
    public float3 startAnimationPoint;
    public float3 endAnimationPoint;
    public float moveFactor;
    public int legIndex;
}

/////////////////////////////////////////////////////////////////////////////////

public struct LegAnimatorLeg: IBufferElementData
{
    public Entity twoBoneIKEntity;
    // Rest position in body space; planted position in world space.
    public float3 refPosition;
    public float3 plantedPosition;
    public float stepThreshold;
    public int groupId;
    public bool initialized;
}

/////////////////////////////////////////////////////////////////////////////////

public struct LegAnimatorComponent: IComponentData, IEnableableComponent
{
    public int legStartIndex;
    public float stepTime;
    public float stepHeight;
}
}
