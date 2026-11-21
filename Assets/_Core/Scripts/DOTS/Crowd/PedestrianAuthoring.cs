using Unity.Entities;
using UnityEngine;

namespace SlapGame
{
    public class PedestrianAuthoring : MonoBehaviour
    {
        public float WanderRadius = 10f;
        public float MaxHealth = 100f;

        [System.Serializable]
        public struct IdleAction
        {
            public string Name; 
            public int AnimationID; 
            public float MinTime;
            public float MaxTime;
            public bool LockMovement; 
        }

        [System.Serializable]
        public struct MoveAction
        {
            public string Name;
            public int AnimationID; 
            public float MoveSpeed;
            public float TurnSpeed;
        }

        [Header("Idle Actions")]
        public IdleAction[] IdleStates;
        
        [Header("Movement Actions")]
        public MoveAction[] MovementStates;
    }

    public class PedestrianBaker : Baker<PedestrianAuthoring>
    {
        public override void Bake(PedestrianAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PedestrianConfigBlob>();
            
            root.WanderRadius = authoring.WanderRadius;
            
            // Bake Idle States
            var idleArray = builder.Allocate(ref root.IdleStates, authoring.IdleStates.Length);
            for (int i = 0; i < idleArray.Length; i++)
            {
                idleArray[i] = new IdleStateData { 
                    AnimationID = authoring.IdleStates[i].AnimationID,
                    MinTime = authoring.IdleStates[i].MinTime, 
                    MaxTime = authoring.IdleStates[i].MaxTime,
                    LockMovement = authoring.IdleStates[i].LockMovement
                };
            }

            // Bake Movement States
            var moveArray = builder.Allocate(ref root.MovementStates, authoring.MovementStates.Length);
            for (int i = 0; i < moveArray.Length; i++)
            {
                moveArray[i] = new MovementStateData { 
                    AnimationID = authoring.MovementStates[i].AnimationID,
                    MoveSpeed = authoring.MovementStates[i].MoveSpeed, 
                    TurnSpeed = authoring.MovementStates[i].TurnSpeed 
                };
            }

            var blobRef = builder.CreateBlobAssetReference<PedestrianConfigBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();

            AddBlobAsset(ref blobRef, out _);
            AddComponent(entity, new PedestrianConfig { Value = blobRef });
            
            AddComponent(entity, new PedestrianState
            {
                Origin = authoring.transform.position,
                StateTimer = 0f,
                IsMoving = false,
                NeedsNewPath = false,
                CurrentAnimationID = 0,
                CurrentMoveSpeed = 0f,
                CurrentTurnSpeed = 120f
            });
            
            AddComponent(entity, new Health { Current = authoring.MaxHealth, Max = authoring.MaxHealth });
            AddBuffer<PedestrianPathPoint>(entity);
        }
    }
}