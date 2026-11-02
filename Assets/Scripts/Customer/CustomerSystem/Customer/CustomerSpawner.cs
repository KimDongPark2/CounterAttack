using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

// 영업 준비(30초) → 하루 진행(5분) + 손님 만들기 + 밸런스 숫자 모음
// 숫자는 전부 Inspector에서 바꾸면서 맞추면 됨
public class CustomerSpawner : MonoBehaviour
{
    [Serializable]
    public class Phase
    {
        public string name;
        [Tooltip("이 구간이 시작하는 시각 (초)")] public float startTime;
        [Tooltip("손님 사이 간격 최소 (초)")] public float minInterval;
        [Tooltip("손님 사이 간격 최대 (초)")] public float maxInterval;

        public Phase() { }
        public Phase(string n, float start, float min, float max)
        {
            name = n; startTime = start; minInterval = min; maxInterval = max;
        }
    }

    [Header("연결")]
    [Tooltip("손님 프리팹들 (여러 개 넣으면 랜덤)")]
    public GameObject[] customerPrefabs;
    public Transform entrance;
    [Tooltip("비우면 입구로 나감")]
    public Transform exit;
    [Tooltip("비우면 자동으로 찾음")]
    public CheckoutCounter counter;
    [Tooltip("비우면 자동으로 찾음")]
    public Player_State state;

    [Header("하루")]
    public bool startOnPlay = true;
    [Tooltip("켜면 시계(준비 시간 · 하루 시간)가 멈춤. 손님은 계속 옴 (치트 '시간 정지'가 켜고 끔)")]
    public bool clockStopped = false;
    public float dayLength = 300f;
    [Tooltip("이 시각(초)부터는 손님이 안 들어옴")]
    public float stopSpawnTime = 280f;
    public int startHour = 9;
    public int endHour = 22;
    [Tooltip("게임 속 시계를 보여줄 글자 (선택)")]
    public TMP_Text clockText;
    public Phase[] phases =
    {
        new Phase("오전", 0f, 12f, 15f),
        new Phase("점심 러시", 60f, 6f, 8f),
        new Phase("오후", 120f, 10f, 12f),
        new Phase("퇴근 러시", 210f, 5f, 7f),
        new Phase("마감", 270f, 12f, 15f),
    };
    [Tooltip("하루가 끝나면 실행 (정산 화면 열기 등을 여기에 연결)")]
    public UnityEvent onDayEnd;

    [Header("영업 준비")]
    [Tooltip("셔터가 올라간 뒤 손님이 오기 전까지 물건 채울 시간 (초). 0이면 바로 영업 시작")]
    public float prepTime = 30f;
    [Tooltip("준비 중에 Enter를 누르면 바로 영업 시작")]
    public bool allowSkip = true;
    [Tooltip("영업이 시작될 때 실행 (선택)")]
    public UnityEvent onDayStart;

    [Header("손님")]
    public int maxCustomers = 6;
    [Range(0f, 1f)] public float thiefChance = 0.1f;
    [Range(0f, 1f)] public float hurryChance = 0.25f;
    public int minItems = 1;
    public int maxItems = 2;
    public float normalPatience = 25f;
    public float hurryPatience = 12f;
    public float walkSpeed = 1.5f;
    public float hurrySpeed = 2f;
    public float runSpeed = 3.5f;
    [Tooltip("선반 앞에서 물건 고르는 시간")]
    public float pickTime = 1f;
    [Tooltip("결제 후 인사(Greet)하는 시간. Greet 애니메이션 길이에 맞추기")]
    public float greetTime = 1.2f;

    [Header("만족도 점수")]
    public int fastPoint = 2;
    public int slowPoint = 0;
    public int angryPoint = -2;
    public int missingPoint = -1;
    public int wrongShotPoint = -5;
    public int thiefShotPoint = 3;
    public int thiefEscapePoint = -3;

    public float DayTime { get; private set; }
    public bool DayRunning { get; private set; }
    public string ClockText { get; private set; } = "";
    // 영업 준비 중인지, 남은 준비 시간 (PrepCountdownUI가 읽음)
    public bool Preparing { get; private set; }
    public float PrepTimeLeft { get; private set; }

    private float spawnTimer;

    private void Start()
    {
        if (counter == null) counter = FindAnyObjectByType<CheckoutCounter>();
        if (state == null) state = FindAnyObjectByType<Player_State>();
        if (exit == null) exit = entrance;

        if (customerPrefabs == null || customerPrefabs.Length == 0 || entrance == null || counter == null)
        {
            Debug.LogError("[CustomerSpawner] 손님 프리팹, 입구, 계산대(CheckoutCounter)를 연결하세요.");
            enabled = false;
            return;
        }

        if (startOnPlay) StartCoroutine(StartWhenShutterOpens());
    }

