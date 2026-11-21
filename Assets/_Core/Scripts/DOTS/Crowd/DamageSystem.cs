using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;      // <--- ADD THIS
using Rukhanka.Hybrid;       // <--- ADD THIS (For OverrideTransformIKComponent)
using Unity.Collections;

namespace SlapGame
{
    [BurstCompile]
    public partial struct DamageSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (health, damage, pedState, entity) in 
                     SystemAPI.Query<RefRW<Health>, RefRO<DamageRequest>, RefRW<PedestrianState>>().WithEntityAccess())
            {
                health.ValueRW.Current -= damage.ValueRO.Amount;

                if (health.ValueRO.Current <= 0)
                {
                    // FATAL HIT: Ragdoll
                    ecb.AddComponent<RagdollTag>(entity);
                    ecb.AddComponent(entity, new RagdollTimer { TimeLeft = 5f });
                    pedState.ValueRW.CurrentAnimationID = -1;
                    pedState.ValueRW.IsMoving = false;
                    pedState.ValueRW.NeedsNewPath = false;
                }
                else
                {
                    // // SURVIVED HIT: Add Hit Reaction
                    
                    // // 1. Create a virtual target entity at the hit location
                    // var hitTarget = ecb.CreateEntity();
                    // ecb.SetComponent(hitTarget, LocalTransform.FromPosition(damage.ValueRO.HitPosition));
                    // ecb.AddComponent(hitTarget, new HitReactionTimer { TimeLeft = 0.5f }); 

                    // // 2. Find the Chest Bone and add the IK Component to it
                    // if (state.EntityManager.HasBuffer<LinkedEntityGroup>(entity))
                    // {
                    //     var group = state.EntityManager.GetBuffer<LinkedEntityGroup>(entity);
                    //     foreach (var linked in group)
                    //     {
                    //         if (state.EntityManager.HasComponent<HitReactionBoneTag>(linked.Value))
                    //         {
                    //             // Add Rukhanka's built-in IK component to the Chest
                    //             ecb.AddComponent(linked.Value, new OverrideTransformIKComponent
                    //             {
                    //                 target = hitTarget,
                    //                 positionWeight = 0f, // Don't move the bone
                    //                 rotationWeight = 1f  // Fully rotate towards the hit point
                    //             });
                    //             break;
                    //         }
                    //     }
                    // }
                }

                ecb.RemoveComponent<DamageRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}