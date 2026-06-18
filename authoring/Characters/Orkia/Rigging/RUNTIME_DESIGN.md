# Orkia Puppet Rig Runtime Design

이 문서는 오르키아 puppet rig를 실제 WPF 런타임에 연결하기 위한 설계이다.

목표는 Live2D 같은 mesh deform이 아니다. 목표는 고스트 앱에서 안정적으로 동작하는 **full-body cutout rig renderer**이다.

## Design summary

```text
Character state
  -> pose template
  -> slot asset resolution
  -> transform composition
  -> lightweight animation layer
  -> WPF Canvas/Image render tree
  -> fallback to current modular PNG when missing
```

기존 `manifest.json`의 modular full-state PNG 시스템은 계속 유지한다. puppet rig는 일부 상태부터 점진적으로 대체한다.

## Runtime modes

캐릭터 렌더러는 세 모드를 지원하는 것이 좋다.

| Mode | Purpose |
|---|---|
| `StaticSprite` | 단일 PNG 표시. 가장 단순한 fallback. |
| `SpriteSequence` | 기존 말하기 A/B 같은 전체 PNG frame 교체. |
| `PuppetRig` | slot별 PNG를 조합하고 transform/animation을 적용. |

오르키아는 초기에는 다음 우선순위를 따른다.

```text
try PuppetRig state
  if required parts missing -> use SpriteSequence or StaticSprite from manifest.json
```

## Data sources

| File | Runtime role |
|---|---|
| `../manifest.json` | 현재 안정 캐릭터 매니페스트와 fallback sprite 경로 |
| `rig-manifest.draft.json` | pose, anchor, slot, state mapping draft |
| `parts/*.png` | 얼굴/머리/기본 body 파츠 |
| `outfits/<id>/**/*.png` | 포즈별 의상 overlay |
| `accessories/<id>/**/*.png` | 공통 또는 포즈별 악세사리 |

## Coordinate system

오르키아 rig의 논리 좌표계는 `300 x 598`이다.

```text
origin: top-left
x: right-positive
y: down-positive
unit: logical pixel
```

모든 runtime PNG는 투명 배경의 전체 캔버스 기준으로 export한다. 부분만 잘라낸 PNG는 허용하지 않는다.

## Render tree

WPF에서는 다음 구조가 가장 단순하다.

```text
RiggedCharacterView
  Canvas RootCanvas(width=300, height=598)
    Image body_base
    Image skin_overlay
    Image inner_top
    Image outer_top
    Image sleeve_left
    Image sleeve_right
    Image neck_item
    Image hair_back
    Image head_base
    Image ear_left
    Image ear_right
    Image hair_front
    Image eye_left
    Image eye_right
    Image pupil_left
    Image pupil_right
    Image brow_left
    Image brow_right
    Image mouth
    Image face_accessory
    Image head_accessory
    Image hand_item_left
    Image hand_item_right
```

정렬은 `Canvas.Left = 0`, `Canvas.Top = 0`으로 통일한다. 각 PNG가 전체 캔버스 기준이므로 Image 위치 이동이 필요 없다.

세밀한 움직임은 `RenderTransform`으로 처리한다.

## Slot resolution

상태가 들어오면 다음 순서로 slot 이미지를 찾는다.

```text
state -> pose_id from state_pose_map
pose_id -> outfit pose folder
pose_id -> accessory pose folder or common asset
expression state -> face/mouth/eye assets
```

예시:

```text
state = thinking
pose_id = thinking_tilt
outfit = developer_hoodie/thinking_tilt/*.png
face = expressions/thinking/*.png or parts fallback
```

## Required slots for v0

첫 버전은 전부 분리하지 않는다.

필수:

```text
body_base
head_base
hair_back
hair_front
eye_left
eye_right
pupil_left
pupil_right
brow_left
brow_right
mouth
```

의상은 처음에는 하나의 `body_base` 또는 `outer_top`에 섞여 있어도 된다. 팔/소매/손은 후순위이다.

## Expression model

표정은 상태별로 다음 네 그룹을 우선 구현한다.

| Expression | Used by | Required changes |
|---|---|---|
| `neutral` | idle, listening, speaking | mouth closed/open, blink |
| `thinking` | thinking, skeptical | pupil offset, brow lowered, mouth line |
| `concerned` | confused, concerned, error | brow pinch, mouth small open/line |
| `soft_smile` | acknowledging, happy, relieved | mouth smile, relaxed brow |

