---
name: "rukhanka-animation-system-2-configure-ik"
description: "Use this skill whenever the user asks for character reaching or aiming, such as 'make the hand reach this target', 'aim the spine at the camera', 'bend the knee the other way', or 'set up a tentacle IK chain'. Covers Two Bone IK, FABRIK, Aim, and Override Transform on CPU rigs in Rukhanka Animation System 2. Do NOT use for procedural walking or ragdolls, or base model setup (see rukhanka-animation-system-2-animate-character). When in doubt whether an ECS IK request could involve Rukhanka Animation System 2, use this skill; Prerequisites confirms installation."
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

# Configure Rukhanka Inverse Kinematics

Add CPU animation constraints that reach targets, aim bone chains, or override a bone pose. Configure references on authoring bones and verify the resulting baked components and motion.

## When to use this skill

- "Make the character's hand reach this object."
- "Add a knee hint" or "aim the upper body at a target."
- "Make a chain reach with FABRIK."
- "Override one bone's position or rotation."

Not for:

- Initial rig import: [animate-character](../rukhanka-animation-system-2-animate-character/SKILL.md).
- Selecting a GPU crowd engine: [optimize-animation](../rukhanka-animation-system-2-optimize-animation/SKILL.md).
- Procedural stepping, ground probes, or physics ragdolls: use their dedicated package examples and integration documentation.

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
- Require a working rig in a SubScene with `animationEngine = CPU`; GPU Animator does not support these IK algorithms.
- Identify the intended root/mid/tip bones and give targets bakeable transforms in the SubScene.
- Begin with **Bone Entity Stripping Mode = None** and **Always Animate** culling to establish a functional constraint.
- Keep targets independent of the affected chain where possible to avoid a target moving with its own solution.

## Quick start

1. On the root bone of a two-segment chain, add `Rukhanka.Hybrid.TwoBoneIKAuthoring`.
2. Assign **Mid** to its middle bone, **Tip** to the end bone, and **Target** to an independent target transform.
3. Assign **Mid Bent Hint** to a transform in the desired bend direction. The source field is spelled `midBentHint`.
4. Set **Weight** to `1`, its default. Position the target inside the chain's reach, away from the root and singular alignments.
5. Save, rebake, and enter Play mode. Move the baked target using its ECS transform during Play mode, or reposition its authoring transform and rebake outside Play mode.
6. Expected result: the root entity carries an enabled `TwoBoneIKComponent`; the tip approaches the reachable target and follows target changes.

## Workflows

### Workflow: Configure Two Bone IK and bend direction

**Goal:** Reach with an arm or leg while controlling its bend plane.

**Steps:**

1. Attach the component to the first of the two segments, not the rig object unless that object is the actual chain root.
2. Fill all chain and target references; the baker converts them to dynamic entity references.
3. Use weight in `[0,1]`: `0` removes IK influence and `1` requests the full solution. These are influence values, not reach-length settings.
4. Place the hint away from the limb axis in the intended elbow/knee direction.
5. Compare tip-target distance with weight `0` and `1` on a stable pose. Check a reachable, nondegenerate target before troubleshooting more complex poses.

**Expected result:** A valid chain reaches toward the target, and the hint controls the intended bend direction without invalid transforms.

### Workflow: Configure a FABRIK chain

**Goal:** Make more than two connected segments reach a position.

**Steps:**

1. Add `Rukhanka.Hybrid.FABRIKAuthoring` to the root bone of the affected chain.
2. Set **Tip** to the descendant end bone and **Target** to a valid independent transform.
3. Begin with source defaults: `weight = 1`, `numIterations = 15`, and `threshold = 0.00001`.
4. Increase the iteration budget only when a reachable target fails to converge adequately. Relax the threshold for lower-cost, less precise solving.
5. Rebuild/bake and check `FABRIKComponent` references, iteration count, threshold, and tip-target distance across a few stable frames.

**Expected result:** The selected chain approaches the target within the chosen tolerance when its geometry and iteration budget allow it.

