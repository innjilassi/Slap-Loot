using NUnit.Framework;
using Rukhanka.Toolbox;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class MathUtilsRotationTests
{
    [TestCase(1, 0, 0, 1, 0, 0)]
    [TestCase(1, 0, 0, 0, 1, 0)]
    [TestCase(1, 2, 3, -2, 4, 1)]
    [TestCase(1, 0, 0, -1, 0, 0)]
    [TestCase(0, 1, 0, 0, -1, 0)]
    [TestCase(0, 0, 1, 0, 0, -1)]
    [TestCase(2, -3, 4, -2, 3, -4)]
    [TestCase(1, 0, 0, -1, 0.00001f, 0)]
    [TestCase(1, 2, 3, -1, -1.99998f, -3.00001f)]
    [TestCase(1, 0, 0, 1, 0.00001f, 0)]
    public void NormalizedDirectionsProduceAUnitRotationThatAlignsThem(float fx, float fy, float fz, float tx, float ty, float tz)
    {
        var from = math.normalize(new float3(fx, fy, fz));
        var to = math.normalize(new float3(tx, ty, tz));
        var rotation = MathUtils.FromToRotationForNormalizedVectors(from, to);
        Assert.That(math.all(math.isfinite(rotation.value)), Is.True);
        Assert.That(math.length(rotation.value), Is.EqualTo(1).Within(0.00001f));
        Assert.That(math.distance(math.rotate(rotation, from), to), Is.LessThan(0.00001f));
    }

/////////////////////////////////////////////////////////////////////////////////

    [TestCase(0.05f, 0.08f, 0.02f)]
    [TestCase(1, 0, 0)]
    [TestCase(0, 1, 0)]
    [TestCase(2, -3, 4)]
    public void OppositeDirectionsProduceAUnitHalfTurn(float x, float y, float z)
    {
        var from = new float3(x, y, z);
        var rotation = MathUtils.FromToRotation(from, -from);
        Assert.That(math.length(rotation.value), Is.EqualTo(1).Within(0.00001f));
        Assert.That(math.distance(math.rotate(rotation, math.normalize(from)), -math.normalize(from)), Is.LessThan(0.00001f));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void NearlyOppositeDirectionsRemainStable()
    {
        var from = math.normalize(new float3(1, 2, 3));
        var to = math.normalize(-from + new float3(0.00001f, -0.00002f, 0));
        var rotation = MathUtils.FromToRotation(from, to);
        Assert.That(math.length(rotation.value), Is.EqualTo(1).Within(0.00001f));
        Assert.That(math.distance(math.rotate(rotation, from), to), Is.LessThan(0.00001f));
    }

/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public void MissingDirectionDoesNotScaleTheChain()
    {
        var rotation = MathUtils.FromToRotation(float3.zero, math.up());
        Assert.That(math.length(rotation.value), Is.EqualTo(1).Within(0.00001f));
        Assert.That(math.distance(math.rotate(rotation, math.right()), math.right()), Is.LessThan(0.00001f));
    }
}
}
