# 편의점 태블릿 + 주문 시스템 스크립트

Unity 프로젝트의 `Assets/Scripts/` 아래에 폴더째 넣으면 됩니다.

## 필요한 것

- **Input System 패키지** (Window ▸ Package Manager ▸ Unity Registry ▸ Input System)
- **TextMeshPro** (Window ▸ TextMeshPro ▸ Import TMP Essential Resources)
- Project Settings ▸ Player ▸ **Active Input Handling** = `Input System Package` 또는 `Both`

## 파일

| 파일 | 붙이는 곳 | 역할 |
|---|---|---|
| `Tablet/TabletController.cs` | TabletRig (카메라 자식) | Tab 토글, 위치 애니메이션, 커서 제어 |
| `Tablet/TabletScreenFitter.cs` | TabletModel | Screen 매터리얼 서브메시에 Canvas를 정확히 맞춤 (에디터 도구) |
| `Tablet/TabletAppSwitcher.cs` | ScreenCanvas | 홈 ↔ 주문 앱 패널 전환 |
| `Ordering/ProductData.cs` | (ScriptableObject 에셋) | 상품 1종의 정보 |
| `Ordering/PlayerWallet.cs` | GameSystems | 소지금 |
| `Ordering/OrderManager.cs` | GameSystems | 장바구니 · 결제 · 배송 |
| `Ordering/ProductSlotUI.cs` | ProductSlot 프리팹 | 주문 목록 한 줄 |
| `Ordering/OrderScreenUI.cs` | OrderPanel | 주문 화면 전체 |

## 씬 구조

```
Player                              ← 이동 / 시점 회전 스크립트
 └ Main Camera
    └ TabletRig                     ← TabletController
       ├ TabletModel                ← 태블릿 3D 모델 (Collider 제거)
       │   └ ScreenCanvas           ← Canvas (World Space, Event Camera = Main Camera)
       │       ├ HomePanel
       │       └ OrderPanel         ← OrderScreenUI
       ├ HiddenPoint                ← 빈 오브젝트 (화면 아래)
       └ ShownPoint                 ← 빈 오브젝트 (눈앞)

GameSystems                         ← PlayerWallet + OrderManager
EventSystem                         ← Input System UI Input Module
```

## 위치 잡는 요령

TabletController 컴포넌트 헤더를 우클릭하면 **현재 위치를 ShownPoint 로 저장** /
**현재 위치를 HiddenPoint 로 저장** 메뉴가 나옵니다. Game 뷰를 보면서 태블릿을
원하는 자리에 둔 다음 저장하면 됩니다.

## 화면이 베젤을 덮을 때

Canvas는 3D 메시가 아니라 평평한 판이라, 모델의 액정 면적과 아무 관계 없이 자기 크기대로
그려집니다. 눈대중으로 맞추면 거의 항상 베젤 위로 삐져나옵니다.

`TabletScreenFitter`를 TabletModel에 붙이고 Screen 매터리얼과 ScreenCanvas를 연결한 뒤,
컴포넌트 우클릭 → **화면에 맞추기** 를 누르면 액정 서브메시의 실제 크기를 읽어
위치·회전·sizeDelta·Scale을 한 번에 계산합니다. `inset`을 0.001~0.003 주면 베젤 안쪽으로
살짝 들어갑니다.

메시의 **Read/Write Enabled**가 꺼져 있으면 동작하지 않습니다
(모델 에셋 ▸ Model 탭 ▸ Read/Write Enabled 체크 ▸ Apply).

추가로 Canvas 루트 패널에 **RectMask2D**를 붙이면 스크롤뷰나 애니메이션 요소가
화면 밖으로 새는 것까지 막힙니다.

## Canvas 설정 값 예시

- Render Mode: **World Space**, Event Camera: **Main Camera**
- Width × Height: `1000 × 640`
- Scale: `0.0002` (실제 화면 가로가 0.2m일 때 → 0.2 ÷ 1000)
- Local Position Z: `-0.001` (모델 표면에서 살짝 띄우기)
- Canvas Scaler ▸ Dynamic Pixels Per Unit: `2` ~ `4`

## 자주 겪는 문제

- **태블릿이 잘려 보임** → 카메라 Near Clip Plane 을 `0.01` 로. 그래도 벽에 파고들면
  태블릿 전용 레이어 + Overlay 카메라(Depth 1, Clear Flags: Depth only)로 위에 그리기.
- **클릭이 안 됨** → Canvas의 Event Camera 미지정 / EventSystem 없음 /
  Graphic Raycaster 꺼짐 / 커서가 아직 잠김.
- **글자가 흐림** → Canvas 해상도 ↑ + Scale ↓, TMP 사용, Dynamic Pixels Per Unit ↑.
- **태블릿 켜도 플레이어가 움직임** → `disableWhileOpen` 배열에 이동 스크립트도 추가.
