using Unity.Entities;
using Rukhanka;
using UnityEngine;

namespace SlapGame
{
    [UpdateBefore(typeof(RukhankaAnimationSystemGroup))] 
    public partial class PedestrianAnimationBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Cache the hash for our specific parameter name once
            var paramHash = Animator.StringToHash("ActionID");

            foreach (var (pedState, parameters, indexTable) in
                     SystemAPI.Query<RefRO<PedestrianState>, 
                                     DynamicBuffer<AnimatorControllerParameterComponent>,
                                     RefRO<AnimatorControllerParameterIndexTableComponent>>())
            {
                // Create the helper aspect
                var animator = new AnimatorParametersAspect(parameters, indexTable.ValueRO);
                
                // Set the integer parameter by name hash
                // Rukhanka's SetIntParameter handles writing the value back correctly
                animator.SetIntParameter(new FastAnimatorParameter("ActionID"), pedState.ValueRO.CurrentAnimationID);
            }
        }
    }
}