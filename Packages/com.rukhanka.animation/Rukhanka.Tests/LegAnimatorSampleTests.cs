using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{
public class LegAnimatorSampleTests
{
    [Test]
    public void KneeHintsFollowWalkerWhileFootTargetsRemainIndependent()
    {
        var path = AssetDatabase.FindAssets("Subscene_ECS t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(x => x.EndsWith("/24. Procedural Animation/Subscene_ECS.unity"));
        
        if (path == null)
            Assert.Ignore("Requires the imported Procedural Animation sample.");

        var scene = SceneManager.GetSceneByPath(path);
        var wasLoaded = scene.isLoaded;
        
        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        
        try
        {
            var stage = scene.GetRootGameObjects().Single(x => x.name == "LegAnimatorStage").transform;
            var walker = stage.Find("ProceduralWalker");
            Assert.That(walker, Is.Not.Null);
            var hints = walker.Find("KneeHints");
            Assert.That(hints, Is.Not.Null, "Knee hints must inherit the walker's translation and rotation.");
            Assert.That(hints.childCount, Is.EqualTo(6));
            var targets = stage.Find("FootTargets");
            Assert.That(targets, Is.Not.Null, "Foot targets must stay independent so feet can remain planted.");
            Assert.That(targets.IsChildOf(walker), Is.False);
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
}
