using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SlapGame
{
    // --- CONFIG BLOB ---
    public struct PedestrianConfig : IComponentData
    {
        public BlobAssetReference<PedestrianConfigBlob> Value;
    }

    public struct PedestrianConfigBlob
    {
        public float WanderRadius;
        public BlobArray<IdleStateData> IdleStates;
        public BlobArray<MovementStateData> MovementStates;
    }

    public struct IdleStateData
    {
        public int AnimationID; 
        public float MinTime;
        public float MaxTime;
        public bool LockMovement; 
    }

    public struct MovementStateData
    {
        public int AnimationID;
        public float MoveSpeed;
        public float TurnSpeed;
    }

    // --- RUNTIME STATE ---
    public struct PedestrianState : IComponentData
    {
        public float3 Origin;
        public float3 TargetPosition;
        public float StateTimer;
        public bool IsMoving;
        public bool NeedsNewPath;
        
        // Current Animation & Movement Data
        public int CurrentAnimationID; 
        public float CurrentMoveSpeed;
        public float CurrentTurnSpeed;
    }

    public struct PedestrianPathPoint : IBufferElementData
    {
        public float3 Value;
    }

    // Stores current health
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;
    }

    // Component added by the player when they slap someone
    public struct DamageRequest : IComponentData
    {
        public float Amount;
        public float3 HitPosition;
        public float3 HitDirection;
        public float Force;
    }
    
    // Tag to mark that this entity is now a ragdoll
    public struct RagdollTag : IComponentData { }

    public struct RagdollTimer : IComponentData
    {
        public float TimeLeft;
    }
    public struct RespawnRequest : IComponentData { }

    // Timer to clean up the IK after 0.5 seconds
    public struct HitReactionTimer : IComponentData
    {
        public float TimeLeft;
    }

}
