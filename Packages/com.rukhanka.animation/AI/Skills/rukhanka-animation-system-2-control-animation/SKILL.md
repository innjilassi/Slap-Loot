---
name: "rukhanka-animation-system-2-control-animation"
description: "Use this skill whenever the user asks to drive character animation from gameplay, such as 'set the run speed', 'trigger a jump', 'which animation state am I in', 'handle footstep events', or 'play a clip without an Animator'. Covers Rukhanka Animation System 2 parameters, state queries, event buffers, and scripted clip submission. Do NOT use for model import or materials (see rukhanka-animation-system-2-animate-character) or movement extraction (see rukhanka-animation-system-2-root-motion). When in doubt whether ECS animation control could involve Rukhanka Animation System 2, use this skill; Prerequisites confirms installation."
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

# Control Rukhanka Animation from ECS

Connect gameplay to baked animation data. Write controller parameters before animation processing, read state and event results after processing, and use explicit clip submission only when the rig intentionally has no controller.

## When to use this skill

- "Use my speed value to blend idle and running."
- "Trigger jump" or "tell me when this state finishes."
- "Play footstep sounds from animation events."
- "Play these clips without a Mecanim controller."

Not for:

- Model setup and shaders: [animate-character](../rukhanka-animation-system-2-animate-character/SKILL.md).
- Root-motion movement: [root-motion](../rukhanka-animation-system-2-root-motion/SKILL.md).
- Bone pose constraints: [configure-ik](../rukhanka-animation-system-2-configure-ik/SKILL.md).

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
- Require a baked, playing rig; use the animate-character skill to establish one.
- For controller control, know the exact parameter names and types in the assigned controller.
- Identify the intended rig using a gameplay tag or explicit entity reference. A whole-world query must not change unrelated characters.

## Quick start

1. In the test controller, define a Float parameter named **Speed** and connect it to a transition or blend tree.
2. Copy the parameter helper below into the consuming project's gameplay assembly. Call it from the project's existing animation input system before `Rukhanka.RukhankaAnimationSystemGroup`, passing only the target rig entity's buffer and table.
3. Read the value back with `GetFloatParameter` and confirm it is `1`.
4. Observe the configured transition or blend tree and check layer state/time. Expected result: the parameter changes and drives the intended animation.

```csharp
public static class RukhankaSpeedInput
{
    private static readonly Rukhanka.FastAnimatorParameter Speed =
        new Rukhanka.FastAnimatorParameter(new Unity.Collections.FixedString512Bytes("Speed"));

    public static bool Apply(
        Unity.Entities.DynamicBuffer<Rukhanka.AnimatorControllerParameterComponent> buffer,
        Rukhanka.AnimatorControllerParameterIndexTableComponent table,
        float speed)
    {
        var parameters = new Rukhanka.AnimatorParametersAspect(buffer, table);
        if (!parameters.HasParameter(Speed)) return false;
        parameters.SetFloatParameter(Speed, speed);
        return parameters.GetFloatParameter(Speed) == speed;
    }
}
```

Return `false` as a configuration failure; do not silently add a runtime parameter that the baked controller does not know.

## Workflows

### Workflow: Set parameters and triggers

**Goal:** Change the state machine through its authored inputs.

**Steps:**

1. Read `AnimatorControllerParameterComponent` as a writable buffer and `AnimatorControllerParameterIndexTableComponent` from the target rig.
2. Construct `AnimatorParametersAspect(buffer, table)`.
3. Cache `FastAnimatorParameter` values by exact name, using `FixedString512Bytes`, rather than depending on parameter array order.
4. Check `HasParameter`, then call the matching typed setter: `SetFloatParameter`, `SetIntParameter`, or `SetBoolParameter`. The caller must match the controller parameter type.
5. For a Trigger parameter call `SetTrigger` once on the gameplay event. Use `ResetTrigger` to cancel an unconsumed request. Do not write true every frame unless repeated triggering is intended.
6. Run writes before the animation group. Inspect the resulting state after that group.

**Expected result:** Readback matches the requested input and authored transition conditions produce the intended state.

### Workflow: Query state and consume events

**Goal:** React to current states, footsteps, and state entry/exit without GameObject callbacks.

**Steps:**

