# Orkia Body Templates

이 폴더의 SVG 파일은 최종 캐릭터 그림이 아니다.

오르키아 puppet rig용 **전신 포즈 fitting guide**이다. 새 body sprite, outfit overlay, accessory sprite를 그릴 때 투명 guide layer로 사용한다.

## Rules

1. 논리 캔버스는 `300 x 598`을 유지한다.
2. 포즈별 anchor는 `../rig-manifest.draft.json`과 일치해야 한다.
3. 최종 그림을 이 SVG 안에 직접 그리지 않는다. 그림 프로그램에서 guide layer로 불러와 사용한다.
4. 최종 PNG export는 항상 전체 캔버스 기준, 투명 배경으로 한다.
5. 파생 PNG는 `../parts/`, `../outfits/`, `../accessories/` 아래에 둔다.
6. 기존 승인 PNG는 `../../CharacterBases/`에 있으며 직접 수정하지 않는다.

## Template set

| File | Purpose |
|---|---|
| `neutral_stand.svg` | idle, listening, speaking의 기본 전신 포즈 |
| `thinking_tilt.svg` | thinking, skeptical, confused, concerned용 고개 기울임 포즈 |
| `explain_hand.svg` | 설명/가이드용 한 손 제스처 포즈 |

## Anchor convention

SVG는 다음 anchor를 표시한다.

```text
head
neck
torso
left_shoulder
right_shoulder
left_elbow
right_elbow
left_hand
right_hand
hip
```

숫자 좌표의 canonical source는 `../rig-manifest.draft.json`이다.

## Cropping rule

상반신/3/4 화면을 위해 별도 상반신 원본을 만들지 않는다.

```text
source: full-body template
view: full-body / three-quarter / upper-body crop
```

즉, 이 SVG는 항상 전신 기준이다.
