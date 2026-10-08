카운터 어택 편의점 모델 세트
============================

[파일]
Store_Assembled_24x16.obj  바로 쓰는 완성본 (가게 24m x 16m + 바깥 데크 24m x 3m)

조립 조각 (4m 격자, 기준점은 전부 '가운데 아래')
  Wall_Solid     막힌 벽 (폭 4m, 높이 3.4m)
  Wall_Window    통유리 벽
  Wall_Door      자동문 벽 (문 Door_L, Door_R가 따로 있고 열린 상태)
  Pillar         앞쪽 모서리 기둥 (간판 높이 4.4m)
  Pillar_Short   뒤쪽 모서리 기둥 (지붕 높이 3.6m)
  Fascia         앞쪽 간판 띠 (폭 4m)
  Roof           지붕 + 천장 (4m x 4m)
  Floor          가게 바닥 (4m x 4m)
  Canopy         입구 차양 (폭 4m, 앞으로 3m)
  Deck           차양 아래 바깥 바닥 (4m x 3m)

CounterAttack_Store.mtl, floor_tile.png, deck_paver.png  재질과 텍스처 (OBJ와 같은 폴더에 둘 것)

[Unity로 가져오기]
1. 이 폴더를 통째로 Assets 안에 넣기
2. OBJ 선택 > Inspector Model 탭에서 Scale Factor 1, Generate Colliders 체크 > Apply
   (완성본 가로가 24m가 맞는지 기본 Cube(1m)와 비교. 100배 차이 나면 Scale Factor 조정)
3. 분홍색으로 보이면: Materials 탭 > Extract Materials 로 재질 꺼내기
   > 꺼낸 재질 전부 선택 > Edit > Rendering > Materials > Convert Selected Built-in Materials to URP
4. Glass 재질: Surface Type을 Transparent, Base Color 알파를 90 정도로
5. 완성본 안의 Floor, Deck 오브젝트에 InstallAvailable 태그 (물건 설치 가능 바닥)

[조각으로 직접 조립할 때] 한 칸 = 4m
- 벽: 격자선 위, 칸 가운데에 놓기 (예: 앞벽은 x = 2, 6, 10 ... / z = 0)
  바깥쪽이 -Z 방향. 왼쪽 벽 Rotation Y 90, 오른쪽 벽 -90, 뒷벽 180
- 바닥, 지붕: 칸 가운데 (x = 2, 6 ... / z = 2, 6 ...)
- 기둥: 격자선이 만나는 점 (앞쪽은 Pillar, 나머지는 Pillar_Short)
- 간판 띠, 차양, 데크: 앞벽 조각과 같은 위치, 같은 회전
- Snap 이동: 씬 뷰에서 Ctrl 누르고 끌면 Grid Snapping 단위로 이동 (단위를 4로 설정)