    // 씬 전환 셔터가 내려와 있으면 올라가기 시작할 때 준비 시작 (셔터가 없으면 바로)
    private IEnumerator StartWhenShutterOpens()
    {
        while (SceneShutter.Blocking) yield return null;
        BeginPrep();
    }

    // 영업 준비: 손님은 안 오고 시계도 9시에 멈춰 있음. 시간이 다 되거나 Enter를 누르면 영업 시작
    public void BeginPrep()
    {
        DayRunning = false;
        DayTime = 0f;
        StoreStats.Reset();
        UpdateClock();
        if (prepTime <= 0f) { StartDay(); return; }
        Preparing = true;
        PrepTimeLeft = prepTime;
    }

    // 버튼에서 "바로 영업 시작"으로 쓰고 싶을 때
    public void SkipPrep()
    {
        if (Preparing) PrepTimeLeft = 0f;
    }

    public void StartDay()
    {
        Preparing = false;
        PrepTimeLeft = 0f;
        DayTime = 0f;
        spawnTimer = 2f; // 시작하고 2초 뒤 첫 손님
        DayRunning = true;
        StoreStats.Reset();
        UpdateClock();
        onDayStart?.Invoke();
    }

    private void Update()
    {
        if (Preparing)
        {
            if (!clockStopped) PrepTimeLeft -= Time.deltaTime;
            Keyboard kb = Keyboard.current;
            if (allowSkip && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) PrepTimeLeft = 0f;
            if (PrepTimeLeft <= 0f) StartDay();
            return;
        }

        if (!DayRunning) return;

        if (!clockStopped) DayTime += Time.deltaTime;
        UpdateClock();

        if (DayTime < stopSpawnTime)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                if (Customer.Active.Count < maxCustomers)
                {
                    Spawn();
                    Phase p = CurrentPhase();
                    spawnTimer = p != null ? Random.Range(p.minInterval, p.maxInterval) : 10f;
                }
                else spawnTimer = 1f; // 가게가 꽉 차면 1초 뒤에 다시 확인
            }
        }

        // 시간이 다 되고 남은 손님도 다 나가면 하루 끝
        if (DayTime >= dayLength && Customer.Active.Count == 0) EndDay();
    }

    private Phase CurrentPhase()
    {
        Phase result = null;
        foreach (Phase p in phases)
            if (p != null && DayTime >= p.startTime) result = p;
        return result;
    }

    private void Spawn()
    {
        GameObject prefab = customerPrefabs[Random.Range(0, customerPrefabs.Length)];
        if (prefab == null) return;

        Vector3 pos = entrance.position;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;

        GameObject go = Instantiate(prefab, pos, entrance.rotation);
        Customer c = go.GetComponent<Customer>();
        if (c == null) c = go.AddComponent<Customer>();

        float r = Random.value;
        Customer.CustomerType type =
            r < thiefChance ? Customer.CustomerType.Thief :
            r < thiefChance + hurryChance ? Customer.CustomerType.Hurry :
            Customer.CustomerType.Normal;

        StoreStats.AddVisitor();
        if (state != null) state.countedCustomers++;
        c.Setup(this, counter, type);
    }

    private void EndDay()
    {
        DayRunning = false;
        if (state != null) state.star = StoreStats.Stars;
        StoreStats.SaveBest(StoreStats.ScoreAfterDebt(state != null ? state.debt : 0));   // 인트로 화면 최고 점수
        Debug.Log($"[하루 끝] 손님 {StoreStats.Visitors}명, 계산 {StoreStats.Served}명, 매출 {Player_State.Won(StoreStats.Revenue)}, " +
                  $"화남 {StoreStats.Angry}, 도둑 잡음 {StoreStats.ThievesCaught} / 놓침 {StoreStats.ThievesEscaped}, 별 {StoreStats.Stars}개, " +
                  $"최종 점수 {StoreStats.ScoreAfterDebt(state != null ? state.debt : 0):N0}점 " +
                  $"(매출×{StoreStats.Multiplier} = {StoreStats.FinalScore:N0}, 남은 빚 {(state != null ? state.debt : 0):N0})");
        onDayEnd?.Invoke();
    }

    private void UpdateClock()
    {
        float t = Mathf.Clamp01(DayTime / dayLength);
        float hours = Mathf.Lerp(startHour, endHour, t);
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.FloorToInt((hours - h) * 6f) * 10; // 10분 단위
        ClockText = $"{h:00}:{m:00}";
        if (clockText != null) clockText.text = ClockText;
    }
}
