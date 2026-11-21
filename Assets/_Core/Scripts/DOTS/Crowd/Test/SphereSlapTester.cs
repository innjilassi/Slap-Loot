using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using System.Collections.Generic;

namespace SlapGame
{
    public class SphereSlapTester : MonoBehaviour
    {
        public float SlapRadius = 1.5f;
        public float SlapDamage = 100f;
        public float SlapForce = 25f;
        
        [Header("Directional Slap")]
        [Tooltip("How wide the slap cone is. 180 = full circle. 90 = only in front.")]
        [Range(0f, 180f)] 
        public float SlapAngle = 90f;

        [Header("Layers")]
        public LayerMask HittableLayers; 

        private HashSet<Entity> entitiesInside = new HashSet<Entity>();

        void Update()
        {
            DrawDebugSphere(transform.position, SlapRadius, Color.red);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            
            var entityManager = world.EntityManager;
            var physicsWorldQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
            if (physicsWorldQuery.IsEmpty) return; 
            
            var physicsWorld = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();
            
            var sphereCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = SlapRadius
            });

            uint collidesWithMask = (uint)HittableLayers.value;
            sphereCollider.Value.SetCollisionFilter(new CollisionFilter
            {
                BelongsTo = ~0u, 
                CollidesWith = collidesWithMask, 
                GroupIndex = 0
            });

            var input = new ColliderCastInput(sphereCollider, transform.position, transform.position);
            NativeList<ColliderCastHit> hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            
            physicsWorld.CollisionWorld.CastCollider(input, ref hits);

            HashSet<Entity> currentFrameHits = new HashSet<Entity>();

            foreach (var hit in hits)
            {
                Entity hitEntity = hit.Entity;

                if (entityManager.HasComponent<Health>(hitEntity))
                {
                    currentFrameHits.Add(hitEntity);

                    // --- THE DIRECTIONAL CHECK ---
                    float3 entityPos = entityManager.GetComponentData<LocalTransform>(hitEntity).Position;
                    float3 toTarget = math.normalize(entityPos - (float3)transform.position);
                    
                    // If the angle is less than 180, check if they are in front of us
                    if (SlapAngle < 180f)
                    {
                        float dot = math.dot(transform.forward, toTarget);
                        // Convert the angle to a dot product threshold (e.g., 90 degrees = 0 dot)
                        float angleThreshold = math.cos(math.radians(SlapAngle));
                        
                        if (dot < angleThreshold)
                        {
                            continue; // Skip this pedestrian, they are behind us!
                        }
                    }
                    // -----------------------------

                    if (!entitiesInside.Contains(hitEntity))
                    {
                        if (!entityManager.HasComponent<DamageRequest>(hitEntity))
                        {
                            float3 hitDir = math.normalize(entityPos - (float3)transform.position);

                            entityManager.AddComponentData(hitEntity, new DamageRequest
                            {
                                Amount = SlapDamage,
                                HitPosition = hit.Position,
                                HitDirection = hitDir,
                                Force = SlapForce
                            });
                            
                            Debug.DrawLine(transform.position, hit.Position, Color.green, 1f);
                        }
                    }
                }
            }

            entitiesInside = currentFrameHits;
            hits.Dispose();
            sphereCollider.Dispose();
        }

        private void DrawDebugSphere(Vector3 center, float radius, Color color)
        {
            int segments = 20;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 p1 = center + new Vector3(0, Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius);
                Vector3 p2 = center + new Vector3(0, Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius);
                Debug.DrawLine(p1, p2, color);

                Vector3 p3 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p4 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
                Debug.DrawLine(p3, p4, color);

                Vector3 p5 = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
                Vector3 p6 = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);
                Debug.DrawLine(p5, p6, color);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, SlapRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, SlapRadius);
        }
    }
}