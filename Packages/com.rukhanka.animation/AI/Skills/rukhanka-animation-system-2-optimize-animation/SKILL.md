---
name: "rukhanka-animation-system-2-optimize-animation"
description: "Use this skill whenever the user asks to improve ECS animation performance, such as 'animate a big crowd faster', 'use GPU animation', 'stop animating offscreen characters', 'strip unused bones', or 'my animated mesh disappears at the edge of the screen'. Covers engine selection, animation culling, bone reduction, and renderer bounds in Rukhanka Animation System 2. Do NOT use for initial character setup (see rukhanka-animation-system-2-animate-character) or solver tuning (see rukhanka-animation-system-2-configure-ik). When in doubt whether an ECS animation optimization request could involve Rukhanka Animation System 2, use this skill; Prerequisites confirms installation."
metadata:
  asset: "Rukhanka Animation System 2"
  publisher: "Rukhanka Technologies"
  asset-version: "2.9.1"
  skill-version: "1.2.0"
  unity: "2022.3.0f1+"
  render-pipelines: "URP, HDRP"
  category: "tools/animation"
  asset-store-url: "https://assetstore.unity.com/packages/tools/animation/rukhanka-animation-system-2-298480"
  documentation-url: "https://docs.rukhanka.com/"
  support-url: "https://discord.com/invite/utdMamGbR8"
  last-verified: "2026-10-21"
---

# Optimize Rukhanka Animation

Reduce animation work while preserving the rig features the project uses. Measure a functional scene first, change one setting at a time, and verify runtime behavior as well as frame cost.

## When to use this skill

- "Speed up this animated crowd."
- "Use GPU animation" or "skip invisible characters."
- "Strip unused bone entities."
- "Reduce render registration cost from a large skinned prefab catalog."
- "Fix a character disappearing because its bounds are wrong."

Not for:

- Initial animation setup: [animate-character](../rukhanka-animation-system-2-animate-character/SKILL.md).
- Root-motion integration: [root-motion](../rukhanka-animation-system-2-root-motion/SKILL.md).
- Diagnosing corrupted or missing baked data: [debug-animation](../rukhanka-animation-system-2-debug-animation/SKILL.md).

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
- Require an already working scene and an identified performance issue. Capture baseline frame timings, rig count, bone count, and required features.
- Determine whether rigs need IK, root motion, user curves, blend shapes, CPU bone writeback, physics, or dynamic renderer bounds before choosing GPU.
- Use a copy or reversible settings change for comparison; preserve referenced gameplay bones and attachments.

## Quick start

1. For a basic crowd optimization, keep the existing engine and add one `Rukhanka.AnimationCullingConfig` to a GameObject in the main scene, outside all Entities SubScenes.
2. Populate **Culling Cameras** with the game cameras and **Shadow Casting Lights** with relevant shadow-casting lights. Use an empty array when no lights are required, not null.
3. Set **Add Editor Scene Camera** to false for a repeatable game-camera test; it defaults to true.
4. Set the target Animator's **Culling Mode** to **Cull Completely** or **Cull Update Transforms**, then save and rebake.
5. In Play mode move the test rig outside all configured camera and shadow visibility volumes. Inspect its `CullAnimationsTag` enabled state.
6. Return it to view and confirm animation resumes. Expected result: the invisible rig is culled, the visible rig animates, and the Console contains no missing-culling-config error.

## Workflows

### Workflow: Select CPU or GPU per rig

**Goal:** Choose the engine that supports the rig's required features.

**Steps:**

1. Keep **Animation Engine = CPU** when the rig requires IK, root motion, user curves, blend shapes, CPU bone writeback, physics integration, or renderer bounding-box recalculation.
2. For a rig without these requirements, set **Animation Engine = GPU** on `RigDefinitionAuthoring` and rebake.
3. Inspect the rig's `GPUAnimationEngineTag`. Enabled selects GPU; disabled selects CPU.
4. For an intentional runtime switch, use `EntityManager.SetComponentEnabled<Rukhanka.GPUAnimationEngineTag>(rigEntity, useGpu)` on the existing component. Do not infer component absence from a query that excludes disabled enableable components.
5. Compare animation output and timings with the same rig count, camera, warmup, and rendering workload. GPU is an option to measure, not an unconditional improvement.

