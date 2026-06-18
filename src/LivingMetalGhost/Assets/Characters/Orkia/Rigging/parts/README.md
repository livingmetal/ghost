# Orkia Rig Parts

이 폴더는 오르키아 puppet rig용 파생 PNG 파츠를 두는 위치이다.

아직 실제 런타임 확정 파츠가 아니라 실험 자산을 저장하는 공간이다.

## Export rule

모든 PNG는 전신 논리 캔버스 기준으로 export한다.

```text
canvas: 300 x 598
background: transparent
origin: top-left
```

보이는 부분만 잘라낸 PNG를 넣지 않는다. 전체 캔버스 기준이어야 slot 조합 시 좌표가 흔들리지 않는다.

## Recommended first parts

첫 파츠 분리 목표는 얼굴과 머리 중심이다.

```text
body_base.png
head_base.png
hair_back.png
hair_front.png
ear_left.png
ear_right.png
eye_left_open.png
eye_right_open.png
eye_left_closed.png
eye_right_closed.png
pupil_left.png
pupil_right.png
brow_left.png
brow_right.png
mouth_closed.png
mouth_open_a.png
mouth_open_b.png
mouth_smile.png
```

팔/소매/손은 첫 단계에서 무리하게 분리하지 않는다. 의상 포즈가 필요하면 `../outfits/`에서 포즈별 오버레이로 처리한다.
