# Orkia Outfits

이 폴더는 오르키아 puppet rig용 의상 draft와 포즈별 의상 PNG를 두는 위치이다.

의상은 표준 전신 포즈에 맞춰 제작한다.

## Rule

의상은 상반신 기준으로 따로 만들지 않는다.

```text
source: full-body pose template
export: full-canvas transparent PNG
runtime: crop/framing
```

## Recommended outfit structure

```text
outfits/<outfit-id>/
  outfit.draft.json
  neutral_stand/
    inner_top.png
    outer_top.png
    sleeve_left.png
    sleeve_right.png
    neck_item.png
  thinking_tilt/
    ...
  explain_hand/
    ...
```

포즈마다 모든 layer가 반드시 필요한 것은 아니다. 없는 layer는 기본 body, 다른 공통 layer, 또는 기존 approved full-state PNG fallback으로 처리한다.

## First outfit

현재 첫 draft는 다음 파일이다.

```text
developer_hoodie/outfit.draft.json
```

이 draft는 기본 후디, 셔츠, 넥타이, 소매를 포즈별로 어떻게 나눌지 정의한다.

## Avoid

- 포즈마다 캔버스 크기가 다른 의상 PNG
- 실제 보이는 옷 부분만 잘라낸 PNG
- 몸 실루엣과 맞지 않는 소매/목선
- 처음부터 망토, 긴 코트, 팔짱 전용 복장처럼 실루엣 변경이 큰 의상
