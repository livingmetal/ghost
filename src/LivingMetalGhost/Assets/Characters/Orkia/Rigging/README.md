# Orkia Puppet Rig Workspace

오르키아의 2D puppet rig 실험 공간이다.

목표는 Live2D급 mesh deform이 아니라, 고스트 앱에 맞는 **전신 기준 cutout rig**를 만드는 것이다.

## Design decision

오르키아 리깅은 다음 원칙으로 통일한다.

```text
full-body master pose
  -> stable anchors
  -> outfit/accessory overlays
  -> face and mouth parts
  -> runtime framing crop
  -> fallback to CharacterBases when needed
```

즉, 기준 자산은 전신이다. 상반신이나 3/4 화면은 별도 그림 기준이 아니라 같은 전신 좌표계를 크롭/줌해서 보여준다.

## Do not edit originals

`../CharacterBases/`의 승인 PNG는 현재 앱의 안정 fallback이다.

파츠 분리, 의상 추가, 악세사리 추가, 표준 포즈 실험은 이 폴더 아래에서만 한다.

## Canonical draft files

| File | Purpose |
|---|---|
| `POSE_PLAN.md` | 표준 포즈, 단계, 구현 우선순위 |
| `rig-manifest.draft.json` | 전신 포즈 좌표, anchor, slot, 상태 매핑 draft |
| `body-templates/*.svg` | 전신 포즈 실루엣 가이드 |
| `outfits/developer_hoodie/outfit.draft.json` | 포즈별 의상 오버레이 구조 예시 |

## Standard pose set

초기 표준 포즈는 세 개만 둔다.

| Pose id | Purpose |
|---|---|
| `neutral_stand` | idle, listening, speaking, soft-smile 계열 기본 포즈 |
| `thinking_tilt` | thinking, skeptical, confused, concerned 계열 진단 포즈 |
| `explain_hand` | curious, explaining, tutorial 계열 설명 포즈 |

포즈를 늘릴 때는 먼저 `body-templates/`에 SVG 실루엣을 추가하고, 그 다음 `rig-manifest.draft.json`에 anchor를 추가한다.

## Layer model

의상과 악세사리는 slot 기반이다.

```text
body_base
skin_overlay
inner_top
outer_top
sleeve_left
sleeve_right
neck_item
head_base
ear_left / ear_right
hair_back / hair_front
eye / pupil / brow / mouth
face_accessory
head_accessory
hand_item_left / hand_item_right
```

의상 PNG는 가능하면 포즈별 폴더에 `300x598` 투명 PNG로 export한다. 좌표가 고정되어야 런타임 조합이 흔들리지 않는다.

## Runtime target

첫 번째 구현 대상은 다음 네 상태다.

```text
idle
speaking
thinking
concerned
```

성공 기준은 다음과 같다.

1. idle에서 전신 기준 breathing, blink, tiny head sway가 동작한다.
2. speaking에서 전체 PNG 교체 없이 mouth flap이 가능하다.
3. thinking/concerned에서 얼굴 파츠와 고개 기울임이 바뀐다.
4. 구현되지 않은 상태는 기존 `CharacterBases/approved-*.png`로 fallback한다.

## Practical workflow

1. `body-templates/neutral_stand.svg`를 그림 프로그램에 guide layer로 올린다.
2. 같은 캔버스 크기에서 body, clothes, face, accessory layer를 그린다.
3. PNG export는 항상 전체 캔버스 기준으로 한다.
4. 파일은 `parts/`, `outfits/`, `accessories/` 아래에 넣는다.
5. `rig-manifest.draft.json` 또는 각 outfit/accessory draft에 slot과 경로를 추가한다.

## Avoid

- 상반신 기준으로 새 원본을 만드는 것.
- 포즈마다 캔버스 크기나 바닥선을 바꾸는 것.
- 승인된 `CharacterBases/` 이미지를 직접 수정하는 것.
- 의상 PNG를 실제 보이는 부분만 잘라서 export하는 것.
- 팔짱, 망토, 큰 모자 같은 실루엣 변경 의상을 첫 단계에 넣는 것.
