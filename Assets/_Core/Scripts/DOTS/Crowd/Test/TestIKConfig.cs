using UnityEngine;
using Unity.Assertions;
using UnityEngine.UI;


public class TestIKConfig : MonoBehaviour
{
     public static TestIKConfig Instance { get; private set; }
    
/////////////////////////////////////////////////////////////////////////////////

    void Awake()
    {
        Assert.IsNull(Instance);
        Instance = this;
    }
    public Slider overrideIKPosWeightSlider;
    public Slider overrideIKRotWeightSlider;
}
