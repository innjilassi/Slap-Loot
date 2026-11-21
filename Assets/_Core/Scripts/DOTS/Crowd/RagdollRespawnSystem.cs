using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace SlapGame
{
    [BurstCompile]
    public partial struct RagdollRespawnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (timer, pedState, entity) in 
                     SystemAPI.Query<RefRW<RagdollTimer>, RefRW<PedestrianState>>().WithEntityAccess())
            {
                timer.ValueRW.TimeLeft -= dt;

                if (timer.ValueRO.TimeLeft <= 0f)
                {
                    // 1. Get the raw ragdoll position
                    float3 finalPos = GetFinalRagdollPosition(ref state, entity);
                    
                    // 2. Get the original root position for the Y axis
                    float3 rootPos = state.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                    
                    // 3. FLATTEN THE Y AXIS
                    // Use the X and Z from where they fell, but the Y from the original root.
                    finalPos.y = rootPos.y; 

                    // Debug.Log($"[RespawnSystem] Entity {entity.Index} respawning at {finalPos}");

                    // 4. Move the ROOT entity
                    if (state.EntityManager.HasComponent<LocalTransform>(entity))
                    {
                        var rootTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);
                        rootTransform.Position = finalPos; 
                        ecb.SetComponent(entity, rootTransform);
                    }

                    // 5. Reset Health
                    if (state.EntityManager.HasComponent<Health>(entity))
                    {
                        var health = state.EntityManager.GetComponentData<Health>(entity);
                        health.Current = health.Max;
                        ecb.SetComponent(entity, health);
                    }

                    // 6. Reset State
                    pedState.ValueRW.IsMoving = false;
                    pedState.ValueRW.NeedsNewPath = false;
                    pedState.ValueRW.StateTimer = 2f; 
                    pedState.ValueRW.CurrentAnimationID = 0; // Idle

                    // 7. Reset Physics
                    ResetPhysicsRecursive(ref state, ecb, entity);

                    // 8. Remove tags
                    ecb.RemoveComponent<RagdollTag>(entity);
                    ecb.RemoveComponent<RagdollTimer>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float3 GetFinalRagdollPosition(ref SystemState state, Entity rootEntity)
        {
            float3 fallbackPos = state.EntityManager.GetComponentData<LocalTransform>(rootEntity).Position;

            // Check LinkedEntityGroup first as it's more reliable for Ragdolls
            if (state.EntityManager.HasBuffer<LinkedEntityGroup>(rootEntity))
            {
                var group = state.EntityManager.GetBuffer<LinkedEntityGroup>(rootEntity);
                foreach (var linked in group)
                {
                    if (linked.Value != rootEntity && state.EntityManager.HasComponent<PhysicsVelocity>(linked.Value))
                    {
                        return state.EntityManager.GetComponentData<LocalTransform>(linked.Value).Position;
                    }
                }
            }

            // Fallback to Child buffer
            if (state.EntityManager.HasBuffer<Child>(rootEntity))
            {
                var children = state.EntityManager.GetBuffer<Child>(rootEntity);
                foreach (var child in children)
                {
                    if (state.EntityManager.HasComponent<PhysicsVelocity>(child.Value))
                    {
                        return state.EntityManager.GetComponentData<LocalTransform>(child.Value).Position;
                    }
                }
            }

            return fallbackPos;
        }

        private void ResetPhysicsRecursive(ref SystemState state, EntityCommandBuffer ecb, Entity currentEntity)
        {
            if (state.EntityManager.HasComponent<PhysicsVelocity>(currentEntity) && 
                state.EntityManager.HasComponent<PhysicsMass>(currentEntity))
            {
                var mass = state.EntityManager.GetComponentData<PhysicsMass>(currentEntity);
                mass.InverseMass = 0f; // Set to Kinematic
                ecb.SetComponent(currentEntity, mass);
                
                ecb.SetComponent(currentEntity, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            }

            if (state.EntityManager.HasBuffer<Child>(currentEntity))
            {
                var childBuffer = state.EntityManager.GetBuffer<Child>(currentEntity);
                foreach (var child in childBuffer)
                    ResetPhysicsRecursive(ref state, ecb, child.Value);
            }
        }
    }
}