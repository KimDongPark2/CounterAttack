using System;
using UnityEngine;

// 시점 모드
//   Classic(고정) : 총 · 카메라가 전혀 안 흔들림 (예전 그대로)
//   Dynamic(다이나믹) : FPS 게임처럼 흔들림 · 반동 · 달리기 자세
public enum ViewMode
{
    Classic = 0,
    Dynamic = 1,
}

// 시점 설정을 저장하고 읽는 곳 (PlayerPrefs)
//   "ViewMode"      : 0 = 고정, 1 = 다이나믹 (처음엔 1)
//   "ViewIntensity" : 효과 세기 0 ~ 1 (= 0 ~ 100%, 처음엔 1). 멀미 나는 사람은 줄이면 됨
// 값이 바뀌면 Changed 이벤트로 알려줌 → 게임 중에 바꿔도 바로 적용
public static class ViewModeSettings
{
    public const string ModeKey = "ViewMode";
    public const string IntensityKey = "ViewIntensity";
    public const ViewMode DefaultMode = ViewMode.Dynamic;
    public const float DefaultIntensity = 1f;

    // 모드나 세기가 바뀌면 불림 (설정 UI끼리 서로 맞출 때 씀)
    public static event Action Changed;

    private static bool loaded;
    private static ViewMode mode;
    private static float intensity;

    // 에디터에서 Play를 여러 번 눌러도 처음부터 다시 읽게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        loaded = false;
        Changed = null;
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        mode = PlayerPrefs.GetInt(ModeKey, (int)DefaultMode) == (int)ViewMode.Classic ? ViewMode.Classic : ViewMode.Dynamic;
        intensity = Mathf.Clamp01(PlayerPrefs.GetFloat(IntensityKey, DefaultIntensity));
    }

    // 지금 시점 모드 (바꾸면 바로 저장)
    public static ViewMode Mode
    {
        get { Load(); return mode; }
        set
        {
            Load();
            if (mode == value) return;
            mode = value;
            PlayerPrefs.SetInt(ModeKey, (int)value);
            Changed?.Invoke();
        }
    }

    // 효과 세기 0 ~ 1 (바꾸면 바로 저장)
    public static float Intensity
    {
        get { Load(); return intensity; }
        set
        {
            Load();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(intensity, value)) return;
            intensity = value;
            PlayerPrefs.SetFloat(IntensityKey, value);
            Changed?.Invoke();
        }
    }

    public static bool IsDynamic => Mode == ViewMode.Dynamic;

    // 실제로 쓰는 세기: 다이나믹이면 세기 값, 고정이면 무조건 0 (→ 모든 효과 꺼짐)
    public static float Strength => IsDynamic ? Intensity : 0f;

    // 설정 화면을 닫을 때 디스크에 저장
    public static void Save() => PlayerPrefs.Save();
}
