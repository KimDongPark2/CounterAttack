using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using ConvenienceStore.Tablet;
using Random = UnityEngine.Random;

// 효과음 이름 목록
// 숫자는 Inspector에 넣어둔 소리가 밀리지 않게 고정한 값이라 지우거나 바꾸지 말기 (새로 만들 땐 빈 번호에 추가)
public enum Sfx
{
    None = 0,
    // 인트로 · 화면 전환
    MenuMove = 10, MenuChoose = 11, ShutterDrop = 13, ShutterLand = 14,
    // 태블릿 · 상점 (장바구니 담기 · 업그레이드 · 대출도 ButtonClick)
    TabletOpen = 20, TabletClose = 21, ButtonClick = 22, OrderDone = 24,
    // 하루 진행
    PrepTick = 30, DayStart = 31, PhaseChange = 32, DayEnd = 33,
    // 플레이어 · 물건
    ItemPickup = 40, ItemPlace = 41, DeliveryArrive = 44,
    // 계산대
    CheckoutPlace = 50, ScanBeep = 51, Pay = 52,
    // 손님
    CustomerEnter = 60, CustomerAngry = 61, ThiefRun = 62, ThiefEscaped = 63,
    // 권총
    GunDraw = 70, GunShot = 71, BodyHit = 72, ThiefCaught = 73,
    // 권총 재장전 (GunReload)
    // GunReload = 재장전 소리 한 번에 (MagOut · MagIn · SlideRack이 다 비어 있을 때만 씀)
    // GunSlideRelease = 슬라이드 멈치를 눌러 앞으로 "철컥" (비어 있으면 GunSlideRack으로 대신)
    GunEmpty = 74, GunMagOut = 75, GunMagIn = 76, GunSlideRack = 77, GunReload = 78, GunSlideRelease = 79,
}

// 브금 이름 목록
public enum Bgm { None = 0, Intro = 1, Prep = 2, Day = 3, Rush = 4, Closing = 5, Result = 6 }

public enum SoundBus { Master, Bgm, Sfx }

// 소리 전부를 한 곳에서 관리
// - 볼륨: Audio Mixer(MainMixer)가 맡음. 소리는 믹서의 BGM · SFX 그룹으로 나감
//   설정 화면 슬라이더 → SoundManager.SetVolume(SoundBus.Bgm, 0~1) → 믹서 볼륨이 바뀌고 저장됨
// - 효과음: 목록에서 칸을 찾아 소리 파일만 끌어다 넣으면 끝. 비어 있는 칸은 조용히 넘어감
// - 브금: BgmDirector가 상황에 맞게 틀어줌 (바뀔 때 부드럽게 겹치면서 넘어감)
// - 태블릿을 열면 브금이 살짝 작아짐
// Intro 씬에 "SoundManager" 오브젝트로 두면 씬이 바뀌어도 계속 남음 (SceneShutter처럼)
// 다른 스크립트에서:  SoundManager.Play(Sfx.ScanBeep);   SoundManager.PlayAt(Sfx.CustomerEnter, 위치);
public class SoundManager : MonoBehaviour
{
    [Serializable]
    public class SfxEntry
    {
        [Tooltip("자동으로 채워짐")] public string name;
        public Sfx id;
        [Tooltip("여러 개 넣으면 그중 하나를 랜덤으로 (발소리·총소리처럼 자주 나는 소리에 좋음)")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("높낮이를 이 사이에서 랜덤으로 (1 = 원래). 같은 소리가 반복돼도 덜 질림")]
        public Vector2 pitch = Vector2.one;
    }

