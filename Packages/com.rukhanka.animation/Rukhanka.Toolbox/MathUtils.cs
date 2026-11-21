
using System;
using Unity.Collections;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Toolbox
{
public static class MathUtils
{
	public static quaternion ShortestRotation(quaternion from, quaternion to)
	{
		uint4 sign = math.asuint(math.dot(from, to)) & 0x80000000;
		var rv = math.asfloat(sign ^ math.asuint(to.value));
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static quaternion FromToRotation(float3 from, float3 to)
	{
		var fromNormalized = math.normalizesafe(from);
		var toNormalized = math.normalizesafe(to);
		if (math.lengthsq(fromNormalized) < 0.5f || math.lengthsq(toNormalized) < 0.5f)
			return quaternion.identity;

		return FromToRotationForNormalizedVectors(fromNormalized, toNormalized);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static quaternion FromToRotationForNormalizedVectors(float3 from, float3 to)
	{
		var cosine = math.clamp(math.dot(from, to), -1, 1);
		var axis = math.cross(from, to);
		// Cancellation near opposite directions can leave a component along from.
		axis -= from * math.dot(axis, from);
		var sine = math.length(axis);
		// Avoid cancellation in 1 + dot near opposite directions.
		if (sine > math.EPSILON)
			return quaternion.AxisAngle(axis / sine, math.atan2(sine, cosine));

		if (cosine < 0)
			return quaternion.AxisAngle(math.normalize(MakePerpendicularVector(from)), math.PI);

		return quaternion.identity;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	//	Shuffle array according to given indices
	public static void ShuffleArray<T>(Span<T> arr, in NativeArray<int> shuffleIndices) where T: unmanaged
	{
		if (arr.Length < 2) return;
		if (arr.Length != shuffleIndices.Length) return;
	
		Span<T> scatterArr = stackalloc T[arr.Length];
		for (int i = 0; i < arr.Length; ++i)
		{
			var shuffleIndex = shuffleIndices[i];
			var v = arr[shuffleIndex];
			scatterArr[i] = v;
		}

		for (int i = 0; i < arr.Length; ++i)
		{
			arr[i] = scatterArr[i];
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	
	//	Result is not normalized
	public static float3 MakePerpendicularVector(float3 v)
	{
		var av = math.abs(v);
		
		if (av.z < math.min(av.x, av.y))
			return new float3(v.y, -v.x, 0);
			
		if (av.y < av.x)
			return new float3(-v.z, 0, v.x);
			
		return new float3(0, v.z, -v.y);
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static bool PlaneLineIntersection(float3 linePoint, float3 lineVector, float4 plane, ref float3 intersectionPoint)
	{
		var fv = math.dot(lineVector, plane.xyz);
		if (math.abs(fv) > math.EPSILON)
		{
			intersectionPoint = linePoint - lineVector * math.dot(plane, new float4(linePoint, 1)) * math.rcp(fv);
			return true;
		}
		return false;
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static float4 BuildPlaneFromNormalAndPoint(float3 n, float3 p)
	{
		var d = -math.dot(n, p);
		var pln = new float4(n, d);
		return pln;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static float4 BuildPlaneFromThreePoints(float3 p0, float3 p1, float3 p2)
	{
		var dp0 = p1 - p0;
		var dp1 = p2 - p0;
		
		var n = math.cross(dp0, dp1);
		n = math.normalizesafe(n);
		return BuildPlaneFromNormalAndPoint(n, p0);
	}
}
}
