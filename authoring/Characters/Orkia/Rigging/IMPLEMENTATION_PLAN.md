# Orkia Puppet Rig Implementation Plan

이 문서는 오르키아 puppet rig를 실제 앱 기능으로 끌어올리는 단계별 계획이다.

## Principle

기존 캐릭터 표시를 깨지 않는다.

```text
current stable renderer: modular full-state PNG
new renderer: optional puppet rig
failure behavior: transparent fallback
```

리깅은 기능 추가이지 기존 시스템 교체가 아니다.

## Phase 0: data only

Status: mostly done.

Deliverables:

```text
Rigging/README.md
Rigging/POSE_PLAN.md
Rigging/rig-manifest.draft.json
Rigging/body-templates/*.svg
Rigging/RUNTIME_DESIGN.md
Rigging/ASSET_PIPELINE.md
```

Exit criteria:

1. 기본 `forward_clasped`를 포함한 표준 포즈 4개가 정리되어 있다.
2. slot 목록이 있다.
3. fallback 규칙이 문서화되어 있다.
4. asset export 규칙이 있다.

## Phase 1: manifest loader

Goal: 앱이 rig manifest를 읽을 수 있게 한다.

Suggested files:

```text
src/LivingMetalGhost/Core/Characters/Rigging/PuppetRigManifest.cs
src/LivingMetalGhost/Core/Characters/Rigging/PuppetRigPose.cs
src/LivingMetalGhost/Core/Characters/Rigging/PuppetRigSlot.cs
src/LivingMetalGhost/Core/Characters/Rigging/PuppetRigAssetResolver.cs
```

Tasks:

1. `Rigging/rig-manifest.draft.json` 존재 여부 확인.
2. JSON deserialize.
3. canvas width/height 검증.
4. `state_pose_map` 검증.
5. required slot 검증.
6. 실패 시 null rig profile 반환.

Exit criteria:

```text
오르키아 Rigging 폴더가 없어도 앱이 정상 실행된다.
Rigging 폴더가 있으면 loader가 draft를 읽고 로그에 상태를 남긴다.
```

## Phase 2: asset resolver

Goal: 상태와 pose에 맞는 slot image path를 계산한다.

Input:

```text
character_id
state
speaking frame index
selected outfit id
selected accessory ids
```

Output:

```text
PuppetRigFrame
  pose_id
  slots[] sorted by z
  transforms
  fallback_reason?
```

Tasks:

1. `state_pose_map[state]`로 pose 결정.
2. required slot 경로 결정.
3. optional slot 경로 결정.
4. PNG 존재 여부 확인.
5. 누락 시 fallback reason 반환.

Exit criteria:

```text
idle state에서 body/head/face slot 목록을 만든다.
필수 slot 누락 시 current modular sprite fallback을 요청한다.
```

## Phase 3: WPF RiggedCharacterView

Goal: slot PNG를 WPF Canvas에 올린다.

Suggested files:

```text
src/LivingMetalGhost/UI/Controls/RiggedCharacterView.xaml
src/LivingMetalGhost/UI/Controls/RiggedCharacterView.xaml.cs
```

Tasks:

1. `Canvas` 크기를 rig canvas에 맞춘다.
2. slot별 `Image` control을 만든다.
3. z-order에 따라 children을 정렬한다.
4. image cache를 적용한다.
5. state change 시 slot source만 갱신한다.

Exit criteria:

```text
정적 puppet rig frame 하나가 기존 캐릭터 위치에 표시된다.
framing preset이 기존 full-body canvas 기준으로 동작한다.
```

## Phase 4: minimal animation

Goal: idle/speaking에 생명감을 넣는다.

Animations:

```text
breathing: body group y ±2 px
head sway: head group rotate ±1.5 degrees
blink: eye_open <-> eye_closed
mouth flap: mouth_closed -> mouth_open_a -> mouth_closed -> mouth_open_b
```

Tasks:

1. animation timer를 기존 sprite speaking timer와 충돌하지 않게 분리.
2. speaking일 때 mouth slot만 교체.
3. idle breathing은 window 위치를 움직이지 않고 내부 Canvas transform만 조정.
4. blink는 랜덤 interval을 사용하되 최소/최대 범위를 둔다.

Exit criteria:

```text
idle에서 눈 깜빡임과 호흡이 보인다.
speaking에서 전체 PNG swap 없이 입만 움직인다.
```

## Phase 5: expression rig

Goal: thinking/concerned 상태를 얼굴 파츠로 표현한다.

Tasks:

1. `neutral`, `thinking`, `concerned`, `soft_smile` expression set 정의.
2. brow/mouth/pupil slot 교체.
3. thinking에서 pupil offset 적용.
4. concerned에서 brow pinch와 small dip 적용.

Exit criteria:

```text
thinking/concerned가 full-state PNG 없이 구분된다.
구분이 어렵거나 파츠가 누락되면 fallback한다.
```

## Phase 6: outfit slot support

Goal: 옷을 교체할 수 있는 기반을 둔다.

Initial outfit policy:

```text
outfit v1: body_base whole outfit
outfit v2: inner_top / outer_top / neck_item
outfit v3: sleeves and hand items
```

Tasks:

1. selected outfit id를 profile/settings에 저장.
2. outfit.draft.json loader 작성.
3. pose별 outfit layer resolve.
4. missing outfit layer는 optional로 처리.

Exit criteria:

```text
후디 버전과 후디 없는 버전을 body_base 교체로 바꿀 수 있다.
```

## Phase 7: editor/debug overlay

Goal: 리깅 파츠 위치를 점검할 수 있게 한다.

Debug features:

```text
show canvas bounds
show center line
show anchors
show slot bounding boxes
show current pose/state/expression
show fallback reason
```

Exit criteria:

```text
개발 모드에서 anchor와 slot 상태를 화면에서 확인할 수 있다.
```

## Minimum viable code path

가장 작은 구현 단위는 다음이다.

```text
1. Load rig-manifest.draft.json.
2. Try idle puppet rig.
3. If any required PNG is missing, use existing approved-neutral.png.
4. If present, render slot images in z order.
5. Add mouth flap only after static rendering succeeds.
```

## Out of scope for v0

다음은 v0에서 하지 않는다.

```text
mesh deform
physics hair
cloth simulation
automatic part extraction
automatic image generation pipeline
arms_crossed rig
full outfit marketplace
```

## Risk register

| Risk | Mitigation |
|---|---|
| 파츠 좌표가 흔들림 | 모든 PNG를 full canvas export로 강제 |
| 얼굴이 원본과 달라짐 | 원본 approved-neutral을 기준으로 수동 분리 |
| WPF 성능 저하 | BitmapImage cache, slot source만 교체 |
| 구현 중 기존 표시 깨짐 | rig 실패 시 modular PNG fallback |
| 의상 조합 폭발 | 포즈 4개와 outfit v1부터 제한 |
| 스토리 모드와 충돌 | state 이름은 기존 manifest state를 그대로 사용 |

## Done definition for v0

v0는 다음을 만족하면 완료이다.

1. 기존 Orkia visual behavior가 유지된다.
2. Rigging 폴더가 없어도 앱이 정상 동작한다.
3. Rigging 폴더와 최소 파츠가 있으면 PuppetRig로 idle을 표시한다.
4. speaking에서 입 파츠만 교체한다.
5. 실패 원인이 로그 또는 debug overlay에 남는다.
