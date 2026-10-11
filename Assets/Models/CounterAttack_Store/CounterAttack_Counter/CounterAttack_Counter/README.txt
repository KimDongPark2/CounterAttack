카운터 어택 계산대 모델
======================

[파일]
Counter.obj                 계산대 세트 (가로 2.5m, 앞뒤 2.6m, 진열장 높이 2.3m)
CounterAttack_Counter.mtl   재질 (OBJ와 같은 폴더에 둘 것)

[안에 든 오브젝트]  Unity에서 Counter 아래 자식으로 따로 나옴. 필요 없는 건 지우거나 옮기면 됨
  Counter            몸통 + 나무 상판 (ㄱ자 모양, 높이 1m)
  FrontRack          손님 쪽 앞 진열대 (껌, 사탕 자리)
  FrontRack_Items    앞 진열대 상품
  POS                직원 화면 + 위쪽 손님 화면
  CashDrawer         돈통 (직원 쪽 상판 아래)
  CardReader         카드 단말기 (손님 쪽)
  Scanner            바코드 스캐너
  ReceiptPrinter     영수증 프린터
  BackShelf          뒤쪽 서랍장 + 진열장
  BackShelf_Items    뒤 진열장 상품

[방향]
기준점은 계산대 앞부분 가운데 바닥.
파란 띠가 있는 쪽 = 손님 쪽, 진열장 쪽 = 직원 쪽.
진열장 뒷면을 가게 벽에 붙여서 놓으면 됨.

[Unity로 가져오기]  (편의점 모델이랑 같은 방법)
1. 이 폴더를 통째로 Assets 안에 넣기 (편의점 폴더와 따로 두는 게 재질 이름이 안 꼬임)
2. Counter.obj 선택 > Model 탭에서 Scale Factor 1, Generate Colliders 체크 > Apply
3. 분홍색이면: Materials 탭 > Extract Materials
   > 꺼낸 재질 전부 선택 > Edit > Rendering > Materials > Convert Selected Built-in Materials to URP
4. (선택) Screen 재질에서 Emission 켜고 파란색 넣으면 화면이 켜진 것처럼 보임
