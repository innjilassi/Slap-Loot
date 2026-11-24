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
        [Header("Box Slap Shape")]
        [Tooltip("Full size of the box in local space (X = width/left-right, Y = height, Z = depth/forward-back).")]
        public Vector3 SlapBoxSize = new Vector3(2f, 2f, 1.5f);

        [Tooltip("Offset from the slapper's position to the center of the box (local space). " +
                 "Push Z positive to extend the box forward.")]
        public Vector3 SlapBoxOffset = new Vector3(0f, 0f, 0.75f);

        [Tooltip("Rotation offset (Euler) applied to the box relative to the slapper's forward.")]
        public Vector3 SlapBoxRotation = Vector3.zero;

        public float SlapDamage = 100f;
        public float SlapForce = 25f;

        [SerializeField] private SlapSide slapSide;
        [SerializeField] private SlapperController slapperController;

        [Header("Directional Slap (optional cone filter)")]
        [Tooltip("How wide the slap cone is. 180 = disabled (full box). 90 = only in front.")]
        [Range(0f, 180f)]
        public float SlapAngle = 180f;

        [Header("Layers")]
        public LayerMask HittableLayers;

        private HashSet<Entity> entitiesInside = new HashSet<Entity>();

        void Update()
        {
            if (!slapperController.GetIsSlapping()) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var entityManager = world.EntityManager;
            var physicsWorldQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
            if (physicsWorldQuery.IsEmpty) return;

            var physicsWorld = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();

            // --- Build the Box Collider ---
            var boxGeometry = new BoxGeometry
            {
                Center = float3.zero,
                Size = (float3)SlapBoxSize,
                Orientation = quaternion.identity,
                BevelRadius = 0f
            };
            var boxCollider = Unity.Physics.BoxCollider.Create(boxGeometry);

            uint collidesWithMask = (uint)HittableLayers.value;
            boxCollider.Value.SetCollisionFilter(new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = collidesWithMask,
                GroupIndex = 0
            });

            // --- Build the cast input via the BlobAssetReference overload (no unsafe needed) ---
            var input = new ColliderCastInput(
                boxCollider,
                GetBoxCenter(),
                GetBoxCenter(),
                GetBoxRotation()
            );

            NativeList<ColliderCastHit> hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            physicsWorld.CollisionWorld.CastCollider(input, ref hits);

            HashSet<Entity> currentFrameHits = new HashSet<Entity>();

            foreach (var hit in hits)
            {
                Entity hitEntity = hit.Entity;

                if (entityManager.HasComponent<Health>(hitEntity))
                {
                    currentFrameHits.Add(hitEntity);

                    // --- OPTIONAL DIRECTIONAL CHECK ---
                    float3 entityPos = entityManager.GetComponentData<LocalTransform>(hitEntity).Position;
                    float3 toTarget = math.normalize(entityPos - (float3)transform.position);

                    if (SlapAngle < 180f)
                    {
                        float dot = math.dot(transform.forward, toTarget);
                        float angleThreshold = math.cos(math.radians(SlapAngle));

                        if (dot < angleThreshold)
                        {
                            continue; // Behind us, ignore
                        }
                    }
                    // ---------------------------------

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

                            slapperController.Slap(slapSide);

                            Debug.DrawLine(transform.position, hit.Position, Color.green, 1f);
                        }
                    }
                }
            }

            entitiesInside = currentFrameHits;
            hits.Dispose();
            boxCollider.Dispose();
        }

        // -------- Helpers --------

        private Vector3 GetBoxCenter()
        {
            return transform.position + transform.TransformDirection(SlapBoxOffset);
        }

        private quaternion GetBoxRotation()
        {
            Quaternion rot = transform.rotation * Quaternion.Euler(SlapBoxRotation);
            return rot;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(GetBoxCenter(), GetBoxRotation(), SlapBoxSize);

            // Filled translucent box
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            // Wireframe box
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            // Reset matrix before drawing anything world-space
            Gizmos.matrix = Matrix4x4.identity;

            // Small forward indicator to show "front" of the box
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 1f);
        }
    }

    public enum SlapSide : byte
    {
        Left,
        Right
    }
}