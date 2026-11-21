using NUnit.Framework;
using System;
using System.Reflection;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class DeformationLODSelectionTests
{
    [TestCase(1, 0, 10, 5, 1, true)]
    [TestCase(2, 10, 30, 5, 1, false)]
    [TestCase(1, 0, 10, 5, 2, false)]
    [TestCase(2, 10, 30, 0, 2, true)]
    [TestCase(2, 10, 30, 5, 2, true)]
    [TestCase(2, 10, 30, 30, 2, false)]
    [TestCase(4, 30, 60, 5, 2, false)]
    [TestCase(4, 30, 60, 30, 2, true)]
    [TestCase(4, 30, 60, 0, 4, true)]
    [TestCase(3, 0, 30, 5, 2, true)]
    public void MaximumLODLevelMatchesRendererRangeRules(int lodMask, float min, float max, float distance, int maximumMask, bool expected)
    {
        // Exercise the relocated private helper without exposing the production job.
        var type = typeof(SkinnedMeshPreparationSystem).GetNestedType("ComputeFrameSkinnedMeshesJob", BindingFlags.NonPublic);
        var job = Activator.CreateInstance(type);
        type.GetField("maximumLODLevelMask").SetValue(job, maximumMask);
        var select = type.GetMethod("IsLODRangeActive", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.AreEqual(expected, select.Invoke(job, new object[] { lodMask, min, max, distance }));
    }
}
}
