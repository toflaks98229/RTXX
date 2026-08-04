# DCSS 타일 사용 규약

> 출처: https://github.com/crawl/crawl (branch `stone_soup-0.34`)
> 클론 위치: `External/crawl` (Assets 바깥 — `.gitignore` 등재)

## 1. 분할이 필요 없습니다

기존 프로젝트는 `main.png`(307분할) 같은 **합쳐진 시트**를 스프라이트
에디터로 잘라 쓰고 있었습니다. 그런데 crawl 저장소에는 원본이
**개별 PNG 8,351개**로 들어 있습니다.

```
crawl-ref/source/rltiles/
  mon/humanoids/humans/human.png
  player/base/human_m.png
  player/hand1/long_sword_slant.png
  player/hand2/buckler_round.png
  ...
```

합쳐진 시트는 빌드 산출물이고 원본은 낱장입니다. 낱장을 쓰면
경계가 어긋날 일이 없고 피벗도 파일마다 정확합니다.

## 2. 피벗은 반드시 하단 중앙 (0.5, 0)

실측 결과, crawl 타일은 **캐릭터의 발이 타일 하단에 정확히 닿아** 있습니다.

```
mon/humanoids/humans/human.png   불투명 영역 y=1..31  (아래 여백 0px)
mon/humanoids/orcs/orc_warrior   불투명 영역 y=0..31  (아래 여백 0px)
```

따라서 피벗을 `(0.5, 0)`으로 두면 **피벗이 곧 발바닥**이 됩니다.

### 왜 중앙 피벗이 문제였는가

```
32px 타일 / PPU 32 = 1.0 유닛 높이
중앙 피벗(0.5,0.5) -> 피벗 기준 위아래 0.5씩

Unit_Sprite 로컬 y = 0.5
  -> 스프라이트 하단이 정확히 y=0  (여유 0)
  -> 셰이더 상하 진동이 얹히면 하단이 지면 아래로
```

이것이 "유닛이 지면에 반쯤 잠긴다"의 근본 원인이었습니다.
`_moveLength`를 줄이는 것은 증상 완화일 뿐이었습니다.

### 전환 시 반드시 함께 바꿔야 하는 것

```
이전: 피벗 중앙 + Unit_Sprite 로컬 y=0.5  -> 하단 0.0  OK
이후: 피벗 하단 + Unit_Sprite 로컬 y=0.5  -> 하단 0.5  공중에 뜸!
이후: 피벗 하단 + Unit_Sprite 로컬 y=0    -> 하단 0.0  OK
```

## 3. 무기/방패는 오프셋이 필요 없습니다 ★

이것이 가장 중요한 발견입니다.

무기 PNG는 '무기만 잘라낸 이미지'가 **아닙니다**.
**32x32 캔버스에 손 위치를 기준으로 미리 배치된** 이미지입니다.

```
player/base/human_m.png        32x32  몸이   x=5..26  y=2..31
player/hand1/dagger.png        32x32  무기가 x=2..8   y=5..20
player/hand1/long_sword_slant  32x32  무기가 x=0..8   y=0..20
```

즉 몸통과 무기를 **같은 크기, 같은 피벗, 같은 자리**에 겹쳐 그리면
무기가 자동으로 손에 들립니다. 오프셋 데이터를 찾을 필요가 없습니다.

**검증**: `base_human_m` + `weapon_long_sword_slant`를 (0,0)에 겹쳐 합성한
결과, 칼이 정확히 손에 들리고 발이 y=31에 닿았습니다.

### 기존 프리팹과의 차이

예전에는 잘라 낸 무기를 프리팹에서 손으로 맞췄습니다.

```
Unit_Weapon  localPosition = (0.355, 0, 0)   scaleX = -1
Unit_Shield  localPosition = (0.248, 0, 0.097)
```

원본 타일을 쓰면 이 보정이 **전부 불필요**하며, 오히려 어긋나게 만듭니다.
무기/방패의 `localPosition`은 반드시 `(0, 0, 0)`, `scaleX`는 `1`이어야 합니다.

## 4. 가져오는 방법

`RTXX > DCSS 타일 가져오기` 메뉴 또는:

```bash
Unity.exe -batchmode -projectPath . \
  -executeMethod DCSS_Tile_Importer.Import_From_CLI
```

가져올 목록은 `DCSS_Tile_Importer.tilePaths`에 있습니다.
8,351개를 전부 가져오면 임포트가 길어지고 아틀라스가 비대해지므로,
**지금 쓰는 것만** 골라 둡니다.

임포터가 자동으로 적용하는 설정:

| 항목 | 값 | 이유 |
|---|---|---|
| spriteAlignment | 7 (BottomCenter) | 발바닥이 원점 |
| spritePivot | (0.5, 0) | 위와 동일 |
| spritePixelsPerUnit | 32 | 기존 척도 유지 |
| filterMode | Point | 픽셀 아트 보존 |
| textureCompression | Uncompressed | 32px에서 압축은 뭉갬 |
| mipmapEnabled | false | 2D에 불필요 |

## 5. 향후: 전체 추출

지금은 필요한 20개만 가져옵니다. 전체(8,351개)를 쓰려면:

- `tilePaths`를 폴더 단위 스캔으로 바꾸기
- 스프라이트 아틀라스에 폴더째 등록 (드로우콜 유지)
- 32x48 등 **비균일 크기 타일** 주의
  (`dc-mon.txt`의 `### Larger tiles (32x48)` 절 참고)
  하단 중앙 피벗은 크기가 달라도 그대로 성립합니다.

## 6. 타일 정의 파일

`crawl-ref/source/rltiles/dc-*.txt`가 타일과 게임 enum의 대응을 정의합니다.

```
dc-mon.txt     몬스터    (MONS_*)
dc-player.txt  플레이어 장비 (HAND1/HAND2 카테고리)
dc-item.txt    아이템
dc-dngn.txt    지형
```

형식은 `파일명 ENUM_이름`이며, `%sdir`가 이후 항목의 기준 폴더를 바꿉니다.
무기의 손 위치는 이 파일에 없습니다 — PNG 자체에 이미 반영되어 있습니다.
