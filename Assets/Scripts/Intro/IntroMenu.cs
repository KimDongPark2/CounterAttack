using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// 인트로 영수증 메뉴
// - ↑ ↓ (또는 W S)로 줄 옮기기, Space / Enter로 실행, 마우스도 됨
// - 게임 시작: 셔터 내리고 Game 씬 불러오기 / 나가기: 게임 끄기 / 준비 중: 안내 글자 잠깐 띄우기
// - 최고 점수 표시 (하루 끝날 때 저장된 값)
// 메뉴 줄들을 담은 오브젝트(Menu)에 붙이면 됨
public class IntroMenu : MonoBehaviour
{
    [Tooltip("게임 시작을 누르면 불러올 씬 이름 (Build Settings에 들어가 있어야 함)")]
    public string gameSceneName = "Game";

    [Header("최고 점수 (선택)")]
    public TMP_Text bestScoreText;
    public string bestScoreFormat = "{0:N0}점";
    public string noScoreText = "-";

    [Header("준비 중 안내 (선택)")]
    public TMP_Text toastText;
    public string comingSoonMessage = "아직 준비 중이에요!";
    public float toastTime = 1.5f;

    [Header("색")]
    public Color textSelected = Color.white;
    public Color textNormal = new Color32(0x0A, 0x24, 0x56, 0xFF);
    public Color dotsSelected = new Color(1f, 1f, 1f, 0.6f);
    public Color dotsNormal = new Color32(0x9A, 0xA7, 0xB9, 0xFF);

    [Header("움직임")]
    [Tooltip("줄을 옮길 때 살짝 튀는 정도")]
    public float punchScale = 0.04f;
    public float punchTime = 0.15f;


    private readonly List<IntroMenuRow> rows = new List<IntroMenuRow>();
    private int current = -1;
    private float punchTimer = 99f;
    private float toastTimer = 99f;
    private bool loading;

    private void Start()
    {
        // 인트로에서는 마우스가 보여야 함 (게임 씬에서 잠갔던 것 풀기)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        foreach (IntroMenuRow r in GetComponentsInChildren<IntroMenuRow>(true))
        {
            r.menu = this;
            r.index = rows.Count;
            rows.Add(r);
        }
        if (rows.Count == 0) Debug.LogWarning("[IntroMenu] 자식에 IntroMenuRow가 없어요.", this);

        if (bestScoreText != null)
        {
            int best = StoreStats.BestScore;
            bestScoreText.text = best > 0 ? string.Format(bestScoreFormat, best) : noScoreText;
        }
        if (toastText != null) toastText.alpha = 0f;

        Select(0, false);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && !loading && !SceneShutter.Busy)   // 처음 셔터가 올라가는 동안엔 입력 안 받음
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Select(current - 1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select(current + 1);
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Choose();
        }

        // 선택한 줄 살짝 튀기
        if (current >= 0 && current < rows.Count)
        {
            Transform t = rows[current].transform;
            if (punchTimer < punchTime)
            {
                punchTimer += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(punchTimer / punchTime);
                float s = 1f + punchScale * Mathf.Sin(k * Mathf.PI);
                t.localScale = new Vector3(s, s, 1f);
            }
            else t.localScale = Vector3.one;
        }

        // 안내 글자 사라지기
        if (toastText != null && toastTimer < toastTime)
        {
            toastTimer += Time.unscaledDeltaTime;
            float left = toastTime - toastTimer;
            toastText.alpha = Mathf.Clamp01(left / 0.3f);
        }
    }

    public void Select(int index) => Select(index, true);

    private void Select(int index, bool sound)
    {
        if (rows.Count == 0) return;
        index = (index % rows.Count + rows.Count) % rows.Count;   // 맨 위에서 위로 가면 맨 아래로
        if (index == current) return;

        if (current >= 0 && current < rows.Count) rows[current].transform.localScale = Vector3.one;
        current = index;
        for (int i = 0; i < rows.Count; i++)
            rows[i].Paint(i == current, textSelected, textNormal, dotsSelected, dotsNormal);

        punchTimer = 0f;
        if (sound) SoundManager.Play(Sfx.MenuMove);
    }

    public void Choose()
    {
        if (loading || SceneShutter.Busy || current < 0 || current >= rows.Count) return;
        IntroMenuRow row = rows[current];
        SoundManager.Play(Sfx.MenuChoose);   // 소리 파일은 SoundManager에
        row.onChoose?.Invoke();

        switch (row.action)
        {
            case IntroMenuRow.RowAction.StartGame:
                loading = true;
                SceneShutter.Load(gameSceneName);   // 셔터 내리고 → 씬 바꾸고 → 셔터 올리기 (셔터가 없으면 바로 넘어감)
                break;

            case IntroMenuRow.RowAction.Quit:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;

            case IntroMenuRow.RowAction.ComingSoon:
                if (toastText != null)
                {
                    toastText.text = comingSoonMessage;
                    toastText.alpha = 1f;
                    toastTimer = 0f;
                }
                break;
        }
    }
}