1. Build `AnimatorStateQueryAspect` from the target's `AnimatorControllerLayerComponent` buffer. Check its length before indexing.
2. Call `GetLayerCurrentStateInfo(layerIndex)`, `GetLayerCurrentTransitionInfo(layerIndex)`, and `IsInTransition(layerIndex)`.
3. Compare hashes created with Rukhanka's `Rukhanka.Toolbox.FixedStringExtensions.CalculateHash32`, not `UnityEngine.Animator.StringToHash`. Confirm the state/transition's authored name.
4. For clip events, enable **Has Animation Events** on `RigDefinitionAuthoring` (default false), author events in the clip importer or Animation window, then rebake.
5. Read `DynamicBuffer<Rukhanka.AnimationEventComponent>` after `RukhankaAnimationSystemGroup` for the current frame. Match `nameHash`; use `floatParam`, `intParam`, and `stringParamHash` as needed.
6. For controller events, enable **Has Animator Controller Events** (default false), rebake, and read `AnimatorControllerEventComponent`. Handle `StateEnter`, `StateExit`, or `StateUpdate`.
7. Copy required event values into gameplay-owned data in the same frame. Buffers are refreshed each processing frame; do not retain buffer references across frames or wait several frames to consume events.

**Expected result:** The target's layer state is identified and authored event frames generate the expected gameplay reactions.

### Workflow: Submit a clip without an Animator Controller

**Goal:** Drive a rig with custom clip selection and normalized time.

**Steps:**

1. Set **Rig Config Source** to **User Defined**, assign the model Avatar explicitly, and remove the Animator only for this intentionally controller-free rig.
2. Add `Rukhanka.Hybrid.AnimationAssetSetAuthoring`; populate `animationClips` with valid clips and initialize `avatarMasks` to an empty array if no masks are required.
3. Bake the selected clip's hash using `Rukhanka.Hybrid.BakingUtils.ComputeAnimationHash(clip, avatar)` into your gameplay component. Read the implementation in the imported **21. Scripted Controller** sample for a complete hash-baking and submission example.
4. In your custom runtime system, resolve the hash through `BlobDatabaseSingleton.animations.TryGetValue`. Wait until the singleton and created clip blob exist; do not construct or dispose a package-owned clip blob.
5. Each frame before animation processing, advance normalized time by delta time divided by the clip's positive length. Call `ScriptedAnimator.ResetAnimationState(ref buffer)`, then `ScriptedAnimator.PlayAnimation(ref buffer, clipBlob, normalizedTime)`.
6. The default weight is `1` and default Avatar Mask is absent. Change weight for deliberate blending; submit all intended clips after clearing the buffer once per frame.
7. Confirm that this rig has no `AnimatorControllerLayerComponent` buffer and that the animation submission buffer contains the expected clip and changing time.

**Expected result:** Clip submission controls animation without a baked controller competing for the buffer.

## Verification

- Record the target entity and world used for all checks.
- Confirm each requested parameter exists with the expected controller type and reads back correctly.
- For a state change, read layer state hashes and time on distinct frames; check the target transition condition.
- For events, confirm the relevant buffer exists after rebaking and record the expected event hash/type when its authored time is crossed.
- For scripted playback, require a created clip blob with positive length, a nonempty `AnimationToProcessComponent` buffer, and advancing submitted normalized time.
- Use no new compile errors or unhandled Console exceptions as the final integration check.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Rukhanka.FastAnimatorParameter` | Struct | Stores Rukhanka's parameter name hash. |
| `Rukhanka.AnimatorParametersAspect` | Struct | Checks, reads, and writes parameter values and triggers. |
| `Rukhanka.AnimatorStateQueryAspect` | Struct | Reads layer state and transition information. |
| `Rukhanka.AnimationEventComponent` | Buffer element | Exposes clip event hashes and scalar parameters. |
| `Rukhanka.AnimatorControllerEventComponent` | Buffer element | Exposes controller state lifecycle events. |
| `Rukhanka.ScriptedAnimator.PlayAnimation` | Static method | Adds a clip at normalized time; call each frame. |
| `Rukhanka.Hybrid.AnimationAssetSetAuthoring` | Authoring component | Requests clip and Avatar Mask baking. |

## Common issues

- Parameter write has no effect → Wrong entity, name, or type, or a later system overwrites it → Verify the target and HasParameter result, then inspect update order and competing writers.
- Parameter control breaks after controller editing → Code relies on buffer indices → Use named FastAnimatorParameter values.
- No event buffer → Authoring checkbox remains false → Enable it and rebake; clip events are not GameObject method calls.
- Event appears one frame late → Reader runs before animation processing → Read current-frame events after the animation group.
- Scripted clip freezes → Submission time is not advanced each frame → Advance normalized time and submit every frame.
- Scripted animation is overwritten → A controller still owns the buffer → Use a controller-free rig. Mixed controller mode requires explicit system ownership design; see Boundaries.

## Boundaries

This skill does not implement gameplay state machines, audio playback, or network prediction. Clip event Object parameters are ignored by Rukhanka. Do not assume StateMachineBehaviour callbacks run as GameObject callbacks. Mixed controller/manual state playback can require disabling shared controller systems and affect other rigs; it is outside this basic scripted workflow. Use the package's scripting documentation and inspect the current generic controller-system types before attempting that integration.

