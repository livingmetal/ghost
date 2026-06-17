# Orkia Puppet Rig Pose Plan

This folder is for Orkia's experimental puppet-rig planning assets.

Do not overwrite or edit the approved original character images under `CharacterBases/`.

## Goal

Build a practical 2D cutout puppet rig for Orkia that can replace or supplement the current modular single-image state system.

This is not a Live2D-quality mesh deformation target. The first target is a lightweight WPF-friendly rig with separate head, face, hair, ears, torso, arms, and mouth/eye parts.

## Current visual baseline

The current Orkia manifest uses `visual.mode = modular` with a single `base` layer. Emotional states are selected by swapping full approved PNGs from `CharacterBases/`.

The puppet-rig experiment should preserve that fallback path.

## Pose-first workflow

Do not begin by cutting the source art into arbitrary parts. Begin by deciding which poses the app actually needs.

The pose list determines:

- which parts must be separated,
- which hidden overlaps must be painted or generated,
- where pivots and anchors belong,
- which full-state PNGs remain better as fallback sprites,
- how much motion is worth implementing in WPF.

## Phase 1 required poses

These are the minimum viable rig poses for a desktop assistant.

| Pose id | App state | Visual intent | Required rig motion |
|---|---|---|---|
| `neutral_idle` | idle, listening | 기본 대기 | subtle breathing, blink, tiny head sway |
| `neutral_speaking` | speaking | 일반 답변 | mouth flap A/B, tiny head bob |
| `thinking` | thinking | 계산/고민 | eyes glance down/side, brow down, head tilt |
| `soft_smile` | acknowledging, happy, relieved | 긍정/확인 | mouth smile, softened eyes |
| `concerned` | confused, concerned, error | 문제/불안 | brow pinch, small head dip |
| `strict` | serious, strict, angry | 단호한 설명 | brow down, mouth line, reduced sway |
| `surprised` | surprised, flustered | 놀람 | eyes wider, mouth open, quick head lift |
| `apologetic` | apologetic | 사과/조심 | head down, eyes softened |

## Phase 2 optional poses

These are useful, but not necessary for the first rig.

| Pose id | Use case | Notes |
|---|---|---|
| `blush` | embarrassed, shy | Can remain fallback sprite if face layer is hard to isolate |
| `arms_crossed` | displeased | Full-body pose likely needs separate arm drawings, not just rig transforms |
| `determined` | task start / resolve | Can be expression-only at first |
| `explain` | teaching / architecture explanation | Hand/arm gesture may require custom parts |

## Recommended first implementation target

Start with `neutral_idle`, `neutral_speaking`, `thinking`, and `concerned` only.

These four prove the rig architecture without requiring full redraws.

## Part separation target

Use a conservative part set first.

```text
body_back
hoodie_torso
neck
head_base
ear_left
ear_right
hair_back
hair_side_left
hair_side_right
hair_front
face_base
eye_left_open
eye_right_open
eye_left_closed
eye_right_closed
pupil_left
pupil_right
brow_left
brow_right
mouth_closed
mouth_open_a
mouth_open_b
mouth_smile
arm_left
arm_right
hand_left
hand_right
```

## Pivot guide

| Part | Pivot rule |
|---|---|
| torso | lower chest center |
| neck | base of neck |
| head_base | lower jaw / neck joint |
| ears | ear root attached to head |
| hair_front | upper forehead, parent=head |
| pupils | center of each eye |
| brows | inner brow root or brow center |
| mouth | center of mouth |
| arms | shoulder joint |
| hands | wrist joint |

## Animation scope

First rig motions should be tiny.

| Motion | Range |
|---|---|
| breathing | y ±2 px, scale 0.995..1.005 |
| head idle sway | rotate ±1.5 degrees |
| speaking bob | y -2..0 px |
| thinking tilt | rotate -3 degrees or +3 degrees |
| pupil tracking | x ±3 px, y ±2 px |
| blink | 80 to 140 ms closed frame |
| concerned dip | head y +2 px, brow y +1 px |

## Fallback rule

Any state that cannot be represented cleanly with the puppet rig should keep using the existing `CharacterBases/approved-*.png` images.

The rig renderer should support mixed operation:

```text
preferred: puppet rig state
fallback: modular base image state
```

## Suggested folder layout

```text
Assets/Characters/Orkia/Rigging/
  POSE_PLAN.md
  rig-manifest.draft.json
  parts/
    README.md
```

`parts/` should contain derived experimental PNGs only. Original approved PNGs must stay in `CharacterBases/`.

## Acceptance criteria

The first rig attempt is successful when:

1. Orkia can idle with breathing, blinking, and subtle head motion.
2. Orkia can speak with mouth-flap animation without swapping the whole body image.
3. Orkia can switch between neutral, thinking, and concerned expressions.
4. Existing full-state PNG fallback remains untouched.
5. Removing the `Rigging/` folder restores the previous asset behavior.
