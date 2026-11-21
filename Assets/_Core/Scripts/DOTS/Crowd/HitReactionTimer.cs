using Unity.Burst;
using Unity.Entities;
using Unity.Collections;

namespace SlapGame
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct HitReactionCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. Tick timers and destroy target entities
            foreach (var (timer, entity) in SystemAPI.Query<RefRW<HitReactionTimer>>().WithEntityAccess())
            {
                timer.ValueRW.TimeLeft -= dt;
                if (timer.ValueRO.TimeLeft <= 0f)
                {
                    ecb.DestroyEntity(entity); // Destroy the virtual target
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}