### Workflow: Aim bones toward a target

**Goal:** Rotate an ordered bone list to face a target.

**Steps:**

1. Add `Rukhanka.Hybrid.AimIKAuthoring` to a bone in the animated rig.
2. Set **Target** and a nonzero **Forward Vector** matching the rig's local forward axis; the default is `Vector3.forward`.
3. Set **Angle Limit Min** and **Angle Limit Max** deliberately. Both default to `0`, which clamps the correction to zero. For an unrestricted initial test, use `0` and `180` degrees.
4. Fill **Affected Bones** with `WeightedTransform` entries in parent-before-child order. Set an explicit positive per-bone weight; component weight defaults to `1`.
5. Rebake and compare the bone's rotated forward axis to the direction toward the target. The baker converts degree limits to radians.

**Expected result:** `AimIKComponent` and the `AimIKAffectedBoneComponent` buffer exist, and the chain rotates within the configured limits.

### Workflow: Override a bone pose

**Goal:** Copy a target's position and/or rotation onto one bone.

**Steps:**

1. Add `Rukhanka.Hybrid.OverrideTransformIKAuthoring` to the affected bone.
2. Set **Target** and choose **Position Weight** and **Rotation Weight**; both default to `1`.
3. Set one weight to `0` when preserving that part of the animated pose.
4. Rebake and compare the resulting position/rotation to the target under a stable animation pose.

**Expected result:** The baked `OverrideTransformIKComponent` applies only the intended weighted channels.

## Verification

- Verify authoring references are non-null and the tip descends from the intended root.
- In the runtime world, inspect the corresponding IK component and its enabled state.
- Require `AnimatorEntityRefComponent` on affected bone entities and confirm they refer to the intended rig.
- Require `GPUAnimationEngineTag` disabled on that rig.
- Check all runtime target/mid/tip/hint entities exist before reading their transforms.
- On a reachable test pose, measure tip-target distance after transforms update; compare before and after enabling IK. For Aim, measure the angle from the rotated forward vector toward the target.
- Require finite positions/rotations and no new Console errors. Do not assert exact convergence for unreachable or singular targets.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Rukhanka.Hybrid.TwoBoneIKAuthoring` | Authoring component | Configures root, mid, tip, target, hint, and weight. |
| `Rukhanka.Hybrid.FABRIKAuthoring` | Authoring component | Configures chain tip, target, iteration count, and tolerance. |
| `Rukhanka.Hybrid.AimIKAuthoring` | Authoring component | Configures ordered weighted bones, forward axis, and angle limits. |
| `Rukhanka.Hybrid.OverrideTransformIKAuthoring` | Authoring component | Copies target position/rotation with separate weights. |
| `Rukhanka.TwoBoneIKComponent` | Enableable runtime component | Holds baked chain entity references. |
| `Rukhanka.RukhankaAnimationInjectionSystemGroup` | System group | Hosts pose modification between animation processing and application. |

## Common issues

- IK does nothing → Rig uses GPU Animator → Select CPU and rebake.
- Aim never turns → Both angle limits remain at zero or affected-bone weights are zero → Set explicit limits and positive weights.
- Arm bends unexpectedly → Hint lies on the limb axis or on the wrong side → Reposition it away from the axis in the desired bend direction.
- Chain stops short → Target unreachable or iterations insufficient → Test a reachable target first; then tune FABRIK iterations/tolerance.
- Missing bone reference at runtime → Wrong bone, stripping, or target not baked → Restore references and establish behavior with stripping disabled.
- Target moves when the solved bone moves → Target parented under the same affected chain → Move the target outside that chain.

## Boundaries

These constraints run on CPU rigs and do not provide Unity Animator IK Pass or Foot IK checkbox behavior. They do not implement a walking gait, ground probing, character collision, or ragdoll simulation. Do not modify solver source to compensate for misconfigured references or unreachable targets. Overlapping constraints require explicit ownership and update-order review; do not assume Inspector component order defines solver order.

