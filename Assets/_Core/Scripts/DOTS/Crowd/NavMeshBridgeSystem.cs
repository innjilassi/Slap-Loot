using Unity.Entities;
using Unity.Transforms;
using UnityEngine.AI;

namespace SlapGame
{
    public partial class NavMeshBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var pathBufferLookup = GetBufferLookup<PedestrianPathPoint>();

            foreach (var (pedState, transform, pathBuffer, entity) in 
                     SystemAPI.Query<RefRW<PedestrianState>, 
                                     RefRO<LocalTransform>, 
                                     DynamicBuffer<PedestrianPathPoint>>()
                                     .WithEntityAccess())
            {
                // Only calculate if requested by the Wander System
                if (!pedState.ValueRO.NeedsNewPath) continue;

                var path = new NavMeshPath();
                
                // Call Unity's built-in NavMesh API
                if (NavMesh.CalculatePath(transform.ValueRO.Position, pedState.ValueRO.TargetPosition, NavMesh.AllAreas, path))
                {
                    pathBuffer.Clear();
                    
                    // Copy path corners to the ECS buffer (skip index 0, as that is the start position)
                    for (int i = 1; i < path.corners.Length; i++)
                    {
                        pathBuffer.Add(new PedestrianPathPoint { Value = path.corners[i] });
                    }
                }
                
                // Reset the flag so we don't calculate again next frame
                pedState.ValueRW.NeedsNewPath = false;
            }
        }
    }
}