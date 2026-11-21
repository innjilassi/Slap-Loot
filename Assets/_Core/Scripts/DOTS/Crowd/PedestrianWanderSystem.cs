using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace SlapGame
{
    [BurstCompile]
    public partial struct PedestrianWanderSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            // Creates a pseudo-random number generator based on time
            var rng = new Random((uint)(SystemAPI.Time.ElapsedTime * 1000) + 1);

            foreach (var (pedState, config, pathBuffer) in
                     SystemAPI.Query<RefRW<PedestrianState>, 
                                     RefRO<PedestrianConfig>, 
                                     DynamicBuffer<PedestrianPathPoint>>().WithNone<RagdollTag>())
            {
                ref var cfg = ref config.ValueRO.Value.Value;

                if (pedState.ValueRO.IsMoving)
                {
                    // Wait for NavMesh to give us a path
                    if (pedState.ValueRO.NeedsNewPath) continue; 
                    
                    // If path is empty, we reached our destination
                    if (pathBuffer.Length == 0)
                    {
                        pedState.ValueRW.IsMoving = false;
                        PickNewIdleAction(ref pedState.ValueRW, ref cfg, ref rng);
                    }
                }
                else
                {
                    // Count down the idle timer
                    pedState.ValueRW.StateTimer -= dt;

                    if (pedState.ValueRO.StateTimer <= 0f)
                    {
                        // Time to move! Pick a random point
                        float3 randomDir = rng.NextFloat3Direction();
                        randomDir.y = 0;
                        randomDir = math.normalizesafe(randomDir);
                        
                        pedState.ValueRW.TargetPosition = pedState.ValueRO.Origin + (randomDir * rng.NextFloat(1f, cfg.WanderRadius));
                        
                        // Pick a random walking style (Walk, Run, Walk-Talk)
                        PickNewMoveAction(ref pedState.ValueRW, ref cfg, ref rng);
                        
                        pedState.ValueRW.IsMoving = true;
                        pedState.ValueRW.NeedsNewPath = true;
                    }
                }
            }
        }

        private void PickNewIdleAction(ref PedestrianState pedState, ref PedestrianConfigBlob cfg, ref Random rng)
        {
            if (cfg.IdleStates.Length == 0) return;
            
            int idleIndex = rng.NextInt(0, cfg.IdleStates.Length);
            var action = cfg.IdleStates[idleIndex];

            pedState.CurrentAnimationID = action.AnimationID;
            pedState.StateTimer = rng.NextFloat(action.MinTime, action.MaxTime);
        }

        private void PickNewMoveAction(ref PedestrianState pedState, ref PedestrianConfigBlob cfg, ref Random rng)
        {
            if (cfg.MovementStates.Length == 0) return;

            int moveIndex = rng.NextInt(0, cfg.MovementStates.Length);
            var action = cfg.MovementStates[moveIndex];

            pedState.CurrentAnimationID = action.AnimationID;
            pedState.CurrentMoveSpeed = action.MoveSpeed;
            pedState.CurrentTurnSpeed = action.TurnSpeed;
        }
    }
}