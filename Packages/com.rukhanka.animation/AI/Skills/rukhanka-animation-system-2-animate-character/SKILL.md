---
name: "rukhanka-animation-system-2-animate-character"
description: "Use this skill whenever the user wants to animate a DOTS character, such as 'make this model play idle', 'convert my animator to ECS', 'set up a generic rig', or 'my character is stuck in bind pose'. Covers model import, rig authoring, controllers, SubScene baking, and deformation materials in Rukhanka Animation System 2. Do NOT use for runtime parameter control (see rukhanka-animation-system-2-control-animation) or IK (see rukhanka-animation-system-2-configure-ik). When in doubt whether a character animation request could involve Rukhanka Animation System 2, use this skill; Prerequisites confirms installation."
metadata:
  asset: "Rukhanka Animation System 2"
  publisher: "Rukhanka Technologies"
  asset-version: "2.9.1"
  skill-version: "1.0.1"
  unity: "2022.3.0f1+"
  render-pipelines: "URP, HDRP"
  category: "tools/animation"
  asset-store-url: "https://assetstore.unity.com/packages/tools/animation/rukhanka-animation-system-2-298480"
  documentation-url: "https://docs.rukhanka.com/"
  support-url: "https://discord.com/invite/utdMamGbR8"
  last-verified: "2026-10-23"
---

# Animate a Character with Rukhanka

Create a character that bakes into ECS and plays a controller-driven animation with the correct deformation material. Begin with the CPU animator and Always Animate culling to make the initial result easy to verify.

## When to use this skill

- "Make this character play an idle loop in a SubScene."
- "Convert my Animator setup to DOTS."
- "My generic avatar won't bake" or "the mesh stays in bind pose."

Not for:

- Runtime inputs and controller state queries: [control-animation](../rukhanka-animation-system-2-control-animation/SKILL.md).
- Root-driven movement: [root-motion](../rukhanka-animation-system-2-root-motion/SKILL.md).
- Ordinary GameObject animation without Rukhanka: use Unity's Animator workflow.

## Prerequisites

- Target Rukhanka Animation System 2 version 2.9.1, Unity 2022.3.0f1 or newer, and URP or HDRP. Built-in rendering is unsupported.
- Require Entities 1.4.3+ and Entities Graphics 1.4.16+; the package manifest directly depends on Entities Graphics 1.4.16. Confirm that the chosen package versions support the project's Unity version.
- Before using package APIs, run these statements in an Editor C# execution tool:

```csharp
var installed = false;
foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
    installed |= assembly.GetType("Rukhanka.Hybrid.RigDefinitionAuthoring") != null;
UnityEngine.Debug.Log(installed ? "PASS: Rukhanka authoring available"
    : "FAIL: Rukhanka authoring unavailable");
```