**Expected result:** The enabled tag matches the requested engine and all required features remain functional.

### Workflow: Configure camera and shadow-aware culling

**Goal:** Skip animation pose calculation for invisible rigs.

**Steps:**

1. Use exactly one active `AnimationCullingConfig`; its Awake rejects multiple instances.
2. Place it in the main scene so baking does not destroy it. Set both camera and light arrays explicitly.
3. Configure all relevant cameras and shadow-casting lights. A non-shadow-casting light in the list is unnecessary and produces a warning.
4. For FromAnimator rigs, use Animator Culling Mode. **Always Animate** disables culling; **Cull Update Transforms** and **Cull Completely** both request animation culling.
5. For UserDefined rigs, set `RigDefinitionAuthoring.animationCulling` explicitly.
6. When using HDRP directional-light shadow culling, verify the expected global volume profile includes shadow settings. Check the actual camera/light configuration before changing bounds.
7. Test both in-view and out-of-view poses. If Scene view counts as a camera, it can keep a supposedly invisible rig visible.

**Expected result:** Culled rigs have `CullAnimationsTag` enabled; visible rigs resume pose calculation. Root motion, user curves, and animation events continue processing for culled entities as supported by their engine.

### Workflow: Reduce bone entities or rig bone count

**Goal:** Remove unnecessary work while preserving required bones.

**Steps:**

1. Keep model importer **Optimize Game Objects** disabled; baking needs the full source hierarchy.
2. Start with **Bone Entity Stripping Mode = Automatic** on `RigDefinitionAuthoring`. It removes unreferenced bone entities while retaining referenced ones, including supported attachment/IK uses.
3. Compare the number of `AnimatorEntityRefComponent` bone entities referencing the target rig, and check attachments and gameplay references.
4. For precise entity selection, create an Avatar Mask from the correct skeleton, enable the bones to retain, set **Bone Entity Stripping Mode = Manual**, and assign **Bone Stripping Mask**.
5. Manual stripping flattens the bone entity hierarchy. Verify retained animated bones have animation tracks; otherwise their unparented LocalTransform can drive a world-space pose unexpectedly.
6. Use **Avatar Optimization Mask** only when reducing the animation rig's bone list itself, such as unused modular mesh nodes. This differs from stripping ECS bone entities; retain every bone needed for skinning, animation, or gameplay.
7. Rebake and compare both counts and behavior. A smaller count is not a success if a required attachment or deformation breaks.

**Expected result:** The selected count decreases where removable bones exist, and retained gameplay bones and skinning still behave correctly.

### Workflow: Update renderer bounds on CPU rigs

**Goal:** Keep the renderer visible when animation extends outside its original bounds.

**Steps:**

1. Confirm the affected rig uses CPU.
2. Enable **Update When Offscreen** on the intended `UnityEngine.SkinnedMeshRenderer` and rebake.
3. Inspect the baked renderer setup for `Rukhanka.ShouldUpdateBoundingBoxTag`.
4. Play an animation with substantial bone movement and inspect runtime renderer bounds over multiple frames. Check the edge-of-view case that previously disappeared.
5. For GPU rigs, provide suitable authored bounds instead; CPU bone-based recalculation is unavailable.

**Expected result:** Supported CPU renderer bounds update with the pose and no longer incorrectly exclude the animated mesh.

### Workflow: Reduce render registration cost for skinned asset catalogs

**Goal:** Verify automatic baking-time sharing of render arrays when a catalog contains many distinct skinned meshes.

**Steps:**

