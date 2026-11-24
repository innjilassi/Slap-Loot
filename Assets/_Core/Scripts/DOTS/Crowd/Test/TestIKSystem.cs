// using Unity.Entities;
// using UnityEngine;
// using Rukhanka;

// [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
// [UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
// [RequireMatchingQueriesForUpdate]
// public partial class TestIKSystem : SystemBase
// {
//    protected override void OnUpdate()
//    {
//     	var sampleConf = TestIKConfig.Instance;
// 		if (sampleConf == null)
// 		return;

//         foreach (var ikc in SystemAPI.Query<RefRW<OverrideTransformIKComponent>>())
// 		{
// 			ikc.ValueRW.positionWeight = sampleConf.overrideIKPosWeightSlider.value;
// 			ikc.ValueRW.rotationWeight = sampleConf.overrideIKRotWeightSlider.value;
// 		}	
//    }
// }
