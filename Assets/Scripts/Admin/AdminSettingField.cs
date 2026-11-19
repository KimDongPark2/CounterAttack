using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 직접 만든 관리자 설정 창의 "한 줄"에 붙이는 스크립트
// 예) Settings/MaxCustomer 오브젝트에 붙이고 Inspector에서 항목을 "손님 / 가게 안 최대 손님 수"로 고르기
// - 창이 켜질 때 지금 값(바꾼 값 또는 기본값)을 입력 칸에 보여줌
// - 입력 칸에 숫자를 적고 Enter를 누르거나 칸 밖을 누르면 바로 저장 (다음 게임부터 적용)
// - 범위를 벗어나면 최소/최대로 맞춰줌. 도둑 확률처럼 % 항목은 0~100으로 적기
// - 켜기/끄기 항목은 Toggle을 연결하면 됨
public class AdminSettingField : MonoBehaviour
{
    [Tooltip("바꿀 항목 (목록에서 고르기)")]
    [AdminSettingKey] public string key;

    [Header("연결 (비우면 자식에서 자동으로 찾음)")]
    public TMP_InputField input;
    [Tooltip("켜기/끄기 항목일 때 (선택)")]
    public Toggle toggle;

    [Header("글자 (선택)")]
    [Tooltip("넣으면 항목 이름을 여기에 적어줌 (직접 적어둔 글자를 쓰려면 비우기)")]
    public TMP_Text nameText;
    [Tooltip("켜면 입력 칸이 비었을 때 \"기본 15\"처럼 기본값을 흐리게 보여줌")]
    public bool showDefaultInPlaceholder = true;

    private AdminSettings.Def def;

    private void Awake()
    {
        def = AdminSettings.Find(key);
        if (def == null)
        {
            Debug.LogWarning($"[AdminSettingField] '{name}'의 항목을 Inspector에서 골라주세요. (지금: '{key}')", this);
            enabled = false;
            return;
        }

        if (input == null && toggle == null)
        {
            if (def.kind == AdminSettings.Kind.Bool) toggle = GetComponentInChildren<Toggle>(true);
            if (toggle == null) input = GetComponentInChildren<TMP_InputField>(true);
        }

        if (input != null)
        {
            input.contentType = def.kind == AdminSettings.Kind.Int
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.DecimalNumber;
            input.onEndEdit.AddListener(OnEndEdit);
        }
        if (toggle != null) toggle.onValueChanged.AddListener(OnToggle);

        if (input == null && toggle == null)
            Debug.LogWarning($"[AdminSettingField] '{name}' 아래에 TMP_InputField(또는 Toggle)가 없어요.", this);
    }

    private void OnEnable() => Refresh();

    // 저장된 값으로 다시 보여주기
    public void Refresh()
    {
        if (def == null) return;
        float v = AdminSettings.Get(def);

        if (nameText != null) nameText.text = def.label;
        if (input != null)
        {
            input.SetTextWithoutNotify(AdminSettings.Format(def, v));
            if (showDefaultInPlaceholder && input.placeholder is TMP_Text ph)
                ph.text = "기본 " + AdminSettings.Format(def, AdminSettings.DefaultOf(def)) + (def.kind == AdminSettings.Kind.Percent ? "%" : "");
        }
        if (toggle != null) toggle.SetIsOnWithoutNotify(v >= 0.5f);
    }

    // 이 항목을 기본값으로 (버튼 OnClick에 연결해서 써도 됨)
    public void ResetToDefault()
    {
        if (def == null) return;
        AdminSettings.Set(def, AdminSettings.DefaultOf(def));
        AdminSettings.Save();
        AdminSettings.ApplyStatics();
        Refresh();
    }

    private void OnEndEdit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Refresh();   // 비우면 원래 값으로
            return;
        }
        if (AdminSettings.TryParse(def, text, out float v))
        {
            AdminSettings.Set(def, v);
            AdminSettings.Save();
            AdminSettings.ApplyStatics();   // 별 배율 같은 static 값은 바로 반영
        }
        Refresh();   // 범위를 벗어났으면 맞춘 값으로 다시 보여줌
    }

    private void OnToggle(bool on)
    {
        AdminSettings.Set(def, on ? 1f : 0f);
        AdminSettings.Save();
        AdminSettings.ApplyStatics();   // 치트 기록 인정 같은 static 값은 바로 반영
    }
}

// Inspector에서 항목을 목록으로 고르게 해주는 표시 (그리는 건 Editor/AdminSettingKeyDrawer.cs)
public class AdminSettingKeyAttribute : PropertyAttribute { }
