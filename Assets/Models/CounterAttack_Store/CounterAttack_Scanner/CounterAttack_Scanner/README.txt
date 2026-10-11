카운터 어택 바코드 스캐너 모델
============================

[파일]
Scanner.obj                 손에 드는 스캐너 (길이 17cm, 높이 19cm, 실제 크기)
ScannerStand.obj            계산대 위에 두는 받침대
CounterAttack_Scanner.mtl   재질 (OBJ와 같은 폴더에 둘 것)

[Scanner 안의 자식 오브젝트]
  Scanner_Body     몸통 + 고무 손잡이 + 앞 범퍼 + 옆 파란 배지
  Scanner_Trigger  방아쇠 (파란색)
  Scanner_Window   빨간 스캔 창
  Scanner_LED      위쪽 초록 불빛

[기준점]
Scanner       손잡이 가운데 (손으로 잡는 곳). 스캔 창이 앞(+Z)을 봄
ScannerStand  받침대 바닥 가운데

[받침대에 꽂기]
Scanner를 ScannerStand의 자식으로 넣고 Position (0, 0.087, 0.002), Rotation (0, 0, 0)

[Unity로 가져오기]
1. 이 폴더를 통째로 Assets 안에 넣기
2. OBJ 둘 다 선택 > Model 탭에서 Scale Factor 1 > Apply
   (스캐너는 Generate Colliders 대신 Box Collider를 직접 붙이는 걸 추천. 손에 들고 다니는 물건이라 가벼운 게 좋음)
3. 분홍색이면: Materials 탭 > Extract Materials
   > 꺼낸 재질 전부 선택 > Edit > Rendering > Materials > Convert Selected Built-in Materials to URP
4. (선택) ScannerWindow, ScannerLED 재질에 Emission 켜면 불이 들어온 것처럼 보임
5. 계산대 모델에 들어 있던 예전 Scanner 오브젝트는 지우고 그 자리에 ScannerStand를 놓기
