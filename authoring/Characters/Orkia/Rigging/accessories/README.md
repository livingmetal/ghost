# Orkia Accessories

이 폴더는 오르키아 puppet rig용 악세사리 draft와 파생 PNG를 두는 위치이다.

악세사리는 가능한 한 표준 포즈와 공통 좌표계를 따른다.

## Slot types

`rig-manifest.draft.json`의 악세사리 slot은 다음과 같다.

```text
face_accessory
head_accessory
neck_item
hand_item_left
hand_item_right
```

## Easy accessories

처음에 추가하기 좋은 항목이다.

```text
glasses
hairpin
small_ribbon
ear_clips
brooch
name_badge
small_necklace
```

## Hard accessories

처음부터 넣지 않는 편이 좋다.

```text
large_hat
cape
backpack
large_weapon
big_headgear
large_hand_prop
```

이들은 실루엣을 크게 바꾸거나 포즈별 redraw가 필요하다.

## Export rule

모든 PNG는 전신 캔버스 기준으로 export한다.

```text
canvas: 300 x 598
background: transparent
```

공통 악세사리는 하나의 PNG로 시작한다. 포즈에 따라 위치가 달라지는 악세사리는 포즈별 하위 폴더를 둔다.

```text
glasses/common.png
hand_terminal/neutral_stand.png
hand_terminal/explain_hand.png
```
