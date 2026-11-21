using NUnit.Framework;
using Rukhanka.Toolbox;
using Unity.Collections;
using UnityEngine;
using Random = Unity.Mathematics.Random;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{ 
public class BitFieldNTest
{
    [Test]
    public unsafe void WordBoundaryTest()
    {
        var memory = stackalloc uint[3];
        var bits = new BitFieldN(memory, 3);
        var positions = new[] { 0, 31, 32, 63, 64, 95 };

        foreach (var position in positions)
            bits.Set(position, true);

        for (var position = 0; position < 96; ++position)
            Assert.AreEqual(System.Array.IndexOf(positions, position) >= 0, bits.IsSet(position));

        bits.Set(32, false);
        Assert.IsFalse(bits.IsSet(32));
        Assert.IsTrue(bits.IsSet(31));
        Assert.IsTrue(bits.IsSet(63));
        bits.Set(32, true);

        foreach (var position in positions)
            bits.Set(position, false);

        Assert.IsFalse(bits.TestAny());
    }
    
/////////////////////////////////////////////////////////////////////////////////

    [Test]
    public unsafe void FunctionalityTest()
    {
        var rng = new Random(0x6E624EB7u);
        var maxBitfieldSize = rng.NextInt(300, 1000);
        var bitFieldMem = stackalloc uint[maxBitfieldSize];
        
        var numTests = rng.NextUInt(100);
        for (var i = 0; i < numTests; ++i)
        {
            var sz = rng.NextInt(1, maxBitfieldSize);
            var bf = new BitFieldN(bitFieldMem, sz);
            
            var i0 = rng.NextInt(0, bf.Length);
            bf.Set(i0, true);
            Assert.IsTrue(bf.IsSet(i0));
            Assert.IsTrue(bf.TestAny());
            bf.Set(i0, false);
            Assert.IsFalse(bf.IsSet(i0));
            Assert.IsFalse(bf.TestAny());
        }
    }
}
}
