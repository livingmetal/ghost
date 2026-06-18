# Orkia Asset Guide

오르키아 캐릭터 자산의 기준 문서이다.

이 폴더의 기본 원칙은 다음과 같다.

1. **기존 승인 이미지 보존**
   - `CharacterBases/`의 `approved-*.png`는 현재 앱에서 사용하는 안정 자산이다.
   - 리깅 실험, 파츠 분리, 의상 추가 작업은 원본을 직접 수정하지 않는다.

2. **전신 기준 설계**
   - 마스터 포즈와 의상은 전신 기준으로 설계한다.
   - 앱에서는 전신, 3/4, 상반신 프레이밍을 크롭/줌으로 보여준다.
   - 크롭은 표시 방식이지 별도 원본 기준이 아니다.

3. **Fallback 우선**
   - 리깅이 불완전한 상태는 기존 `CharacterBases/approved-*.png`를 그대로 사용한다.
   - puppet rig는 기존 modular sprite 구조를 대체하기보다 점진적으로 보강한다.

4. **표준 포즈 + 슬롯 방식**
   - 새 의상과 악세사리는 표준 전신 실루엣에 맞춰 제작한다.
   - 옷은 포즈별 오버레이로 만들고, 얼굴/머리/악세사리는 가능한 한 공통 파츠로 재사용한다.

## Current structure

```text
Orkia/
  manifest.json                 # 현재 앱에서 사용하는 캐릭터 매니페스트
  image-prompt.json             # 외부 생성 지시서용 고정/가변 프롬프트 프로필
  story-default.json            # 롤플레잉 시작 템플릿
  CharacterBases/               # 승인된 전체 상태 PNG fallback 자산
  Rigging/                      # 실험용 puppet rig 설계와 파츠 작업 공간
```

## Prompt source of truth

오르키아 프롬프트 메타데이터는 생성 API를 직접 호출하지 않는다. 앱과 외부 작업자가 동일한 기준으로 프롬프트를 만들 수 있도록 `image-prompt.json`에 고정 속성, 참조 자산, 금지 drift, 프레이밍/포즈/mood 문구만 둔다.

우선순위는 다음과 같다.

1. `manifest.json`의 `default_appearance`
2. `image-prompt.json`의 `identity_tokens`
3. `References/`의 기준 이미지
4. `CharacterBases/approved-*.png`의 승인 상태 이미지
5. `Rigging/POSE_PLAN.md`와 `rig-manifest.draft.json`의 포즈/anchor 정보

프롬프트는 반드시 두 층으로 나눈다.

```text
locked identity: 눈색, 귀, 머리 실루엣, 얼굴 비율, 핵심 복장
variable scene: 포즈, 표정, 프레이밍, 조명, 배경, 촬영 거리
```

갑주, 망토, 대형 보석, 메이드풍 장식, 판타지 전투복은 기본 외형 drift로 취급한다. 그런 요소가 필요하면 기본 오르키아가 아니라 별도 의상 variant로 명시한다.

## Rigging folder

`Rigging/`은 아직 런타임 확정 자산이 아니라 설계/실험 공간이다.

```text
Rigging/
  README.md                     # puppet rig 통합 규칙
  POSE_PLAN.md                  # 표준 포즈와 구현 순서
  rig-manifest.draft.json       # 전신 포즈, anchor, slot draft
  body-templates/               # SVG 전신 포즈 가이드
  outfits/                      # 포즈별 의상 오버레이 draft
  parts/                        # 추후 파츠 PNG 출력 위치
  accessories/                  # 추후 악세사리 draft 위치
```

## Golden rule

오르키아는 먼저 **전신 표준 포즈**로 설계하고, 앱 표시는 프레이밍으로 해결한다.

```text
master asset: full-body pose template
runtime view: full-body / three-quarter / upper-body crop
fallback: CharacterBases/approved-*.png
```

이 방식이면 캐릭터 사이즈 조절, 의상 추가, 악세사리 추가, 얼굴 표정 리깅을 같은 좌표계 안에서 관리할 수 있다.
