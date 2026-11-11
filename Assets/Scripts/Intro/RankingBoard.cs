using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인트로 순위표 창 (RankPanel에 붙이기 · 꺼둔 상태로)
// 창이 켜질 때마다 저장된 순위표(RankingStore)를 읽어서 10줄을 채움: 사진 · 학번이름 · 점수
// 기록이 없는 줄은 "-"로 비워둠. Esc나 뒤로 버튼(Close)으로 닫기
// 줄에 마우스를 올리면 그 줄 옆에 사원증(Popup)이 뜸
public class RankingBoard : MonoBehaviour
{
    [Header("줄 (비우면 아래에 있는 RankRowUI를 위에서부터 순서대로 1위 ~ 10위로 씀)")]
    public RankRowUI[] rows;

    [Header("메달 스프라이트 (1위 · 2위 · 3위)")]
    public Sprite[] medalSprites = new Sprite[3];

    [Header("사원증 (선택)")]
    [Tooltip("줄에 마우스를 올리면 뜨는 사원증 (RankBadgePopup)")]
    public RankBadgePopup popup;

    [Header("글자")]
    [Tooltip("{0} = 순위, {1} = 학번이름.  예: \"{0}. {1}\" → 1. 30303 홍길동")]
    public string nameFormat = "{1}";
    [Tooltip("{0} = 점수")]
    public string scoreFormat = "{0:N0}<size=70%>점</size>";
    public string emptyName = "-";
    public string emptyScore = "";
    public Color rankColor = new Color32(0x0A, 0x24, 0x56, 0xFF);
    [Tooltip("4위부터 순위 숫자 색")]
    public Color rankMutedColor = new Color32(0x5B, 0x6B, 0x85, 0xFF);
    [Tooltip("기록 없는 줄 글자 색")]
    public Color emptyColor = new Color32(0x9A, 0xA7, 0xB9, 0xFF);
    [Tooltip("기록 없는 줄 사진 투명도")]
    [Range(0f, 1f)] public float emptyPhotoAlpha = 0.35f;

    [Header("닫기")]
    public bool escToClose = true;

    private readonly List<Texture2D> loaded = new List<Texture2D>();
    private Color nameColor = Color.clear;

    private void OnEnable()
    {
        if (rows == null || rows.Length == 0) rows = GetComponentsInChildren<RankRowUI>(true);
        Refresh();
    }

    private void OnDisable()
    {
        if (popup != null) popup.Hide();
        ReleasePhotos();
    }

    // 줄에 마우스가 들어오고 나갈 때 (RankRowUI가 부름)
    public void Hover(RankRowUI row, bool on)
    {
        bool show = on && row.entry != null;
        if (row.hover != null) row.hover.SetActive(show);
        if (popup == null) return;
        if (show) popup.Show(row);
        else popup.Hide(row);
    }

    private void Update()
    {
        if (escToClose && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    // 뒤로 버튼의 On Click에 연결
    public void Close()
    {
        SoundManager.Play(Sfx.ButtonClick);
        gameObject.SetActive(false);
    }

    // 순위표 다시 읽어서 채우기
    public void Refresh()
    {
        if (popup != null) popup.Hide();
        ReleasePhotos();
        List<RankingStore.Entry> list = RankingStore.Entries;

        for (int i = 0; i < rows.Length; i++)
        {
            RankRowUI row = rows[i];
            if (row == null) continue;
            int rank = i + 1;
            RankingStore.Entry e = i < list.Count ? list[i] : null;
            row.board = this;
            row.entry = e;
            row.rank = rank;
            if (row.hover != null) row.hover.SetActive(false);

            if (!row.remembered)
            {
                row.emptyPhoto = row.photo != null ? row.photo.texture : null;
                row.remembered = true;
            }
            if (nameColor == Color.clear && row.nameText != null) nameColor = row.nameText.color;

            // 순위 · 메달
            if (row.rankText != null)
            {
                row.rankText.text = rank.ToString();
                row.rankText.color = rank <= 3 ? rankColor : rankMutedColor;
            }
            if (row.medal != null)
            {
                bool hasMedal = rank <= 3 && medalSprites != null && rank - 1 < medalSprites.Length && medalSprites[rank - 1] != null;
                row.medal.enabled = hasMedal;
                if (hasMedal) row.medal.sprite = medalSprites[rank - 1];
            }

            // 이름 · 점수
            if (row.nameText != null)
            {
                row.nameText.text = string.Format(nameFormat, rank, e != null ? e.name : emptyName);
                row.nameText.color = e != null ? nameColor : emptyColor;
            }
            if (row.scoreText != null) row.scoreText.text = e != null ? string.Format(scoreFormat, e.score) : emptyScore;
            if (row.highlight != null) row.highlight.SetActive(rank == 1 && e != null);

            // 사진
            if (row.photo != null)
            {
                Texture2D tex = e != null ? RankingStore.LoadPhoto(e) : null;
                if (tex != null) loaded.Add(tex);
                row.photo.texture = tex != null ? tex : row.emptyPhoto;
                Color c = row.photo.color;
                c.a = e != null ? 1f : emptyPhotoAlpha;
                row.photo.color = c;
            }
        }
    }

    // 불러온 사진 메모리에서 지우기
    private void ReleasePhotos()
    {
        foreach (Texture2D t in loaded) if (t != null) Destroy(t);
        loaded.Clear();
    }
}
