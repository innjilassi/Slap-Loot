---
name: "rukhanka-animation-system-2-setup-and-overview"
description: "Use this skill whenever the user asks to install, check, or get started with Rukhanka Animation System 2, such as 'set up DOTS animation', 'is Rukhanka installed', or 'where are the animation samples'. Covers dependencies, project settings, sample import, and routing to character workflows. Do NOT use for configuring an individual character (see rukhanka-animation-system-2-animate-character). When in doubt whether an ECS animation setup request could involve Rukhanka Animation System 2, use this skill; Prerequisites shows how to confirm installation."
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
  last-verified: "2026-10-20"
---

# Set Up Rukhanka Animation System 2

Prepare a project for Rukhanka and locate the correct character, scripting, optimization, or diagnostic workflow. Rukhanka bakes Unity animation authoring into ECS data; rendering also requires deformation-compatible materials.

## When to use this skill

- "Set up Rukhanka in this project."
- "Get DOTS animation working" or "show me the samples."
- "Which animation engine should I start with?"

Not for:

- Individual rig setup: [animate-character](../rukhanka-animation-system-2-animate-character/SKILL.md).
- Gameplay animation code: [control-animation](../rukhanka-animation-system-2-control-animation/SKILL.md).

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

## Quick start

1. Resolve `com.rukhanka.animation` in the consuming project's Package Manager. In this source checkout the package is the repository root; in a consuming project its virtual path is `Packages/com.rukhanka.animation`.
2. Run the install check above and inspect `Packages/com.rukhanka.animation/package.json`. Require version `2.9.1` for these instructions; inspect source before adapting them to another version.
3. Confirm an active URP or HDRP pipeline and successfully resolved Entities dependencies.
4. In Package Manager, select Rukhanka and import **Rukhanka Animation Samples** from its Samples tab. Locate the imported files using the import result; do not assume the physical folder name from an older manual.
5. Open the imported `Scenes/1. Basic Animation/BasicAnimation.unity` and enter Play mode after compilation and SubScene baking finish.
6. Confirm at least one baked rig and progressing animation time using the character skill's runtime check. Expected result: the basic sample displays animated characters with no new Console errors.

## Workflows

### Workflow: Verify an installed package

**Goal:** Distinguish installation, compilation, and rendering setup problems.

**Steps:**

1. Run the reflection check in Prerequisites before compiling any code that references Rukhanka.
2. Check the manifest's version and dependencies. Inspect the consuming project's `Packages/manifest.json` to identify local versus registry/package installation.
3. Confirm that the package contains `Rukhanka.Hybrid/Rig/RigDefinitionAuthoring.cs`, `Rukhanka.Runtime/AnimationSystemGroup.cs`, and `Rukhanka.Editor/ShaderGraph/`.
4. Open **Edit > Project Settings**, then select **Rukhanka Animation**. Its provider path is `Project/Rukhanka Animation`.
5. Leave optional rendering and debugging switches at their existing values unless the task requires a change. CPU is the zero/default value of `RigDefinitionAuthoring.AnimationEngine`; choose it for a first functional rig.
6. Report the installed package and dependency versions, active pipeline, supported minimums (Entities 1.4.3+ and Entities Graphics 1.4.16+), and **Edit > Project Settings > Rukhanka Animation** (`Project/Rukhanka Animation`). Distinguish the observed versions from the supported minimums.

**Expected result:** The package compiles, the settings provider exists, and its exact installed version is known.

### Workflow: Import examples and choose a workflow

**Goal:** Find a working example and the matching skill.

**Steps:**

1. Import samples through Package Manager rather than copying selected scene files without their dependencies.
2. Use these skill entry points:
   - [animate-character](../rukhanka-animation-system-2-animate-character/SKILL.md): prepare a model, controller, SubScene, and deformation materials.
   - [control-animation](../rukhanka-animation-system-2-control-animation/SKILL.md): drive parameters, query states, consume events, or submit clips.
   - [configure-ik](../rukhanka-animation-system-2-configure-ik/SKILL.md): configure reaching, aiming, and pose overrides.
   - [root-motion](../rukhanka-animation-system-2-root-motion/SKILL.md): apply animation movement or read it for a custom mover.
   - [optimize-animation](../rukhanka-animation-system-2-optimize-animation/SKILL.md): choose CPU/GPU, configure culling, strip bones, and update bounds.
   - [debug-animation](../rukhanka-animation-system-2-debug-animation/SKILL.md): inspect baked data and record playback.
3. Match the sample to the task: Basic Animation (1), Animator Parameters (3), Root Motion (8), Inverse Kinematics (18), Animation Culling (20), or Scripted Controller (21).

**Expected result:** The imported sample and chosen skill cover the user's next action.

## Verification

For an installation-only request, verify installation, compilation, pipeline, and settings availability without importing samples or entering Play mode. The sample and runtime checks below apply when importing or running examples is requested. If the execution tool returns no output for `Debug.Log`, retrieve the captured Console entries to confirm the reflection check actually logged PASS.

- The reflection check logs PASS.
- `package.json` reports `com.rukhanka.animation`, version `2.9.1`, and the expected dependency.
- The selected pipeline asset is assigned and belongs to URP or HDRP.
- The imported Basic Animation scene resolves its SubScene, controller, clips, and materials.
- In Play mode, a runtime query finds `Rukhanka.RigDefinitionComponent` with a created rig blob; animation time progresses.
- Package import and sample playback produce no compile errors or unhandled exceptions.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Rukhanka.Hybrid.RigDefinitionAuthoring` | Authoring component | Supplies rig configuration for baking. |
| `Rukhanka.Hybrid.RigDefinitionAuthoring.AnimationEngine` | Enum | Selects CPU or GPU animation. |
| `Rukhanka.RukhankaAnimationSystemGroup` | ECS system group | Runs the animation processing pipeline before transforms. |
| `Project/Rukhanka Animation` | SettingsProvider path | Exposes debug and deformation configuration. |

## Common issues

- Install check fails → Package missing or compilation failed → Resolve dependencies and inspect the first Console error; do not treat missing types as an API rename.
- Sample appears static or distorted → Material lacks Rukhanka deformation support → Follow animate-character's material workflow.
- An older instruction says "Rukhanka.Animation" → UI text changed → Use the source-confirmed settings provider and window paths in these skills.
- Sample scripts require additional packages → Imported sample uses an optional integration → Inspect that sample's dependencies; do not install Netcode or physics solely to animate a basic rig.

## Boundaries

These skills target the full Rukhanka Animation System 2 package, not another edition. Installation does not provide full Mecanim feature parity. Legacy animation, Animator IK Pass, and state Foot IK are unsupported; use Rukhanka IK for supported CPU rigs. Network transport, ragdolls, and custom third-party shaders require separate integration work.
