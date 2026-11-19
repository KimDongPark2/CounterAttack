using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 관리자 설정 창 (화면은 코드가 알아서 만듦 → 씬에서 따로 꾸밀 필요 없음)
// - 탭: 하루 / 손님 / 손님 간격 / 점수 / 계산 · 상점 / 플레이어 / 비밀번호
// - 칸에 숫자를 적고 "저장"을 누르면 PlayerPrefs에 저장 → 다음 게임부터 적용
// - 항목 목록은 AdminSettings.cs 에 있음
// 보통은 AdminPasswordPanel이 AdminSettingsPanel.OpenNew()로 열어줌
// 직접 쓰고 싶으면 빈 오브젝트에 붙이고 font에 한글 글꼴을 넣은 뒤 Open() 부르기
// (화면은 이 오브젝트가 아니라 따로 만든 "AdminSettingsPanel UI" 오브젝트에 그려짐 → 다른 UI에 붙여도 안 섞임)
// 직접 꾸민 창을 쓰고 싶으면 이 스크립트 대신 줄마다 AdminSettingField를 붙이면 됨
public class AdminSettingsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { IsOpen = false; instance = null; }

    [Tooltip("한글이 나오는 TMP 글꼴. 비우면 씬에 있는 글자에서 가져옴")]
    public TMP_FontAsset font;
    [Tooltip("다른 창보다 위에 그리기")]
    public int sortingOrder = 500;

    [Header("색")]
    public Color windowColor = new Color32(0xF7, 0xF5, 0xEF, 0xFF);
    public Color navy = new Color32(0x0A, 0x24, 0x56, 0xFF);
    public Color gray = new Color32(0x80, 0x90, 0xA8, 0xFF);
    public Color changedColor = new Color32(0xD9, 0x48, 0x0F, 0xFF);
    public Color rowColorA = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    public Color rowColorB = new Color32(0xEE, 0xEB, 0xE2, 0xFF);

    private const string PasswordTab = "비밀번호";

    private static AdminSettingsPanel instance;

    private bool built;
    private GameObject ui;      // 코드로 만든 화면 (이 오브젝트와 따로)
    private RectTransform content;
    private ScrollRect scroll;
    private TMP_Text messageText;
    private readonly List<(string tab, Button button, TMP_Text text)> tabButtons = new List<(string, Button, TMP_Text)>();
    private readonly Dictionary<AdminSettings.Def, float> pending = new Dictionary<AdminSettings.Def, float>();
    private string currentTab;
    private string newPassword = "", newPassword2 = "";
    private bool resetPassword;

    // 창이 없으면 새로 만들어서 열기
    public static AdminSettingsPanel OpenNew(TMP_FontAsset font)
    {
        if (instance == null)
        {
            var go = new GameObject("AdminSettingsPanel (자동)");
            instance = go.AddComponent<AdminSettingsPanel>();
            instance.font = font;
        }
        else if (font != null && instance.font == null) instance.font = font;
        instance.Open();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (ui != null) Destroy(ui);
        IsOpen = false;
    }

    private void OnDisable() => Close();

    public void Open()
    {
        if (!built) Build();

        // 저장된 값으로 채우기 (저장 안 하고 닫았던 건 버림)
        pending.Clear();
        foreach (AdminSettings.Def d in AdminSettings.Defs) pending[d] = AdminSettings.Get(d);
        newPassword = newPassword2 = "";
        resetPassword = false;
        SetMessage("", navy);

        ui.SetActive(true);
        IsOpen = true;
        ShowTab(currentTab ?? AdminSettings.Tabs[0]);
    }

    public void Close()
    {
        if (ui != null) ui.SetActive(false);
        IsOpen = false;
    }

    private void Update()
    {
        if (!IsOpen) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
    }

    // ---------- 버튼 ----------
    private void SaveAndClose()
    {
        // 비밀번호 바꾸기 (두 칸이 같아야 함)
        if (newPassword.Length > 0 || newPassword2.Length > 0)
        {
            if (newPassword != newPassword2)
            {
                ShowTab(PasswordTab);
                SetMessage("새 비밀번호 두 칸이 서로 달라요", changedColor);
                return;
            }
            AdminSettings.SetPassword(newPassword);
        }
        else if (resetPassword) AdminSettings.SetPassword(null);

        foreach (var pair in pending) AdminSettings.Set(pair.Key, pair.Value);
        AdminSettings.Save();
        AdminSettings.ApplyToScene();   // 게임 씬에서 열었다면 바로 반영
        SoundManager.Play(Sfx.MenuChoose);
        Close();
    }

    private void ResetTab()
    {
        if (currentTab == PasswordTab)
        {
            newPassword = newPassword2 = "";
            resetPassword = true;
            ShowTab(PasswordTab);
            SetMessage("저장하면 기본 비밀번호로 돌아가요", navy);
            return;
        }
        foreach (AdminSettings.Def d in AdminSettings.Defs)
            if (d.tab == currentTab) pending[d] = AdminSettings.DefaultOf(d);
        ShowTab(currentTab);
        SetMessage($"'{currentTab}' 탭을 기본값으로 바꿨어요 (저장해야 적용)", navy);
    }

    private void ResetAll()
    {
        foreach (AdminSettings.Def d in AdminSettings.Defs) pending[d] = AdminSettings.DefaultOf(d);
        ShowTab(currentTab);
        SetMessage("전부 기본값으로 바꿨어요 (저장해야 적용)", navy);
    }

    // ---------- 탭 내용 ----------
    private void ShowTab(string tab)
    {
        currentTab = tab;
        foreach (var t in tabButtons)
        {
            bool on = t.tab == tab;
            t.button.image.color = on ? navy : rowColorB;
            t.text.color = on ? Color.white : navy;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform old = content.GetChild(i);
            old.SetParent(null, false);   // 바로 목록에서 빼고 지우기 (Destroy는 프레임 끝에 지워짐)
            Destroy(old.gameObject);
        }

        if (tab == PasswordTab) BuildPasswordRows();
        else
        {
            int n = 0;
            foreach (AdminSettings.Def d in AdminSettings.Defs)
                if (d.tab == tab) BuildRow(d, n++);
        }

        scroll.verticalNormalizedPosition = 1f;
    }

    private void BuildRow(AdminSettings.Def d, int index)
    {
        RectTransform row = Row(index, d.tip != null ? 78 : 62);

        TMP_Text label = MakeText(row, "", 26, navy, TextAlignmentOptions.MidlineLeft);
        Place(label.rectTransform, 0, 1, 28, -470);

        TMP_Text def = MakeText(row, "기본 " + AdminSettings.Format(d, AdminSettings.DefaultOf(d)) + Unit(d), 20, gray, TextAlignmentOptions.MidlineRight);
        Place(def.rectTransform, 1, 1, -460, -270);

        void Paint()
        {
            bool changed = !Mathf.Approximately(pending[d], AdminSettings.DefaultOf(d));
            string tip = d.tip != null ? $"\n<size=70%><color=#{ColorUtility.ToHtmlStringRGB(gray)}>{d.tip}</color></size>" : "";
            label.text = (changed ? "● " : "") + d.label + tip;
            label.color = changed ? changedColor : navy;
        }

        if (d.kind == AdminSettings.Kind.Bool)
        {
            Button b = MakeButton(row, "", 24, out TMP_Text bText);
            PlaceRight(b.GetComponent<RectTransform>(), 220, 46);
            void PaintBool()
            {
                bool on = pending[d] >= 0.5f;
                bText.text = on ? "켜짐" : "꺼짐";
                b.image.color = on ? navy : rowColorB;
                bText.color = on ? Color.white : navy;
                Paint();
            }
            b.onClick.AddListener(() =>
            {
                pending[d] = pending[d] >= 0.5f ? 0f : 1f;
                SoundManager.Play(Sfx.MenuMove);
                PaintBool();
            });
            PaintBool();
            return;
        }

        TMP_InputField input = MakeInput(row, d.kind == AdminSettings.Kind.Percent ? "%" : "");
        PlaceRight(input.GetComponent<RectTransform>(), 220, 46);
        input.contentType = d.kind == AdminSettings.Kind.Int ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
        input.text = AdminSettings.Format(d, pending[d]);
        input.onEndEdit.AddListener(s =>
        {
            if (AdminSettings.TryParse(d, s, out float v)) pending[d] = v;
            else SetMessage($"'{d.label}' 에는 숫자만 적을 수 있어요", changedColor);
            input.SetTextWithoutNotify(AdminSettings.Format(d, pending[d]));   // 범위를 벗어나면 맞춰서 다시 보여줌
            Paint();
        });
        Paint();
    }

    private void BuildPasswordRows()
    {
        RectTransform info = Row(0, 70);
        string state = AdminSettings.HasCustomPassword ? "바꾼 비밀번호를 쓰는 중" : "기본 비밀번호(비밀번호 창 Inspector 값)를 쓰는 중";
        if (resetPassword) state = "저장하면 기본 비밀번호로 돌아감";
        Place(MakeText(info, state, 24, gray, TextAlignmentOptions.MidlineLeft).rectTransform, 0, 1, 28, -28);

        PasswordRow(1, "새 비밀번호", newPassword, s => newPassword = s);
        PasswordRow(2, "새 비밀번호 한 번 더", newPassword2, s => newPassword2 = s);
    }

    private void PasswordRow(int index, string label, string value, System.Action<string> onChange)
    {
        RectTransform row = Row(index, 62);
        Place(MakeText(row, label, 26, navy, TextAlignmentOptions.MidlineLeft).rectTransform, 0, 1, 28, -470);
        TMP_InputField input = MakeInput(row, "");
        PlaceRight(input.GetComponent<RectTransform>(), 300, 46);
        input.contentType = TMP_InputField.ContentType.Password;
        input.text = value;
        input.onValueChanged.AddListener(s => { onChange(s); resetPassword = false; });
    }

    private static string Unit(AdminSettings.Def d) => d.kind == AdminSettings.Kind.Percent ? "%" : "";

    private void SetMessage(string text, Color color)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageText.color = color;
    }

    // ---------- 화면 만들기 ----------
    private void Build()
    {
        built = true;
        if (font == null)
        {
            TMP_Text any = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (any != null) font = any.font;
        }

        ui = new GameObject("AdminSettingsPanel UI", typeof(RectTransform));
        ui.SetActive(false);
        Canvas canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        ui.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)ui.transform;

        // 뒤 어둡게 (뒤에 있는 메뉴가 안 눌리게)
        Image dim = NewUI("Dim", root).gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(dim.rectTransform);

        // 창
        RectTransform window = NewUI("Window", root);
        window.gameObject.AddComponent<Image>().color = windowColor;
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1360, 960);

        TMP_Text title = MakeText(window, "관리자 설정", 46, navy, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        Top(title.rectTransform, 36, 60, 40);
        TMP_Text sub = MakeText(window, "값을 바꾸고 저장하면 다음 게임부터 적용돼요 · <color=#D9480F>●</color> 바꾼 항목 · Esc 닫기", 22, gray, TextAlignmentOptions.TopLeft);
        Top(sub.rectTransform, 92, 34, 40);

        // 탭
        RectTransform tabs = NewUI("Tabs", window);
        Top(tabs, 140, 56, 40);
        var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
        var tabNames = new List<string>(AdminSettings.Tabs) { PasswordTab };
        foreach (string t in tabNames)
        {
            string tab = t;
            Button b = MakeButton(tabs, tab, 24, out TMP_Text bt);
            b.onClick.AddListener(() => { SoundManager.Play(Sfx.MenuMove); ShowTab(tab); });
            tabButtons.Add((tab, b, bt));
        }

        // 목록 (스크롤)
        RectTransform view = NewUI("Viewport", window);
        view.anchorMin = new Vector2(0, 0); view.anchorMax = new Vector2(1, 1);
        view.offsetMin = new Vector2(40, 130); view.offsetMax = new Vector2(-40, -212);
        view.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);   // 휠이 빈 곳에서도 먹게
        view.gameObject.AddComponent<RectMask2D>();

        content = NewUI("Content", view);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = window.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // 아래 안내 + 버튼
        messageText = MakeText(window, "", 22, navy, TextAlignmentOptions.MidlineLeft);
        RectTransform m = messageText.rectTransform;
        m.anchorMin = new Vector2(0, 0); m.anchorMax = new Vector2(1, 0);
        m.pivot = new Vector2(0.5f, 0);
        m.offsetMin = new Vector2(40, 90); m.offsetMax = new Vector2(-40, 124);

        RectTransform footer = NewUI("Footer", window);
        footer.anchorMin = new Vector2(0, 0); footer.anchorMax = new Vector2(1, 0);
        footer.pivot = new Vector2(0.5f, 0);
        footer.offsetMin = new Vector2(40, 28); footer.offsetMax = new Vector2(-40, 88);
        var fh = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        fh.spacing = 12; fh.childControlWidth = fh.childControlHeight = true; fh.childForceExpandWidth = true; fh.childForceExpandHeight = true;

        MakeButton(footer, "이 탭 기본값", 26, out _).onClick.AddListener(ResetTab);
        MakeButton(footer, "전부 기본값", 26, out _).onClick.AddListener(ResetAll);
        MakeButton(footer, "취소", 26, out _).onClick.AddListener(Close);
        Button save = MakeButton(footer, "저장", 26, out TMP_Text saveText);
        save.image.color = navy;
        saveText.color = Color.white;
        saveText.fontStyle = FontStyles.Bold;
        save.onClick.AddListener(SaveAndClose);
    }

    private RectTransform Row(int index, float height)
    {
        RectTransform row = NewUI("Row", content);
        row.gameObject.AddComponent<Image>().color = index % 2 == 0 ? rowColorA : rowColorB;
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        return row;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 창 위쪽에 가로로 꽉 차게 (y: 위에서부터, 높이, 양옆 여백)
    private static void Top(RectTransform rt, float y, float height, float side)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(side, -y - height);
        rt.offsetMax = new Vector2(-side, -y);
    }

    // 줄 안에서 가로 위치 (anchor 0=왼쪽, 1=오른쪽 기준)
    private static void Place(RectTransform rt, float anchorLeft, float anchorRight, float left, float right)
    {
        rt.anchorMin = new Vector2(anchorLeft, 0); rt.anchorMax = new Vector2(anchorRight, 1);
        rt.offsetMin = new Vector2(left, 0); rt.offsetMax = new Vector2(right, 0);
    }

    private static void PlaceRight(RectTransform rt, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(1, 0.5f);
        rt.anchoredPosition = new Vector2(-28, 0);
        rt.sizeDelta = new Vector2(width, height);
    }

    private TMP_Text MakeText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var t = NewUI("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private Button MakeButton(Transform parent, string label, float size, out TMP_Text text)
    {
        RectTransform rt = NewUI("Button", parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = rowColorB;
        Button b = rt.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        b.colors = cb;
        text = MakeText(rt, label, size, navy, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return b;
    }

    private TMP_InputField MakeInput(Transform parent, string suffix)
    {
        RectTransform rt = NewUI("Input", parent);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = Color.white;
        Outline line = rt.gameObject.AddComponent<Outline>();
        line.effectColor = gray;
        line.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform area = NewUI("Text Area", rt);
        Stretch(area);
        area.offsetMin = new Vector2(14, 4);
        area.offsetMax = new Vector2(suffix.Length > 0 ? -40 : -14, -4);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text text = MakeText(area, "", 26, navy, TextAlignmentOptions.MidlineRight);
        Stretch(text.rectTransform);
        text.raycastTarget = true;

        if (suffix.Length > 0)
        {
            TMP_Text s = MakeText(rt, suffix, 24, gray, TextAlignmentOptions.MidlineRight);
            Stretch(s.rectTransform);
            s.rectTransform.offsetMax = new Vector2(-12, 0);
        }

        TMP_InputField input = rt.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = bg;
        input.textViewport = area;
        input.textComponent = (TextMeshProUGUI)text;
        input.caretColor = navy;
        input.customCaretColor = true;
        input.selectionColor = new Color(navy.r, navy.g, navy.b, 0.25f);
        input.onFocusSelectAll = true;
        return input;
    }
}
