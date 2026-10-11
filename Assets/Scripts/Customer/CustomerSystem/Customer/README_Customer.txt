카운터 어택 손님 시스템
=====================

[파일]
Customer/CustomerSpawner.cs   하루 진행(5분) + 손님 생성 + 밸런스 숫자 (전부 Inspector에서 조절)
Customer/Customer.cs          손님 한 명의 행동 (쇼핑 → 줄 → 계산 → 결제 → 퇴장, 도둑은 도망)
Customer/CheckoutCounter.cs   계산대 줄 관리 + 계산대 위 물건 스캔 + 결제
Customer/CheckoutItem.cs      계산대 위 물건 표시 (자동으로 붙음)
Customer/StoreStats.cs        오늘 기록 (만족도, 손님 수, 매출, 별점)
PlacedItem.cs                 기존 파일 교체 (손님이 선반 물건을 찾을 수 있게 추가)

[준비]
1. Window > Package Manager > Unity Registry > "AI Navigation" 설치
2. 빈 오브젝트(NavMesh) > Add Component > NavMesh Surface > Bake
   (선반, 계산대, 벽은 자동으로 피해 감. 물건을 옮기면 다시 Bake)
3. Tag에 "Customer" 추가

[손님 프리팹]
- 모델 + Animator (Float "Speed" 블렌드 트리: 0 Idle / 걷기 속도 Walk / 뛰기 속도 Run, Trigger "Greet"는 선택)
- NavMeshAgent, Capsule Collider, Tag = Customer
- Customer 스크립트 (안 붙여도 자동으로 붙음)
- (선택) 머리 위 화난 표시 오브젝트를 만들어 Customer의 Angry Mark에 연결

[씬에 놓을 것]
- 입구(Entrance), 출구(Exit): 바닥 위 빈 오브젝트. 출구를 비우면 입구로 나감
- 줄 서는 자리 3~4개: 계산대 손님 쪽 바닥에 일렬로. 파란 화살표가 계산대를 보게
- 물건 놓는 자리 2~3개: 계산대 위 손님 쪽
- CheckoutCounter: 계산대 근처 빈 오브젝트에 붙이고 위 자리들 연결
- CustomerSpawner: 빈 오브젝트에 붙이고 손님 프리팹, 입구, 출구 연결

[Player_Interaction의 OnScan에 한 줄 추가]
    if (CheckoutCounter.Instance != null) CheckoutCounter.Instance.TryScan(playerCamera);

[주의]
- 손님은 선반 칸(Pos)에 놓인 물건만 삼. 선반이 비어 있으면 불만만 쌓이고 나감
- 손에 든 스캐너, 총에 콜라이더가 있으면 스캔/총 레이를 막음 → 콜라이더 제거
- 가게를 크게 키웠으면 속도, 간격(Extra Spacing), Item Reach Radius, Scan Range,
  NavMesh 에이전트 크기를 그 비율만큼 키우고, 블렌드 트리 숫자도 걷기/뛰기 속도와 같게
- 테스트할 때는 Thief Chance 0, Normal Patience 크게 해두면 편함