- If the check fails, report that Rukhanka is absent or has not compiled. Check package resolution and Console errors before proceeding; direct the user to [Rukhanka Animation System 2](https://assetstore.unity.com/packages/tools/animation/rukhanka-animation-system-2-298480) if it is not installed.
- Execute statement snippets through an Editor C# execution tool. Put complete system/class examples in the consuming project's own assembly with the required package references. Do not place customer scripts inside the installed package.
- Require a model, compatible animation clip, Avatar, Animator Controller, writable consuming-project scene, and a deformation-compatible material for the active pipeline.
- Use an imported Basic Animation sample as a known working reference when project assets are not yet prepared.
- Preserve the user's scene and prefab choices. Make changes in their project or a test copy, not in the installed package source.

## Quick start

1. Select the model asset and inspect its Rig import settings. Disable **Optimize Game Objects** so the baker can inspect the complete skeleton.
2. For avatar-backed models, set **Animation Type** to **Humanoid** and apply once to populate accessible skeleton information. For a Generic model, switch back to **Generic** and apply. Do not proceed if the intended avatar or skeleton data cannot be produced; inspect the model's rig.
3. Place a model instance in an Entities SubScene in the consuming project.
4. Add `Rukhanka.Hybrid.RigDefinitionAuthoring` to the same object as `UnityEngine.Animator`. Set **Rig Config Source** to **From Animator** and **Animation Engine** to **CPU**.
5. Assign the model Avatar and a controller with one default state using the supplied clip to the Animator. Set **Culling Mode** to **Always Animate**.
6. Assign deformation-compatible materials to all skinned renderers using the workflow below.
7. Save the scene and SubScene, wait for baking, and enter Play mode. Run the runtime check below twice while the simulation advances.
8. Expected result: the rig has a created blob, a nonempty animation submission buffer, advancing controller time, and a visibly deforming mesh.

## Workflows

### Workflow: Prepare Humanoid or Generic avatar authoring

**Goal:** Bake a controller-driven model with complete rig information.

**Steps:**

1. Complete the importer preparation in Quick start. Keep bone names unique within the rig; the baker hashes names and rejects duplicate bones.
2. On the authoring object set `rigConfigSource = FromAnimator`. In this mode the baker takes the Avatar, root-motion flag, and culling flag from the Animator.
3. Assign a valid controller and Avatar. Verify `GetAvatar()` returns the expected asset.
4. Leave **Bone Entity Stripping Mode** at **None** for initial setup; this is the zero/default enum value. Configure stripping only after establishing a working rig.
5. For an existing compatible model selected in the Editor, the following statements configure its authoring without replacing its controller or Avatar:

```csharp
var go = UnityEditor.Selection.activeGameObject;
if (go == null) throw new System.InvalidOperationException("Select the model instance.");
var animator = go.GetComponent<UnityEngine.Animator>();
if (animator == null || animator.runtimeAnimatorController == null || animator.avatar == null)
    throw new System.InvalidOperationException("Assign an Animator, controller and Avatar first.");
UnityEditor.Undo.RecordObject(animator, "Configure Rukhanka Animator");
animator.cullingMode = UnityEngine.AnimatorCullingMode.AlwaysAnimate;
var rig = go.GetComponent<Rukhanka.Hybrid.RigDefinitionAuthoring>();
if (rig == null)
    rig = UnityEditor.Undo.AddComponent<Rukhanka.Hybrid.RigDefinitionAuthoring>(go);
UnityEditor.Undo.RecordObject(rig, "Configure Rukhanka Rig");
rig.rigConfigSource = Rukhanka.Hybrid.RigDefinitionAuthoring.RigConfigSource.FromAnimator;
rig.animationEngine = Rukhanka.Hybrid.RigDefinitionAuthoring.AnimationEngine.CPU;
UnityEditor.EditorUtility.SetDirty(animator);
UnityEditor.EditorUtility.SetDirty(rig);
```

6. Save the authoring scene with the Editor and wait for baking.

**Expected result:** A baked entity carries `RigDefinitionComponent`, `AnimationToProcessComponent`, and controller data.

### Workflow: Assign a deformation-compatible material

**Goal:** Render the computed skinning instead of a static mesh.

**Steps:**

1. For the fastest verified result, reuse a matching-pipeline material from the imported Basic Animation sample.
2. For a new Shader Graph, create a graph for the active URP or HDRP pipeline.
3. Drag the subgraph from `Packages/com.rukhanka.animation/Rukhanka.Editor/ShaderGraph/` into the graph. Identify the **Rukhanka Deformation** subgraph.
4. Connect its position, normal, and tangent outputs to the corresponding vertex-stage inputs.
5. Add `_DeformedMeshIndex` as Float and `_DeformationParamsForMotionVectors` as Vector4, with **Hybrid Per Instance** declarations.
6. Save the graph, create a material using it, and assign it to each skinned renderer. Keep motion-vector configuration consistent with the project's Rukhanka settings.

**Expected result:** Skinning affects visible vertices in Play mode, with no shader compile errors. Property presence alone is not proof of correct graph wiring.

## Verification

Run these statements in Play mode in a local, non-networked test scene. For multiple worlds or rigs, select the intended world/entity through the Editor tools before attributing a result to a character.

```csharp
var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
if (world == null || !world.IsCreated)
    throw new System.InvalidOperationException("Enter Play mode and wait for world creation.");
var em = world.EntityManager;
using var query = em.CreateEntityQuery(
    Unity.Entities.ComponentType.ReadOnly<Rukhanka.RigDefinitionComponent>(),
    Unity.Entities.ComponentType.ReadOnly<Rukhanka.AnimationToProcessComponent>());
using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
UnityEngine.Debug.Log("Runtime rig count: " + entities.Length);
foreach (var entity in entities)
{
    var rig = em.GetComponentData<Rukhanka.RigDefinitionComponent>(entity);
    var clips = em.GetBuffer<Rukhanka.AnimationToProcessComponent>(entity, true);
    UnityEngine.Debug.Log(entity + " blob=" + rig.rigBlob.IsCreated + " clips=" + clips.Length);
    if (em.HasBuffer<Rukhanka.AnimatorControllerLayerComponent>(entity))
    {
        var layers = em.GetBuffer<Rukhanka.AnimatorControllerLayerComponent>(entity, true);
        var state = new Rukhanka.AnimatorStateQueryAspect(layers);
        if (layers.Length > 0)
            UnityEngine.Debug.Log("Layer 0 time=" + state.GetLayerCurrentStateInfo(0).normalizedTime);
    }
}
```

- Confirm the intended rig blob is created and clip count exceeds zero.
- Compare controller time from two distinct frames; it must progress for a playing, non-paused state.
- Confirm all assigned shaders compile and render with deformation; inspect actual vertex/bone movement.
- Check the Console for new baking, shader, or runtime errors.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Rukhanka.Hybrid.RigDefinitionAuthoring.GetAvatar()` | Method | Resolves the configured Avatar. |
| `Rukhanka.Hybrid.RigDefinitionAuthoring.RigConfigSource` | Enum | Chooses FromAnimator or UserDefined rig configuration. |
| `Rukhanka.RigDefinitionComponent` | Runtime component | Holds the baked rig blob and root-motion setting. |
| `Rukhanka.AnimationToProcessComponent` | Buffer element | Holds clips and weights submitted for animation. |
| `Rukhanka.AnimatorStateQueryAspect` | Struct | Reads controller state and transition information. |

## Common issues

- Baking reports missing skeleton information → Generic import lacks the required avatar skeleton data → Apply Humanoid once, return to Generic, and recheck the Avatar.
- Bone transforms missing during baking → Optimize Game Objects enabled → Disable it and reimport.
- Controller time advances but mesh stays static → Material does not read deformation buffers → Use a working sample material or fix graph connections.
- Skinned or blend-shape mesh invisible on Unity 6.6+ with "Mesh (…) is not readable and cannot be deformed" error → Unity 6.6+ cannot enable raw vertex buffer access on non-readable meshes → Enable **Read/Write** in the model import settings and reimport.
- Rig looks correct as a GameObject but no runtime rig exists → Authoring is outside a baking SubScene or baking failed → Check scene placement and the first baking error.
- Missing culling configuration error → Animator requests culling → Use Always Animate initially, or follow optimize-animation to configure culling.

## Boundaries

This workflow uses standard controller and avatar authoring; it does not create animation clips or infer a correct humanoid mapping for an arbitrary skeleton. Rukhanka does not support Legacy animation or every Mecanim setting. For rigs built without an Avatar, inspect the separate user-defined hierarchy workflow in the package documentation rather than forcing a broken humanoid import. Use the control-animation skill for intentional controller-free playback.