    [Serializable]
    public class BgmEntry
    {
        [Tooltip("자동으로 채워짐")] public string name;
        public Bgm id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("끄면 한 번만 (결과 화면 징글 같은 것)")]
        public bool loop = true;
    }

    public static SoundManager Instance { get; private set; }

    [Header("믹서 (볼륨은 믹서가 맡음)")]
    [Tooltip("MainMixer. 비우면 아래 볼륨 값을 소리에 직접 곱함")]
    public AudioMixer mixer;
    [Tooltip("비우면 믹서에서 \"BGM\" 그룹을 자동으로 찾음")]
    public AudioMixerGroup bgmGroup;
    [Tooltip("비우면 믹서에서 \"SFX\" 그룹을 자동으로 찾음")]
    public AudioMixerGroup sfxGroup;
    [Tooltip("믹서에서 Expose한 이름 (Exposed Parameters에 적은 이름과 똑같이)")]
    public string masterParam = "MasterVolume";
    public string bgmParam = "BgmVolume";
    public string sfxParam = "SfxVolume";

    [Header("볼륨 0 ~ 1 (처음 값 · 설정 화면에서 바꾸면 저장됨)")]
    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float bgm = 0.8f;
    [Range(0f, 1f)] public float sfx = 1f;

    [Header("브금")]
    [Tooltip("태블릿을 열면 브금을 이만큼으로 줄임 (1이면 안 줄임)")]
    [Range(0f, 1f)] public float tabletDuck = 0.5f;
    [Tooltip("준비 · 러시 · 마감 브금 칸이 비어 있으면 영업(Day) 브금을 대신 틀기")]
    public bool fallbackToDay = true;
    public BgmEntry[] bgmList;

    [Header("3D 소리 (손님 · 배달처럼 위치가 있는 소리)")]
    [Tooltip("이 거리 안에서는 제일 크게 (매장이 5배 크기라 넉넉하게)")]
    public float minDistance = 5f;
    [Tooltip("이 거리부터는 거의 안 들림")]
    public float maxDistance = 60f;
    [Tooltip("동시에 날 수 있는 효과음 수")]
    public int voices = 12;

    [Header("확인용")]
    [Tooltip("켜면 소리를 낼 때마다 Console에 이름이 찍힘 (소리가 안 날 때 원인 찾기용 · 다 되면 끄기)")]
    public bool logEveryPlay = false;

    [Header("효과음 (칸에 소리 파일만 넣으면 됨)")]
    public SfxEntry[] sfxList;

    // ---------- 다른 스크립트에서 부르는 것들 (SoundManager가 없으면 아무 일도 안 함) ----------
    public static void Play(Sfx id) { if (Instance != null) Instance.PlayVoice(id, null); }
    // 그 칸에 소리 파일이 들어 있는지 (비어 있으면 false)
    public static bool Has(Sfx id) => Instance != null && Instance.sfxMap.TryGetValue(id, out SfxEntry e)
                                      && e.clips != null && e.clips.Length > 0 && e.clips[0] != null;
    public static void PlayAt(Sfx id, Vector3 position) { if (Instance != null) Instance.PlayVoice(id, position); }
    public static void PlayBgm(Bgm id, float fade = 1f) { if (Instance != null) Instance.SwitchBgm(id, fade); }
    public static void StopBgm(float fade = 1f) { if (Instance != null) Instance.SwitchBgm(Bgm.None, fade); }
    public static Bgm CurrentBgm => Instance != null ? Instance.currentBgm : Bgm.None;

    public static float GetVolume(SoundBus bus)
    {
        if (Instance == null) return 1f;
        return bus == SoundBus.Master ? Instance.master : bus == SoundBus.Bgm ? Instance.bgm : Instance.sfx;
    }

    // 설정 화면 슬라이더에서 부르기 (0 ~ 1, 저장됨)
    public static void SetVolume(SoundBus bus, float value)
    {
        if (Instance == null) return;
        value = Mathf.Clamp01(value);
        if (bus == SoundBus.Master) Instance.master = value;
        else if (bus == SoundBus.Bgm) Instance.bgm = value;
        else Instance.sfx = value;
        PlayerPrefs.SetFloat("Vol_" + bus, value);
        Instance.ApplyMixer();
    }

    // 0 ~ 1 → 믹서 dB (-80 = 무음, 0 = 원래 크기)
    public static float ToDecibel(float value) => value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

    // ---------- 내부 ----------
    private class BgmChannel
    {
        public AudioSource source;
        public BgmEntry entry;
        public float fade, target, speed;
    }

    private readonly Dictionary<Sfx, SfxEntry> sfxMap = new Dictionary<Sfx, SfxEntry>();
    private readonly Dictionary<Bgm, BgmEntry> bgmMap = new Dictionary<Bgm, BgmEntry>();
    private readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
    private readonly BgmChannel[] channels = new BgmChannel[2];
    private AudioSource[] pool;
    private int nextVoice;
    private int activeChannel;
    private Bgm currentBgm = Bgm.None;
    private float duck = 1f, duckTarget = 1f;
    private bool duplicate;
    private bool mixerWarned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        // 씬을 다시 불러오면 하나 더 생기니까 원래 있던 것만 남기기
        if (Instance != null && Instance != this)
        {
            duplicate = true;
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        foreach (SoundBus bus in Enum.GetValues(typeof(SoundBus)))
        {
            string key = "Vol_" + bus;
            if (!PlayerPrefs.HasKey(key)) continue;
            float v = PlayerPrefs.GetFloat(key);
            if (bus == SoundBus.Master) master = v; else if (bus == SoundBus.Bgm) bgm = v; else sfx = v;
        }

        if (mixer != null)
        {
            if (bgmGroup == null) bgmGroup = FindGroup("BGM");
            if (sfxGroup == null) sfxGroup = FindGroup("SFX");
        }

        BuildMaps();

        for (int i = 0; i < 2; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.outputAudioMixerGroup = bgmGroup;
            channels[i] = new BgmChannel { source = s };
        }

        pool = new AudioSource[Mathf.Max(1, voices)];
        for (int i = 0; i < pool.Length; i++)
        {
            GameObject go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.outputAudioMixerGroup = sfxGroup;
            s.rolloffMode = AudioRolloffMode.Linear;
            pool[i] = s;
        }
    }

    // 믹서 볼륨은 Awake에서 바꾸면 안 먹혀서 Start에서
    private void Start()
    {
        if (!duplicate) ApplyMixer();
    }

    private AudioMixerGroup FindGroup(string groupName)
    {
        AudioMixerGroup[] found = mixer.FindMatchingGroups(groupName);
        return found != null && found.Length > 0 ? found[0] : null;
    }

    private void ApplyMixer()
    {
        if (mixer == null) return;
        bool ok = mixer.SetFloat(masterParam, ToDecibel(master));
        ok &= mixer.SetFloat(bgmParam, ToDecibel(bgm));
        ok &= mixer.SetFloat(sfxParam, ToDecibel(sfx));
        if (!ok && !mixerWarned)
        {
            mixerWarned = true;
            Debug.LogWarning($"[SoundManager] 믹서에서 '{masterParam}' · '{bgmParam}' · '{sfxParam}' 중에 Expose 안 된 게 있어요. " +
                             "그룹 Volume 우클릭 → Expose 하고 Exposed Parameters에서 이름을 똑같이 바꿔주세요.", this);
        }
    }

    // 믹서가 있으면 볼륨은 믹서가 맡으니까 여기선 1
    private float BgmGain => mixer != null ? 1f : bgm * master;
    private float SfxGain => mixer != null ? 1f : sfx * master;

    private void OnApplicationQuit() => PlayerPrefs.Save();

    private void OnEnable()
    {
        TabletController.OnTabletToggled += HandleTablet;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        TabletController.OnTabletToggled -= HandleTablet;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (!duplicate && Instance == this) Instance = null;
    }

    // 씬이 바뀔 때마다: 소리를 듣는 귀(Audio Listener)가 있는지 확인
    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        if (duplicate || Instance != this) return;
        if (FindAnyObjectByType<AudioListener>() == null)
            Debug.LogWarning($"[SoundManager] '{scene.name}' 씬에 Audio Listener가 없어서 소리가 안 들려요. Player 카메라에 Audio Listener를 붙여주세요.", this);
    }

    // 비어 있는 칸을 불렀을 때 (에디터에서만, 칸마다 한 번)
    private readonly HashSet<Sfx> warnedEmpty = new HashSet<Sfx>();
    private void NoteEmpty(Sfx id)
    {
        if (!Application.isEditor || !warnedEmpty.Add(id)) return;
        Debug.Log($"[SoundManager] {id} 칸이 비어 있어서 소리가 안 나요 ({Describe(id)})", this);
    }

    private void HandleTablet(bool open)
    {
        PlayVoice(open ? Sfx.TabletOpen : Sfx.TabletClose, null);
        duckTarget = open ? tabletDuck : 1f;
    }

    private void BuildMaps()
    {
        sfxMap.Clear();
        if (sfxList != null)
            foreach (SfxEntry e in sfxList)
                if (e != null && !sfxMap.ContainsKey(e.id)) sfxMap.Add(e.id, e);
        bgmMap.Clear();
        if (bgmList != null)
            foreach (BgmEntry e in bgmList)
                if (e != null && !bgmMap.ContainsKey(e.id)) bgmMap.Add(e.id, e);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;   // 일시정지 중에도 브금은 계속
        duck = Mathf.MoveTowards(duck, duckTarget, dt * 2f);

        foreach (BgmChannel c in channels)
        {
            if (c == null || c.source == null) continue;
            c.fade = Mathf.MoveTowards(c.fade, c.target, dt * c.speed);
            float entryVol = c.entry != null ? c.entry.volume : 1f;
            c.source.volume = entryVol * c.fade * duck * BgmGain;
            if (c.fade <= 0f && c.target <= 0f && c.source.isPlaying) c.source.Stop();
        }
    }

    private void PlayVoice(Sfx id, Vector3? position)
    {
        if (id == Sfx.None || pool == null) return;
        if (!sfxMap.TryGetValue(id, out SfxEntry e) || e.clips == null || e.clips.Length == 0) { NoteEmpty(id); return; }
        AudioClip clip = e.clips[Random.Range(0, e.clips.Length)];
        if (clip == null) { NoteEmpty(id); return; }

        // 같은 소리가 한순간에 여러 번 겹쳐서 크게 터지지 않게
        if (lastPlayed.TryGetValue(id, out float last) && Time.unscaledTime - last < 0.03f) return;
        lastPlayed[id] = Time.unscaledTime;

        if (logEveryPlay) Debug.Log($"[SoundManager] ▶ {id} ({clip.name}){(position.HasValue ? " · 3D" : "")}", this);

        AudioSource s = NextVoice();
        s.transform.position = position ?? transform.position;
        s.spatialBlend = position.HasValue ? 1f : 0f;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        float lo = Mathf.Min(e.pitch.x, e.pitch.y), hi = Mathf.Max(e.pitch.x, e.pitch.y);
        s.pitch = hi <= 0f ? 1f : Random.Range(Mathf.Max(0.1f, lo), hi);
        s.volume = e.volume * SfxGain;
        s.clip = clip;
        s.Play();
    }

    // 놀고 있는 스피커를 먼저 쓰고, 다 바쁘면 돌아가며 씀
    private AudioSource NextVoice()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            AudioSource s = pool[(nextVoice + i) % pool.Length];
            if (!s.isPlaying)
            {
                nextVoice = (nextVoice + i + 1) % pool.Length;
                return s;
            }
        }
        AudioSource busy = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Length;
        return busy;
    }

    private BgmEntry Resolve(Bgm id)
    {
        if (id == Bgm.None) return null;
        if (bgmMap.TryGetValue(id, out BgmEntry e) && e.clip != null) return e;
        if (fallbackToDay && (id == Bgm.Prep || id == Bgm.Rush || id == Bgm.Closing)
            && bgmMap.TryGetValue(Bgm.Day, out BgmEntry day) && day.clip != null) return day;
        return null;
    }

    private void SwitchBgm(Bgm id, float fadeTime)
    {
        if (id == currentBgm) return;
        currentBgm = id;
        float speed = 1f / Mathf.Max(0.01f, fadeTime);

        BgmEntry next = Resolve(id);
        BgmChannel active = channels[activeChannel];

        // 같은 곡이면 끊지 않고 그대로 (예: 러시 브금 칸이 비어서 영업 브금을 계속 트는 경우)
        if (next != null && active.entry == next && active.source.isPlaying)
        {
            active.target = 1f;
            active.speed = speed;
            return;
        }

        active.target = 0f;
        active.speed = speed;
        if (next == null) return;

        activeChannel = 1 - activeChannel;
        BgmChannel c = channels[activeChannel];
        c.entry = next;
        c.source.clip = next.clip;
        c.source.loop = next.loop;
        c.fade = 0f;
        c.target = 1f;
        c.speed = speed;
        c.source.Play();
    }

    // ---------- Inspector 목록 자동 채우기 ----------
    private void Reset() => FillLists();
    private void OnValidate()
    {
        FillLists();
        if (Application.isPlaying && Instance == this)
        {
            BuildMaps();
            ApplyMixer();   // 플레이 중에 Inspector 볼륨을 움직이면 바로 들리게
        }
    }

    private void FillLists()
    {
        List<SfxEntry> s = new List<SfxEntry>();
        foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
        {
            if (id == Sfx.None) continue;
            SfxEntry e = null;
            if (sfxList != null)
                foreach (SfxEntry x in sfxList)
                    if (x != null && x.id == id && (e == null || (x.clips != null && x.clips.Length > 0))) e = x;
            if (e == null)
            {
                bool varied = id == Sfx.ItemPickup || id == Sfx.ItemPlace || id == Sfx.DeliveryArrive
                              || id == Sfx.CheckoutPlace || id == Sfx.GunShot || id == Sfx.BodyHit || id == Sfx.CustomerAngry;
                e = new SfxEntry { id = id, volume = 1f, pitch = varied ? new Vector2(0.94f, 1.06f) : Vector2.one };
            }
            bool empty = e.clips == null || e.clips.Length == 0 || e.clips[0] == null;
            e.name = $"{id} · {Describe(id)}{(empty ? "  (비어 있음)" : "")}";
            s.Add(e);
        }
        sfxList = s.ToArray();

        List<BgmEntry> b = new List<BgmEntry>();
        foreach (Bgm id in Enum.GetValues(typeof(Bgm)))
        {
            if (id == Bgm.None) continue;
            BgmEntry e = null;
            if (bgmList != null)
                foreach (BgmEntry x in bgmList)
                    if (x != null && x.id == id && (e == null || x.clip != null)) e = x;
            if (e == null) e = new BgmEntry { id = id, volume = 0.6f, loop = id != Bgm.Result };
            e.name = $"{id} · {Describe(id)}{(e.clip == null ? "  (비어 있음)" : "")}";
            b.Add(e);
        }
        bgmList = b.ToArray();
    }

    public static string Describe(Sfx id)
    {
        switch (id)
        {
            case Sfx.MenuMove: return "인트로 메뉴 줄 옮길 때";
            case Sfx.MenuChoose: return "인트로 메뉴 고를 때";
            case Sfx.ShutterDrop: return "셔터 내려올 때";
            case Sfx.ShutterLand: return "셔터 바닥에 닿을 때";
            case Sfx.TabletOpen: return "태블릿 열 때";
            case Sfx.TabletClose: return "태블릿 닫을 때";
            case Sfx.ButtonClick: return "버튼 (장바구니 담기 · 업그레이드 · 대출 · 탭 · 뒤로)";
            case Sfx.OrderDone: return "주문(결제) 성공";
            case Sfx.PrepTick: return "준비 시간 마지막 5초 똑딱";
            case Sfx.DayStart: return "영업 시작";
            case Sfx.PhaseChange: return "단계 배너 (점심 러시 등)";
            case Sfx.DayEnd: return "하루 끝";
            case Sfx.ItemPickup: return "물건 줍기";
            case Sfx.ItemPlace: return "선반에 물건 놓기 (3D)";
            case Sfx.DeliveryArrive: return "배달 물건 떨어짐 (3D)";
            case Sfx.CheckoutPlace: return "손님이 계산대에 물건 올림";
            case Sfx.ScanBeep: return "바코드 찍기";
            case Sfx.Pay: return "손님 결제 완료";
            case Sfx.CustomerEnter: return "손님 들어옴 (입구 · 3D)";
            case Sfx.CustomerAngry: return "손님 화나서 나감 (3D)";
            case Sfx.ThiefRun: return "도둑이 도망치기 시작 (3D)";
            case Sfx.ThiefEscaped: return "도둑 놓침";
            case Sfx.GunDraw: return "권총 꺼내기";
            case Sfx.GunShot: return "총 쏘기";
            case Sfx.BodyHit: return "사람 맞음 (3D)";
            case Sfx.ThiefCaught: return "도둑 잡음";
            case Sfx.GunEmpty: return "총알 없음 (딸깍)";
            case Sfx.GunMagOut: return "탄창 빼기";
            case Sfx.GunMagIn: return "탄창 끼우기";
            case Sfx.GunSlideRack: return "슬라이드 당기기 (철컥)";
            case Sfx.GunReload: return "재장전 (통째로 · 탄창 · 슬라이드 소리가 다 비었을 때)";
            case Sfx.GunSlideRelease: return "슬라이드 멈치 풀기 (철컥 · 비면 SlideRack)";
        }
        return "";
    }

    public static string Describe(Bgm id)
    {
        switch (id)
        {
            case Bgm.Intro: return "인트로 화면";
            case Bgm.Prep: return "영업 준비 30초 (비면 Day)";
            case Bgm.Day: return "평소 영업";
            case Bgm.Rush: return "점심 · 퇴근 러시 (비면 Day)";
            case Bgm.Closing: return "마감 (비면 Day)";
            case Bgm.Result: return "하루 끝 (한 번만)";
        }
        return "";
    }
}