초기 구현에서는 얼굴 파츠가 없으면 기존 full-state PNG로 fallback한다.

## Animation layer

애니메이션은 모두 미세해야 한다.

| Animation | Target | Range |
|---|---|---|
| breathing | body group | y ±2 px or scale 0.995..1.005 |
| idle sway | head group | rotate ±1.5 degrees |
| blink | eye slots | closed frame 80..140 ms |
| mouth flap | mouth slot | closed/open_a/open_b frame cycle |
| thinking gaze | pupil slots | x ±3 px, y ±2 px |
| concerned dip | head/brow group | head y +2 px, brow y +1 px |

## Transform groups

부모/자식 관계는 물리 bone이 아니라 transform group으로 처리한다.

```text
body_group:
  body_base
  skin_overlay
  inner_top
  outer_top
  sleeve_left
  sleeve_right
  neck_item

head_group:
  head_base
  ears
  hair_front
  eyes
  pupils
  brows
  mouth
  face_accessory
  head_accessory

hair_back may be either body_group or head_group depending on exported art.
```

처음에는 `head_group`만 회전시키고, 몸은 breathing y 이동만 한다.

## Fallback rules

다음 중 하나라도 실패하면 기존 `manifest.json` modular state를 사용한다.

1. rig manifest load 실패
2. pose mapping 없음
3. required slot 이미지 없음
4. PNG decode 실패
5. slot z-order 충돌로 렌더 트리가 불완전함
6. 상태가 `displeased`, `apologetic`, `determined`처럼 full-pose 특화 sprite인 경우

fallback은 오류가 아니라 정상 동작이다.

## State mapping plan

| App state | Initial renderer | Later renderer |
|---|---|---|
| idle | PuppetRig | PuppetRig |
| speaking | PuppetRig mouth flap | PuppetRig mouth flap |
| listening | StaticSprite fallback | PuppetRig neutral |
| thinking | StaticSprite fallback first | PuppetRig thinking |
| concerned | StaticSprite fallback first | PuppetRig concerned |
| soft-smile | StaticSprite fallback | PuppetRig expression |
| strict | StaticSprite fallback | expression-only if useful |
| displeased | StaticSprite fallback | keep full sprite |
| apologetic | StaticSprite fallback | keep full sprite |
| determined | StaticSprite fallback | keep full sprite |

## Implementation classes

Suggested C# classes:

```text
Core/Characters/Rigging/
  PuppetRigManifest.cs
  PuppetRigPose.cs
  PuppetRigSlot.cs
  PuppetRigStateMap.cs
  PuppetRigAssetResolver.cs
  PuppetRigFrame.cs

UI/Controls/
  RiggedCharacterView.xaml
  RiggedCharacterView.xaml.cs

UI/ViewModels/
  RiggedCharacterViewModel.cs
```

## Rendering algorithm

```text
1. Character state changes.
2. Renderer asks PuppetRigAssetResolver for a frame.
3. Resolver maps state to pose_id.
4. Resolver collects slot image paths by z order.
5. Missing required slot -> return fallback request.
6. View creates or reuses Image controls for slots.
7. View applies group transforms for idle/speaking/thinking.
8. If speaking, mouth slot swaps at speaking frame interval.
```

## Caching

Images must be cached by absolute asset path.

```text
Dictionary<string, BitmapImage>
```

Do not reload PNGs every animation tick. Only change `Image.Source` when slot/frame changes.

## Testing checklist

1. Load rig manifest without crashing.
2. Missing `Rigging/` folder falls back to current Orkia sprite.
3. Missing one optional slot still renders.
4. Missing required slot falls back cleanly.
5. idle breathing does not shift window layout.
6. mouth flap does not reload all images every frame.
7. framing presets still work on the full rig canvas.
8. deleting generated rig parts restores current behavior.

## v0 milestone

v0 is successful when Orkia can do this with rig parts:

```text
idle: breathing + blink + tiny head sway
speaking: mouth flap without full-body PNG swap
thinking: pupil offset + brow change + slight head tilt
concerned: brow/mouth change with fallback if parts are missing
```
