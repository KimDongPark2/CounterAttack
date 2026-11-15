using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// 인트로 "플레이 방법" 창 (HelpPanel에 붙이기 · 꺼둔 상태로)
// 왼쪽 목차를 누르면 오른쪽에 그 단계 영상이 반복 재생되고, 아래에 제목 · 설명이 나옴
//  - Steps에 단계를 넣는 만큼 목차가 자동으로 생김 (Tab Template을 복사해서 씀)
//  - 영상은 소리 없이 반복 재생. 창을 닫으면 멈춤
//  - ↑ ↓ 키로 단계 바꾸기, Esc나 뒤로 버튼(Close)으로 닫기
public class HowToPlayPanel : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        [Tooltip("목차 · 제목에 나올 이름 (예: 물건 주문)")]
        public string title;
        [TextArea(2, 4)] public string description;
        [Tooltip("이 단계 영상 (mp4를 프로젝트에 넣으면 생기는 Video Clip)")]
        public VideoClip clip;
    }

    [Header("단계 (위에서부터 1번)")]
    public Step[] steps;

    [Header("연결")]
    [Tooltip("영상이 나올 RawImage")]
    public RawImage screen;
    [Tooltip("목차 한 칸. 이걸 복사해서 단계 수만큼 만듦 (Tabs 안에 하나만 만들어두기)")]
    public HowToPlayTab tabTemplate;
    public TMP_Text titleText;
    public TMP_Text descText;
    [Tooltip("영상을 불러오는 동안 · 영상이 없을 때 보일 것 (선택)")]
    public GameObject loading;

    [Header("설정")]
    [Tooltip("영상 그림 크기 (가로 × 세로)")]
    public Vector2Int videoSize = new Vector2Int(1280, 720);
    [Tooltip("켜면 영상 소리를 끔")]
    public bool mute = true;
    public bool escToClose = true;

    private VideoPlayer player;
    private RenderTexture target;
    private HowToPlayTab[] tabs;
    private int current = -1;

    private void Awake()
    {
        player = GetComponent<VideoPlayer>();
        if (player == null) player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.audioOutputMode = mute ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
        player.prepareCompleted += OnPrepared;
        BuildTabs();
    }

    private void OnDestroy()
    {
        if (player != null) player.prepareCompleted -= OnPrepared;
    }

    private void OnEnable()
    {
        if (target == null)
        {
            target = new RenderTexture(Mathf.Max(16, videoSize.x), Mathf.Max(16, videoSize.y), 0);
            target.Create();
        }
        player.targetTexture = target;
        if (screen != null) screen.texture = target;
        current = -1;
        Select(0);
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.Stop();
            player.targetTexture = null;
        }
        if (screen != null) screen.texture = null;
        if (target != null)
        {
            target.Release();
            Destroy(target);
            target = null;
        }
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (escToClose && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (kb.downArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Move(1);
        if (kb.upArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Move(-1);
    }

    // 목차 만들기: 템플릿을 단계 수만큼 복사
    private void BuildTabs()
    {
        int n = steps != null ? steps.Length : 0;
        tabs = new HowToPlayTab[n];
        if (tabTemplate == null) return;
        for (int i = 0; i < n; i++)
        {
            HowToPlayTab tab = i == 0 ? tabTemplate : Instantiate(tabTemplate, tabTemplate.transform.parent);
            tab.gameObject.SetActive(true);
            tab.Setup(this, i, steps[i].title);
            tabs[i] = tab;
        }
        if (n == 0) tabTemplate.gameObject.SetActive(false);
    }

    private void Move(int dir)
    {
        if (steps == null || steps.Length == 0) return;
        int next = (current + dir + steps.Length) % steps.Length;
        SoundManager.Play(Sfx.ButtonClick);
        Select(next);
    }

    // 목차 칸이 부름
    public void Select(int index)
    {
        if (steps == null || steps.Length == 0) return;
        index = Mathf.Clamp(index, 0, steps.Length - 1);
        if (index == current) return;
        current = index;
        Step step = steps[index];

        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i] != null) tabs[i].SetSelected(i == index);
        if (titleText != null) titleText.text = step.title;
        if (descText != null) descText.text = step.description;

        // 영상 바꾸기
        player.Stop();
        if (screen != null) screen.enabled = false;   // 새 영상 첫 장면이 나올 때까지 가림
        if (loading != null) loading.SetActive(true);
        if (step.clip == null) return;
        player.clip = step.clip;
        player.Prepare();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        if (!isActiveAndEnabled) return;
        vp.Play();
        if (screen != null) screen.enabled = true;
        if (loading != null) loading.SetActive(false);
    }

    // 뒤로 버튼의 On Click에 연결
    public void Close()
    {
        SoundManager.Play(Sfx.ButtonClick);
        gameObject.SetActive(false);
    }
}
