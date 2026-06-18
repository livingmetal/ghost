# 쑝 스프라이트 정규화

이 폴더의 PNG 스프라이트는 상태 전환 시 캔버스 크기와 몸통 위치가 다르면 캐릭터가 덜컥거리며 움직입니다. `normalize_sprites.py`는 `manifest.json`에서 참조하는 PNG들을 같은 투명 캔버스에 올리고, 보이는 영역의 바닥 중앙을 기준으로 정렬합니다.

## 가장 쉬운 실행

```powershell
cd C:\workspace\livingmetal\temporory\ghost\src\LivingMetalGhost\Assets\Characters\ssyong
.\normalize.cmd
```

결과는 `normalized` 폴더에 생성됩니다. 먼저 눈으로 확인하세요.

## 원본 PNG까지 교체

결과가 괜찮으면 아래 명령으로 원본을 덮어씁니다. 덮어쓰기 전 `_backup_before_normalize` 폴더에 백업이 생깁니다.

```powershell
py .\normalize_sprites.py --repair-empty --apply
```

`py`가 안 되면 `python`으로 바꿔 실행하면 됩니다.

```powershell
python .\normalize_sprites.py --repair-empty --apply
```

## 옵션

```powershell
py .\normalize_sprites.py --canvas 642x414 --anchor-y-offset 12
```

- `--canvas 642x414`: 최종 투명 캔버스 크기입니다.
- `--anchor-y-offset 12`: 몸통 바닥 기준점을 캔버스 맨 아래에서 12px 위에 둡니다.
- `--repair-empty`: 비어 있는 참조 PNG를 보수적으로 대체합니다.
- `--apply`: `normalized` 결과물을 원본 PNG에 덮어씁니다.
- `--all-png`: manifest 참조 여부와 무관하게 폴더의 모든 PNG를 처리합니다.

## 주의

현재 방식은 alpha bounding box의 bottom-center를 기준으로 잡습니다. 장식, Zzz, 이펙트가 몸통보다 더 아래나 옆으로 튀어나오면 기준점이 조금 흔들릴 수 있습니다. 이 경우 해당 PNG만 Aseprite, Krita, Photopea 등에서 수동으로 미세 조정하면 됩니다.
