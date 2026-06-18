# Orkia Puppet Rig Pose Plan

이 문서는 오르키아 puppet rig의 포즈 기준을 정리한다.

핵심 원칙은 간단하다.

```text
전신 표준 포즈를 먼저 만든다.
그 전신 좌표계에 맞춰 옷과 악세사리 스프라이트를 그린다.
앱에서는 같은 전신 자산을 full-body / three-quarter / upper-body로 크롭해서 보여준다.
```

`CharacterBases/`의 기존 승인 이미지는 fallback으로 유지한다. 이 폴더의 작업은 실험용이며 원본 승인 PNG를 직접 수정하지 않는다.

## Goal

고스트 앱에 맞는 가벼운 2D cutout puppet rig를 만든다.

목표는 Live2D급 mesh deform이 아니다. 첫 목표는 WPF에서 다루기 쉬운 전신 기준 레이어 조합이다.

```text
full-body pose template
  -> anchor coordinates
  -> body/clothes/accessory slots
  -> face/mouth/eye parts
  -> runtime crop/framing
  -> approved PNG fallback
```

## Current baseline

현재 오르키아 `manifest.json`은 `visual.mode = modular`이고, 사실상 `base` 한 레이어를 상태별 전체 PNG로 교체한다.

puppet rig는 이 구조를 즉시 대체하지 않는다. 안정 상태는 그대로 두고, 일부 상태부터 점진적으로 puppet rig로 옮긴다.

## Pose-first workflow

바로 이미지를 자르지 않는다.

먼저 앱에서 필요한 표준 포즈를 정한다. 포즈가 정해져야 다음 항목이 결정된다.

- 어떤 파츠를 나눌지
- 어떤 옷 레이어가 필요한지
- 어떤 가려진 부분을 보정해야 하는지
- pivot과 anchor를 어디에 둘지
- 어떤 상태는 기존 전체 PNG fallback으로 남길지
- 크롭 시 얼굴과 손이 어디에 보여야 하는지

## Standard full-body pose set

초기 표준 포즈는 세 개로 제한한다.

| Pose id | App states | Intent | Notes |
|---|---|---|---|
| `neutral_stand` | idle, listening, speaking, soft-smile, happy | 기본 대기/일반 응답 | 첫 번째 의상 fitting 기준 |
| `thinking_tilt` | thinking, skeptical, confused, concerned | 계산/진단/고민 | 고개 기울임과 눈동자 이동 중심 |
| `explain_hand` | curious, explaining, tutorial | 설명/가이드 | 한 손 제스처가 필요한 경우 |

## Later pose candidates

처음부터 만들지 않는다. 필요성이 확인되면 추가한다.

| Pose id | Use case | Risk |
|---|---|---|
| `arms_crossed` | displeased, strict | 팔과 소매를 새로 그려야 해서 비용이 큼 |
| `surprised_lift` | surprised, flustered | 얼굴 파츠만으로 먼저 대체 가능 |
| `apologetic_bow` | apologetic | 고개/상체 anchor가 크게 바뀜 |
| `working_tablet` | working, tool use | 손 소품과 팔 포즈가 필요함 |

## First implementation states

첫 런타임 구현은 네 상태만 목표로 한다.

```text
idle
speaking
thinking
concerned
```

이 네 상태가 되면 구조 검증은 충분하다.

## Full-body canvas rule

논리 캔버스는 현재 오르키아 visual 기준과 맞춘다.

```text
logical canvas: 300 x 598
master art: same aspect ratio, higher resolution allowed
export: transparent full-canvas PNG
```

작업용 원본은 2배 또는 4배 해상도로 그릴 수 있다. 단, 런타임용 export는 항상 같은 비율과 같은 좌표계를 유지한다.

금지한다.

```text
상반신만 기준으로 그린 원본
포즈마다 다른 캔버스 크기
보이는 부분만 잘라낸 PNG export
포즈마다 바닥선이 흔들리는 전신
```

## Framing rule

표시는 크롭/줌으로 해결한다.

| Framing | Purpose |
|---|---|
| full-body | 전체 캐릭터와 의상 확인 |
| three-quarter | 일반 대화에서 얼굴과 상체를 더 크게 보여줌 |
| upper-body | 표정과 입 움직임 강조 |

프레이밍은 별도 그림 기준이 아니라 같은 전신 좌표계의 카메라 설정이다.

## Slot target

초기 slot은 `rig-manifest.draft.json`에 맞춘다.

```text
body_base
skin_overlay
inner_top
outer_top
sleeve_left
sleeve_right
neck_item
head_base
ear_left
ear_right
hair_back
hair_front
eye_left
eye_right
pupil_left
pupil_right
brow_left
brow_right
mouth
face_accessory
head_accessory
hand_item_left
hand_item_right
```

## Pivot and anchor guide

| Part | Pivot / anchor rule |
|---|---|
| body_base | hip 또는 torso anchor 기준 |
| torso / clothes | torso anchor 기준 |
| neck | neck anchor 기준 |
| head_base | neck와 head 사이, 턱 아래 pivot |
| ears | head_base에 parent, 귀 뿌리 기준 |
| hair_front | head_base에 parent, 이마 위 기준 |
| pupils | 각 eye slot 중심 기준 |
| brows | brow 중앙 또는 안쪽 눈썹 기준 |
| mouth | 입 중앙 기준 |
| sleeves | shoulder/elbow/hand anchor를 참고하되 첫 단계는 포즈별 고정 PNG 권장 |
| hand items | hand anchor 기준 |

## Animation scope

첫 rig motion은 작게 잡는다.

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

리깅이 깨끗하게 표현되지 않는 상태는 기존 전체 PNG를 쓴다.

```text
preferred: puppet rig state
fallback: modular base image state from CharacterBases/approved-*.png
```

특히 `arms_crossed`, 큰 외투, 망토, 대형 모자, 복잡한 손 소품은 첫 단계에서 무리하게 리깅하지 않는다.

## Acceptance criteria

첫 rig 시도는 다음을 만족하면 성공이다.

1. `neutral_stand`에서 breathing, blink, subtle head motion이 가능하다.
2. `speaking`에서 전체 PNG 교체 없이 mouth flap이 가능하다.
3. `thinking_tilt`에서 눈동자, 눈썹, 머리 기울임이 바뀐다.
4. `concerned`를 thinking 계열에서 파생 표현할 수 있다.
5. 미구현 상태는 기존 `CharacterBases/approved-*.png` fallback으로 표시된다.
6. `Rigging/` 폴더를 제거해도 기존 오르키아 표시가 깨지지 않는다.