1. Rebake the affected SubScenes after upgrading to a package revision using Entities Graphics render-array baking. Previously serialized entity scenes retain the old layout until rebaked.
2. Rukhanka supplies `RenderMeshUnmanaged` and extra-material baking inputs. Entities Graphics creates shared render arrays and remaps `MaterialMeshInfo` within each scene section, including prefabs and disabled renderers; there is no authoring switch. Arrays can also contain ordinary Entities Graphics renderers.
3. Count arrays referenced by the target section's Rukhanka renderers with queries that include prefabs, disabled entities, and disabled `MaterialMeshInfo`. A clean bake should consolidate a normal-sized catalog into one array per section. Incremental baking may leave multiple arrays; newer Entities Graphics versions may retain unused mesh/material entries to preserve indices. Do not require compaction after each edit.
4. Custom material or mesh override code must resolve indices against the entity's current `RenderMeshArray`. Do not assume the renderer's mesh occupies array index zero. One Entity Per Submesh uses direct-index MMI mode and preserves its submesh index; a single-material renderer also uses direct indexing. Multiple materials on one entity use range mode.
5. Instantiate catalog prefabs, exercise multi-material and split-submesh renderers and LODs, then incrementally rebake one renderer's mesh/material assignment and delete another. Verify unchanged renderers as well as changed ones. Check section streaming and compare `RegisterMaterialsAndMeshes` timings under the same workload.
6. Very large multi-material catalogs remain subject to the installed Entities Graphics version's packed range limits. Rukhanka does not add automatic table splitting. Verify large catalogs separately and partition content into scene sections if necessary.

**Expected result:** A clean bake produces substantially fewer shared render arrays while mesh/material selection, deformation, and LOD behavior remain correct. Incremental updates preserve rendering correctness, without guaranteeing one array or removal of stale entries. Array sharing reduces registration bookkeeping; it does not merge geometry or guarantee fewer draw calls.

## Verification

- Check `GPUAnimationEngineTag` enabled state against the requested engine.
- Confirm exactly one main-scene `AnimationCullingConfig`, valid camera/light arrays, and the intended Animator culling modes.
- Inspect `CullAnimationsTag` through the EntityManager while moving the target across visibility boundaries; use an entity reference or query that includes disabled components.
- Compare per-rig bone entity count using `AnimatorEntityRefComponent.animatorEntity`, not a whole-world total.
- For rig masks, inspect `RigDefinitionComponent.rigBlob.Value.bones.Length` before and after baking.
- For bounds, verify the update tag and changing runtime bounds during a moving pose.
- For skinned catalogs, inspect per-section shared-array counts after rebaking and verify both MMI modes still select the original meshes, materials, and submeshes.
- Compare profiler timings only after warmup under the same workload; report hardware, engine, counts, and sample duration.
- Require no compile errors, missing reference errors, or new runtime exceptions.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Rukhanka.GPUAnimationEngineTag` | Enableable component | Selects GPU when enabled and CPU when disabled. |
| `Rukhanka.AnimationCullingConfig` | Main-scene MonoBehaviour | Supplies cameras, lights, and debugging options. |
| `Rukhanka.CullAnimationsTag` | Enableable component | Marks rigs currently culled from pose processing. |
| `Rukhanka.Hybrid.RigDefinitionAuthoring.BoneEntityStrippingMode` | Enum | Selects None, Automatic, or Manual entity stripping. |
| `Rukhanka.AnimatorEntityRefComponent` | Runtime component | Relates a bone entity to its rig and bone index. |
| `Rukhanka.ShouldUpdateBoundingBoxTag` | Enableable component | Requests supported renderer bounds updates. |

## Common issues

- Offscreen character still animates → Another camera, Scene view, or visible shadow keeps it relevant → Inspect every configured visibility source.
- Culling setup disappears at runtime → Configuration is in a SubScene → Move it to the main scene and rebake.
- Startup throws for multiple culling configurations → More than one active configuration → Consolidate them and their camera/light lists.
- GPU choice breaks IK or movement → Required feature unavailable on GPU → Keep that rig on CPU.
- Manual stripping changes bone poses → Flattened hierarchy lacks animation tracks → Restore required tracks or use Automatic/None.
- Mask is ignored during baking → Avatar optimization mask skeleton count differs → Build the mask from the matching Avatar and inspect the warning.

## Boundaries

GPU Animator cannot support all CPU features; consult the package's Animation Engines feature table before switching. Culling is not equivalent to stopping all animation-related logic. These settings do not optimize gameplay AI, spawning, draw calls, or third-party physics. Advanced project-wide deformation storage and skinning switches require separate shader compatibility checks and are outside this per-rig workflow.
