카운터 어택 편의점 모델 세트
============================

[파일]
Store_Assembled_12x8.obj  바로 쓰는 완성본 (가게 12m x 8m + 바깥 데크 12m x 3m)
StoreBuilder.cs           조각을 칸 수대로 자동 배치해주는 도구

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
2. OBJ 전부 선택 > Inspector Model 탭에서 Scale Factor 1, Generate Colliders 체크 > Apply
   (완성본 가로가 12m가 맞는지 기본 Cube(1m)와 비교)
3. 분홍색으로 보이면: Materials 탭 > Extract Materials 로 재질 꺼내기
   > 꺼낸 재질 전부 선택 > Edit > Rendering > Materials > Convert Selected Built-in Materials to URP
4. Glass 재질: Surface Type을 Transparent, Base Color 알파를 90 정도로
5. 완성본 안의 Floor, Deck 오브젝트에 InstallAvailable 태그 (물건 설치 가능 바닥)

[StoreBuilder로 크기 바꾸기]
1. Hierarchy에 빈 오브젝트(Store) 만들고 StoreBuilder 붙이기
2. 조각 칸 10개에 Project 창의 OBJ를 이름 맞춰 끌어다 넣기
3. Width(가로 칸 수), Depth(세로 칸 수), Door Index(문 칸 번호, 왼쪽부터 0) 정하기
   한 칸 = 4m. 3 x 2 = 12m x 8m, 4 x 3 = 16m x 12m
4. 컴포넌트 이름 오른쪽 점 세 개(⋮) > "편의점 만들기"
   바닥과 데크에 InstallAvailable 태그가 자동으로 붙음
5. 숫자 바꾸고 다시 "편의점 만들기" 누르면 기존 것 지우고 새로 만듦 (Ctrl+Z로 되돌리기 가능)
