using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using static Rukhanka.AnimatorControllerSystemJobs;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{ 
public class AnimatorControllerTest
{
	[Test]
	public void NestedBlendTreeBlobTest()
	{
		var builder = new Unity.Entities.BlobBuilder(Unity.Collections.Allocator.Temp);
		try
		{
			ref var root = ref builder.ConstructRoot<MotionBlob>();
			root.type = MotionBlob.Type.BlendTree1D;
			var children = ChildMotionBlobArray.Allocate(ref builder, ref root.blendTree.motions, 2);
			children[0].motion.type = MotionBlob.Type.AnimationClip;
			children[0].motion.animationIndex = 7;
			children[1].motion.type = MotionBlob.Type.BlendTreeDirect;
			var nested = ChildMotionBlobArray.Allocate(ref builder, ref children[1].motion.blendTree.motions, 1);
			nested[0].motion.type = MotionBlob.Type.AnimationClip;
			nested[0].motion.animationIndex = 11;
			using var blob = builder.CreateBlobAssetReference<MotionBlob>(Unity.Collections.Allocator.Persistent);
			Assert.AreEqual(2, blob.Value.blendTree.motions.Length);
			Assert.AreEqual(7, blob.Value.blendTree.motions[0].motion.animationIndex);
			Assert.AreEqual(1, blob.Value.blendTree.motions[1].motion.blendTree.motions.Length);
			Assert.AreEqual(11, blob.Value.blendTree.motions[1].motion.blendTree.motions[0].motion.animationIndex);
		}
		finally
		{
			builder.Dispose();
		}
	}

/////////////////////////////////////////////////////////////////////////////////

	[Test]
	public void LoopAwareTransitionTimeTest()
	{
		var fn = TestToolbox.GetStaticPrivateMethod<StateMachineProcessJob>("GetLoopAwareTransitionExitTime");

		var testData = new List<float2>();
		testData.Add(new float2(0, 0));
		testData.Add(new float2(1, 0));
		testData.Add(new float2(0, 1));
		testData.Add(new float2(1, 1));
		testData.Add(new float2(0, 2));
		testData.Add(new float2(1, 2));
		testData.Add(new float2(0, 3));
		testData.Add(new float2(1, 3));
		testData.Add(new float2(0, 3.5f));
		testData.Add(new float2(1, 3.5f));
		testData.Add(new float2(0, 6.99f));
		testData.Add(new float2(1, 6.99f));
		testData.Add(new float2(0, 7.01f));
		testData.Add(new float2(1, 7.01f));

		UnityEngine.Random.InitState(DateTime.Now.Millisecond);

		for (var i = 0; i < 100; ++i)
		{
			var exitTime = UnityEngine.Random.value;
			var normalizedStateTime = (UnityEngine.Random.value * 2 - 1) * 20;
			testData.Add(new float2(exitTime, normalizedStateTime));
		}


		//	Test for exit time [0..1]
		foreach (var td in testData)
		{
			var exitTime = td.x;
			var normalizedStateTime = td.y;
			var t = StateMachineProcessJob.GetLoopAwareTransitionExitTime(exitTime, normalizedStateTime, math.sign(normalizedStateTime));
			var dt = t - normalizedStateTime;

			//Debug.Log($"ET: {exitTime}, NT: {normalizedStateTime}, Transition Time: {t}");

			if (math.sign(normalizedStateTime) > 0)
			{
				Assert.IsTrue(dt >= 0);
				Assert.IsTrue(dt <= 1);
			}
			else
			{
				Assert.IsTrue(dt <= 0);
				Assert.IsTrue(dt >= -1);
			}
		}

		//	Test for exit time >= 1
		for (var i = 0; i < 20; ++i)
		{
			var exitTime = UnityEngine.Random.value * 10 + 1;
			var normalizedStateTime = (UnityEngine.Random.value * 2 - 1) * 20;

			var sgn = math.sign(normalizedStateTime);
			var t = StateMachineProcessJob.GetLoopAwareTransitionExitTime(exitTime, normalizedStateTime, sgn);

			Assert.IsTrue(t == exitTime * sgn);
		}
	}
}
}
