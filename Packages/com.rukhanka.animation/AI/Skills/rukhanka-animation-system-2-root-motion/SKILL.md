---
name: "rukhanka-animation-system-2-root-motion"
description: "Use this skill whenever the user wants animation to move an ECS character, such as 'enable root motion', 'my walk animation stays in place', 'read movement from the clip', or 'let my character controller apply the movement'. Covers Humanoid and Generic root-motion setup, built-in movement, and per-frame movement data in Rukhanka Animation System 2. Do NOT use for collision or character controller implementation, or IK (see rukhanka-animation-system-2-configure-ik). When in doubt whether an ECS root-motion request could involve Rukhanka Animation System 2, use this skill; Prerequisites confirms installation."
metadata:
  asset: "Rukhanka Animation System 2"
  publisher: "Rukhanka Technologies"
  asset-version: "2.9.1"
  skill-version: "1.0.0"
  unity: "2022.3.0f1+"
  render-pipelines: "URP, HDRP"
  category: "tools/animation"
  asset-store-url: "https://assetstore.unity.com/packages/tools/animation/rukhanka-animation-system-2-298480"
  documentation-url: "https://docs.rukhanka.com/"
  support-url: "https://discord.com/invite/utdMamGbR8"
  last-verified: "2026-10-19"
---

# Configure Root Motion with Rukhanka

Enable animation-derived movement on a CPU rig, or calculate it for a gameplay system that owns movement. Verify both the computed delta and the actual entity transform so movement is neither lost nor applied twice.

## When to use this skill

- "Enable root motion on my ECS character."
- "Why does this walking clip stay in place?"
- "Extract movement but let my controller move the entity."

Not for:

- Initial rig preparation: [animate-character](../rukhanka-animation-system-2-animate-character/SKILL.md).
- IK reaching or foot targets: [configure-ik](../rukhanka-animation-system-2-configure-ik/SKILL.md).
- Collision response, navigation, or networking: integrate with the owning gameplay system.

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
- Require a working CPU rig, a compatible moving animation clip, and a saved test scene.
- Use a clip with intentional translation or rotation tracks. A genuinely in-place clip cannot yield movement absent from its authored data.
- Identify which system owns the character transform before enabling built-in movement.

## Quick start

1. Inspect the clip's model importer **Root Transform Rotation** and **Root Transform Position** settings. Configure the required channels for root-motion extraction; do not bake the intended movement entirely into the pose.
2. For a Generic rig, set the model importer's **Root node** to the bone that drives object movement.
3. On the Animator enable **Apply Root Motion**.
4. On `Rukhanka.Hybrid.RigDefinitionAuthoring`, select **CPU** and **Root Motion Mode = Normal**. Normal is the zero/default mode.
5. Keep **Rig Config Source = From Animator** for this workflow; the baker reads `Animator.applyRootMotion`.
6. Save, rebake, and play the moving clip. Sample the rig entity's position and `RootMotionVelocityComponent` on distinct frames.
7. Expected result: `RigDefinitionComponent.applyRootMotion` is true, movement deltas are nonzero during the moving portion, and the rig entity moves once per frame.

## Workflows

### Workflow: Apply movement directly from animation

**Goal:** Let Rukhanka move the rig entity from the authored clip.

**Steps:**

1. Complete Quick start and check the Generic root node if applicable.
2. Verify that no gameplay mover simultaneously overwrites the same transform.
3. Inspect the runtime `RootMotionVelocityComponent.removeBuiltinEntityMovement`; it must be false in Normal mode.
4. Test a portion of the clip known to move, not an idle segment or a paused frame.
5. If using **User Defined** rig configuration instead, set `RigDefinitionAuthoring.applyRootMotion` explicitly; there is no Animator to supply it.

**Expected result:** Animation-derived motion changes the rig entity transform in the expected direction.

### Workflow: Calculate movement for a custom mover

**Goal:** Extract root-motion data while leaving transform ownership to gameplay.

**Steps:**

1. Keep root motion enabled and the rig on CPU.
2. Set **Root Motion Mode** to **Disable Builtin Movement** on `RigDefinitionAuthoring`.
3. Rebake. Confirm `RootMotionVelocityComponent.removeBuiltinEntityMovement == true`.
4. Read `RootMotionVelocityComponent` after `Rukhanka.RukhankaAnimationSystemGroup` updates.
5. Use `worldVelocity` for a world-space velocity-based mover. `deltaPos` and `deltaRot` are the motion delta composed with the current entity pose by the built-in implementation; compose them in that same local motion space when using a delta-based mover. Do not add `deltaPos` directly to a world position on a rotated rig. Keep their units distinct: do not multiply a per-frame delta by delta time again.
6. Integrate collision/grounding using the project's existing movement system. Consume the data once per simulation step; account explicitly for that system's update rate.
7. With the custom mover temporarily inactive in a test copy, check that root deltas remain nonzero but the rig entity does not translate from Rukhanka's built-in movement. Then enable the mover and check the applied displacement.

**Expected result:** Motion is calculated each frame, and gameplay applies the intended movement exactly once.

## Verification

- Verify `RigDefinitionComponent.applyRootMotion == true`.
- Verify `GPUAnimationEngineTag` is disabled.
- Confirm `RootMotionVelocityComponent` exists; its presence alone is insufficient because the baker adds it even when root motion is off.
- Inspect `worldVelocity`, `deltaPos`, and `deltaRot` after animation processing; require finite values and nonzero movement in the known moving test segment.
- Compare the target rig's transform over multiple frames. Normal mode moves it; Disable Builtin Movement does not move it unless another mover does.
- Check the Console for baking/runtime errors.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `UnityEngine.Animator.applyRootMotion` | Property | Supplies the baked request in FromAnimator mode. |
| `Rukhanka.Hybrid.RigDefinitionAuthoring.RootMotionMode` | Enum | Selects Normal or DisableBuiltinMovement. |
| `Rukhanka.RigDefinitionComponent.applyRootMotion` | Runtime field | Enables root-motion processing for the rig. |
| `Rukhanka.RootMotionVelocityComponent` | Runtime component | Exposes world velocity, position delta, rotation delta, and movement suppression flag. |

## Common issues

- Velocity stays zero → Clip is in-place, movement baked into pose, wrong Generic root node, or root motion disabled → Check importer data and runtime applyRootMotion before changing movement code.
- Motion data exists but position does not change → Disable Builtin Movement selected → Confirm this is intentional and inspect the custom mover.
- Character moves too far or in the wrong direction → Movement applied twice, delta treated as velocity, or local delta added directly in world space → Choose one movement owner and preserve units and coordinate space.
- Root motion disappears after selecting GPU → GPU Animator does not support root motion → Return this rig to CPU.
- Runtime component exists but character remains static → Component existence mistaken for enabled processing → Check applyRootMotion and moving clip data.

## Boundaries

GPU Animator does not support root motion. Root motion supplies movement, not collision handling, navigation, gravity, or prediction. Physics-driven and networked characters need an integration that respects simulation order and transform ownership. Use native Unity Animator root motion for a GameObject-only character outside ECS.
