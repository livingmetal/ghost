# Orkia Puppet Rig Asset Pipeline

이 문서는 오르키아 puppet rig 자산을 만드는 작업 순서를 정의한다.

핵심은 간단하다.

```text
전신 템플릿을 기준으로 그린다.
전체 캔버스 PNG로 export한다.
런타임은 slot을 겹쳐서 조합한다.
깨지면 기존 CharacterBases PNG로 돌아간다.
```

## Source of truth

| Item | Source |
|---|---|
| 기본 캐릭터 정체성 | `../manifest.json` |
| 현재 승인 fallback 이미지 | `../CharacterBases/approved-*.png` |
| 전신 포즈 좌표 | `rig-manifest.draft.json` |
| 포즈 guide | `body-templates/*.svg` |
| 파츠 export 위치 | `parts/` |
| 의상 export 위치 | `outfits/` |
| 악세사리 export 위치 | `accessories/` |

## Canvas rule

모든 런타임용 PNG는 같은 캔버스를 사용한다.

```text
logical canvas: 300 x 598
background: transparent
origin: top-left
```

작업용 원본은 2배 또는 4배로 그려도 된다.

```text
600 x 1196
1200 x 2392
```

단, export할 때는 같은 비율과 같은 좌표계를 유지한다.

## First asset target

처음부터 전신을 전부 분리하지 않는다.

v0의 목표는 얼굴과 말하기만 분리하는 것이다.

```text
parts/
  body_base.png
  hair_back.png
  hair_side_left.png
  hair_side_right.png
  head_base.png
  ear_left.png
  ear_right.png
  hair_front.png
  eye_left_open.png
  eye_right_open.png
  eye_left_closed.png
  eye_right_closed.png
  pupil_left.png
  pupil_right.png
  brow_left_neutral.png
  brow_right_neutral.png
  brow_left_thinking.png
  brow_right_thinking.png
  brow_left_concerned.png
  brow_right_concerned.png
  mouth_closed.png
  mouth_open_a.png
  mouth_open_b.png
  mouth_smile.png
```

`body_base.png`는 처음에는 옷과 팔이 합쳐진 전신 PNG여도 된다. v0에서는 몸 파츠보다 얼굴 파츠가 중요하다.

## Recommended cut order

### 1. Base body

원본 승인 이미지에서 얼굴과 머리를 제외한 안정 body를 만든다.

포함해도 되는 것:

```text
shirt
neck tie
skirt
stockings
boots
arms
hands
hoodie or no-hoodie outfit
```

처음에는 옷을 무리하게 slot으로 나누지 않는다.

### 2. Head base

얼굴 피부, 귀 접합부, 코, 기본 음영을 포함한다.

제외:

```text
eyes
pupils
brows
mouth
front hair if it covers the eyes
```

### 3. Hair

긴 네추럴 웨이브는 최소 네 레이어로 나눈다.

```text
hair_back
hair_side_left
hair_side_right
hair_front
```

큰 머리 질량은 `hair_back`으로 body 뒤쪽에 둔다. 어깨 바깥으로 흐르는 좌우
웨이브는 `hair_side_left/right`로 분리해 작은 sway를 허용한다. 눈을 덮는
앞머리와 얼굴 옆 짧은 가닥은 `hair_front`로 둔다.

### 4. Eyes

눈은 다음을 분리한다.

```text
eye_left_open
eye_right_open
eye_left_closed
eye_right_closed
pupil_left
pupil_right
```

v0에서 눈동자는 ±3px 이상 움직이지 않는다.

### 5. Brows

상태별로 최소 세 벌을 만든다.

```text
neutral
thinking
concerned
```

눈썹만 바꿔도 감정 표현이 크게 살아난다.

### 6. Mouth

최소 네 벌을 만든다.

```text
closed
open_a
open_b
smile
```

말하기는 `closed -> open_a -> closed -> open_b` 순환으로 충분하다.

## Export naming rule

파일명은 snake_case를 사용한다.

```text
mouth_open_a.png
brow_left_concerned.png
```

금지:

```text
MouthOpenA.png
mouth open a.png
mouth-open-a.png
```

## Layer validation checklist

각 PNG export 후 확인한다.

1. 캔버스가 `300 x 598`인가?
2. 배경이 투명한가?
3. 원점이 다른 파츠와 같은가?
4. 필요 없는 checkerboard가 실제 픽셀로 들어가지 않았는가?
5. 눈/입 파츠 주변에 흰색 halo가 없는가?
6. 같은 파츠의 상태별 위치가 흔들리지 않는가?
7. 전체 조합 시 원본과 크게 어긋나지 않는가?

## Outfit pipeline

의상은 v0 이후에 진행한다.

의상은 세 단계로 나눈다.

### Outfit v1: whole-body outfit

의상 전체를 `body_base.png`에 포함한다.

장점:

```text
빠르다
깨질 확률이 낮다
리깅 구현이 단순하다
```

단점:

```text
옷 교체가 어렵다
팔/소매 애니메이션이 어렵다
```

### Outfit v2: top-level clothes slots

다음 정도만 분리한다.

```text
inner_top
outer_top
neck_item
```

### Outfit v3: arm and sleeve split

나중에 필요할 때만 분리한다.

```text
sleeve_left
sleeve_right
hand_item_left
hand_item_right
```

팔 포즈가 바뀌는 의상은 포즈별 전신 overlay로 처리하는 것이 안전하다.

## Accessory pipeline

처음에 좋은 악세사리:

```text
glasses
hairpin
small_ribbon
brooch
name_badge
small_necklace
```

공통 악세사리는 `common.png`로 시작한다.

```text
accessories/glasses/common.png
```

포즈에 따라 위치가 달라지는 악세사리는 포즈별 파일을 둔다.

```text
accessories/hand_terminal/neutral_stand.png
accessories/hand_terminal/explain_hand.png
```

## AI image generation rule

AI 이미지는 최종 파츠로 바로 믿지 않는다.

좋은 용도:

```text
의상 시안
헤어스타일 시안
작은 악세사리 아이디어
색상 조합
```

나쁜 용도:

```text
동일 캐릭터의 여러 전신 포즈를 좌표 검수 없이 자동 생성
완성된 파츠 PNG 자동 생성
정확한 300x598 좌표계 자동 유지
눈/입/소매 파츠 자동 분리
```

AI 결과는 참고 이미지로 보고, 실제 런타임 파츠는 template 위에서 정리한다.

## Acceptance criteria for first art pass

첫 asset pass는 다음만 만족하면 된다.

1. `forward_clasped` 기준 전신 조합이 master와 비슷하게 보인다.
2. 눈을 깜빡일 수 있다.
3. 입을 열고 닫을 수 있다.
4. thinking/concerned 눈썹이 구분된다.
5. 전체 PNG fallback과 리깅 결과의 크기/위치가 크게 튀지 않는다.
