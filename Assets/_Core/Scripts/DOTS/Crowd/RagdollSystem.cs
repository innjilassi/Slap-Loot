using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace SlapGame
{
    [BurstCompile]
    public partial struct RagdollSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (ragdollTag, damage, entity) in 
                     SystemAPI.Query<RefRO<RagdollTag>, RefRO<DamageRequest>>().WithEntityAccess())
            {
                // 1. Wake up physics on the bones and apply the slap force
                ApplyPhysicsRecursive(ref state, ecb, entity, damage.ValueRO);

                // 2. Remove tags
                ecb.RemoveComponent<RagdollTag>(entity);
                ecb.RemoveComponent<DamageRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ApplyPhysicsRecursive(ref SystemState state, EntityCommandBuffer ecb, Entity currentEntity, DamageRequest damage)
        {
            if (state.EntityManager.HasComponent<PhysicsVelocity>(currentEntity) && 
                state.EntityManager.HasComponent<PhysicsMass>(currentEntity))
            {
                var mass = state.EntityManager.GetComponentData<PhysicsMass>(currentEntity);
                
                // Force dynamic (if it was kinematic)
                if (mass.InverseMass == 0f)
                {
                    mass.InverseMass = 1f; 
                    ecb.SetComponent(currentEntity, mass);
                }

                var velocity = state.EntityManager.GetComponentData<PhysicsVelocity>(currentEntity);
                var boneTransform = state.EntityManager.GetComponentData<LocalTransform>(currentEntity);
                
                float3 forceDir = math.normalize(boneTransform.Position - damage.HitPosition);
                velocity.Linear += forceDir * damage.Force;
                velocity.Angular += damage.HitDirection * damage.Force * 0.5f;
                
                ecb.SetComponent(currentEntity, velocity);
            }

            if (state.EntityManager.HasBuffer<Child>(currentEntity))
            {
                var childBuffer = state.EntityManager.GetBuffer<Child>(currentEntity);
                foreach (var child in childBuffer)
                    ApplyPhysicsRecursive(ref state, ecb, child.Value, damage);
            }
        }
    }
}