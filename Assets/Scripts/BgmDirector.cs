using UnityEngine;
using UnityEngine.SceneManagement;

// 하루 진행에 맞춰 브금을 바꿈 (Game 씬 아무 오브젝트에 하나 붙이기)
//   영업 준비 → SoundManager 브금 칸 Prep
//   러시 (단계 이름에 "러시")      → Rush
//   평소 영업 · 마감 · 하루 끝     → Day · Closing · Result 칸 (비어 있으면 조용)
// 셔터가 내려와 있는 동안엔 기다렸다가, 셔터가 올라가기 시작하면 틂
// Game 씬을 나가면 (타이틀 · 다시 하기) 브금을 끔
// 소리 파일은 SoundManager의 브금 칸에 넣기
// BgmDirector가 없는 씬(Intro)은 알아서 Intro 브금을 틂 → Intro 씬엔 안 붙여도 됨
public class BgmDirector : MonoBehaviour
{
    [Tooltip("켜면 하루 진행에 맞춰 브금을 자동으로 바꿈 (Game 씬)")]
    public bool followDay = true;
    [Tooltip("Follow Day를 끄면 이 브금만 틂")]
    public Bgm sceneBgm = Bgm.None;

    [Tooltip("비우면 자동으로 찾음")]
    public CustomerSpawner spawner;
    [Tooltip("단계 이름에 이 글자가 있으면 러시 브금")]
    public string rushWord = "러시";
    [Tooltip("단계 이름에 이 글자가 있으면 마감 브금")]
    public string closingWord = "마감";

    [Tooltip("브금이 바뀔 때 겹치며 넘어가는 시간 (초)")]
    public float fadeTime = 1.2f;

    private void Start()
    {
        if (followDay && spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
    }

    private void Update()
    {
        if (SceneShutter.Blocking) return;   // 셔터가 화면을 덮고 있으면 기다림
        if (VolumeSlider.PreviewingBgm) return;  // 설정에서 브금 미리듣기 중이면 안 바꿈

        Bgm want = Decide();
        if (want != SoundManager.CurrentBgm) SoundManager.PlayBgm(want, fadeTime);
    }

    private void OnDestroy()
    {
        // 씬을 나가면 브금 끄기 (다음 씬이 이미 인트로 브금을 틀었으면 그대로)
        if (SoundManager.CurrentBgm != Bgm.Intro) SoundManager.StopBgm(fadeTime);
    }

    // ---------- BgmDirector가 없는 씬 (Intro) ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // 처음 켠 씬
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<BgmDirector>() != null) return;     // 직접 붙인 게 있으면 그걸 따름
        if (FindAnyObjectByType<CustomerSpawner>() != null) return; // 게임 씬은 BgmDirector가 맡음
        SoundManager.PlayBgm(Bgm.Intro, 1f);
    }

    private Bgm Decide()
    {
        if (!followDay || spawner == null) return sceneBgm;

        if (spawner.Preparing) return Bgm.Prep;
        if (spawner.DayRunning)
        {
            string phase = spawner.PhaseName;
            if (!string.IsNullOrEmpty(rushWord) && phase.Contains(rushWord)) return Bgm.Rush;
            if (!string.IsNullOrEmpty(closingWord) && phase.Contains(closingWord)) return Bgm.Closing;
            return Bgm.Day;
        }
        if (spawner.DayTime >= spawner.dayLength) return Bgm.Result;   // 하루 끝
        return SoundManager.CurrentBgm;                                // 그 밖엔 그대로
    }
}
