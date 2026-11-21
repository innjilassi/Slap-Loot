using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SlapGame
{
    [BurstCompile]
    public partial struct PedestrianMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, pedState, pathBuffer) in
                     SystemAPI.Query<RefRW<LocalTransform>, 
                                     RefRO<PedestrianState>, 
                                     DynamicBuffer<PedestrianPathPoint>>().WithNone<RagdollTag>())
            {
                if (pathBuffer.Length == 0 || !pedState.ValueRO.IsMoving) continue;

                float3 currentPos = transform.ValueRO.Position;
                float3 targetPoint = pathBuffer[0].Value;
                
                // Ignore Y axis so they don't fly
                targetPoint.y = currentPos.y; 

                float3 direction = targetPoint - currentPos;
                float distance = math.length(direction);

                // Reached the current path corner
                if (distance < 0.5f)
                {
                    pathBuffer.RemoveAt(0);
                    continue;
                }

                // Move forward using the speed from the current MoveAction
                direction = math.normalize(direction);
                transform.ValueRW.Position += direction * pedState.ValueRO.CurrentMoveSpeed * dt;

                // Rotate towards the movement direction
                quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());
                
                // Turn Speed is in degrees, convert to a slerp factor
                float slerpFactor = math.saturate((pedState.ValueRO.CurrentTurnSpeed * dt) / 90f); 
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation, 
                    targetRotation, 
                    slerpFactor);
            }
        }
    }
}