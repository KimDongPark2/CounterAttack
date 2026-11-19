using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 관리자 설정 값 모음 (게임 밸런스 · 플레이어)
// - 관리자 설정 창에서 바꾼 값은 PlayerPrefs에 저장 → 게임을 껐다 켜도 남음
// - 씬이 열릴 때마다 (Start보다 먼저) 그 씬에 있는 스크립트에 값을 넣어줌
// - 바꾸지 않은 값은 씬(Inspector)에 적힌 값을 그대로 씀
//
// 새 항목을 넣고 싶으면 아래 Defs 목록에 한 줄 추가하면 끝
//   Add(탭, 이름, 스크립트 종류, "변수 이름", 최소, 최대, 기본값, 종류)
//   변수 이름에 "phases[1].minInterval" 처럼 배열 칸도 쓸 수 있음
public static class AdminSettings
{
    public enum Kind { Int, Float, Bool, Percent }   // Percent: 0~1로 저장, 창에선 0~100%로 보임

    public class Def
    {
        public string key;       // 저장 이름 (안 바꾸는 게 좋음)
        public string tab;       // 창의 탭
        public string label;     // 창에 보이는 이름
        public string tip;       // 작은 설명 (선택)
        public Kind kind;
        public float min, max;
        public float fallback;   // 씬 값을 아직 못 읽었을 때 쓰는 기본값
        public Type type;        // 값이 들어갈 스크립트
        public string path;      // 변수 이름
        public bool isStatic;    // static 변수면 true (StoreStats 등)
        public Func<Object, bool> filter;   // 같은 스크립트 중 일부만 바꿀 때 (선택)
    }

    private const string ValuePrefix = "Admin.";
    private const string DefaultPrefix = "AdminDef.";
    private const string PasswordKey = "Admin.Password";

    // ---------- 항목 목록 ----------
    public static readonly List<Def> Defs = new List<Def>();
    public static readonly List<string> Tabs = new List<string>();

    static AdminSettings()
    {
        const string day = "하루", cus = "손님", rush = "손님 간격", score = "점수", shop = "계산 · 상점", player = "플레이어", cheat = "치트";
        Type sp = typeof(CustomerSpawner);

        // 하루
        Add(day, "하루 길이 (초)", sp, "dayLength", 30, 1800, 300, Kind.Float, "게임 속 9시~22시가 이만큼 걸림");
        Add(day, "손님 그만 오는 시각 (초)", sp, "stopSpawnTime", 0, 1800, 280, Kind.Float, "하루 길이보다 조금 짧게");
        Add(day, "영업 준비 시간 (초)", sp, "prepTime", 0, 300, 50, Kind.Float, "0이면 바로 영업 시작");
        Add(day, "준비 중 Enter로 건너뛰기", sp, "allowSkip", 0, 1, 1, Kind.Bool);

        // 손님
        Add(cus, "가게 안 최대 손님 수", sp, "maxCustomers", 1, 50, 15, Kind.Int);
        Add(cus, "도둑일 확률", sp, "thiefChance", 0, 1, 0.15f, Kind.Percent, "도둑 + 급한 손님이 100%를 넘지 않게");
        Add(cus, "급한 손님일 확률", sp, "hurryChance", 0, 1, 0.3f, Kind.Percent);
        Add(cus, "사는 물건 최소 개수", sp, "minItems", 1, 20, 1, Kind.Int);
        Add(cus, "사는 물건 최대 개수", sp, "maxItems", 1, 20, 5, Kind.Int);
        Add(cus, "보통 손님 참을성 (초)", sp, "normalPatience", 1, 300, 25, Kind.Float);
        Add(cus, "급한 손님 참을성 (초)", sp, "hurryPatience", 1, 300, 12, Kind.Float);
        Add(cus, "걷는 속도", sp, "walkSpeed", 0.1f, 50, 7, Kind.Float);
        Add(cus, "급한 손님 속도", sp, "hurrySpeed", 0.1f, 50, 9, Kind.Float);
        Add(cus, "도망 속도 (도둑)", sp, "runSpeed", 0.1f, 50, 15, Kind.Float);
        Add(cus, "물건 고르는 시간 (초)", sp, "pickTime", 0, 30, 1, Kind.Float);
        Add(cus, "결제 후 인사 시간 (초)", sp, "greetTime", 0, 10, 1.2f, Kind.Float);

        // 손님 간격 (단계별)
        string[] phaseNames = { "오전", "점심 러시", "오후", "퇴근 러시", "마감" };
        float[] phaseMin = { 12, 7, 10, 6, 12 };
        float[] phaseMax = { 15, 9, 12, 8, 15 };
        for (int i = 0; i < phaseNames.Length; i++)
        {
            Add(rush, $"{phaseNames[i]} · 간격 최소 (초)", sp, $"phases[{i}].minInterval", 0.5f, 120, phaseMin[i], Kind.Float);
            Add(rush, $"{phaseNames[i]} · 간격 최대 (초)", sp, $"phases[{i}].maxInterval", 0.5f, 120, phaseMax[i], Kind.Float);
        }

        // 점수
        Add(score, "빨리 계산", sp, "fastPoint", -100, 100, 5, Kind.Int, "만족도 점수");
        Add(score, "늦게 계산", sp, "slowPoint", -100, 100, 1, Kind.Int);
        Add(score, "손님이 화나서 나감", sp, "angryPoint", -100, 100, -3, Kind.Int);
        Add(score, "물건이 없어서 못 삼 (개당)", sp, "missingPoint", -100, 100, -5, Kind.Int);
        Add(score, "일반 손님을 쏨", sp, "wrongShotPoint", -100, 100, -15, Kind.Int);
        Add(score, "도둑을 잡음", sp, "thiefShotPoint", -100, 100, 5, Kind.Int);
        Add(score, "도둑을 놓침", sp, "thiefEscapePoint", -100, 100, -10, Kind.Int);
        Add(score, "도둑 잡은 보상 (원)", sp, "thiefReward", 0, 1000000, 1000, Kind.Int, "매출에는 안 들어감");
        float[] cuts = { 0.5f, 1.5f, 2.5f, 3.5f };
        for (int i = 0; i < cuts.Length; i++)
            AddStatic(score, $"★{i + 2} 기준 (1명당 점수)", typeof(StoreStats), $"StarCuts[{i}]", -100, 100, cuts[i], Kind.Float);
        float[] mults = { 0.5f, 0.8f, 1.0f, 1.3f, 1.6f };
        for (int i = 0; i < mults.Length; i++)
            AddStatic(score, $"★{i + 1} 점수 배율", typeof(StoreStats), $"StarMultipliers[{i}]", 0, 10, mults[i], Kind.Float, i == 0 ? "최종 점수 = 매출 × 배율 − 빚" : null);

        // 계산 · 상점
        Type cc = typeof(CheckoutCounter);
        Add(shop, "판매가 배율 (사는 값 ×)", cc, "sellMarkup", 0.1f, 10, 1.3f, Kind.Float);
        Add(shop, "판매가 반올림 단위 (원)", cc, "roundTo", 1, 10000, 10, Kind.Int);
        Add(shop, "손님이 물건 올리는 간격 (초)", cc, "placeInterval", 0, 10, 0.8f, Kind.Float);
        Add(shop, "물건 하나씩 올리기", cc, "oneAtATime", 0, 1, 0, Kind.Bool, "켜면 찍어야 다음 물건이 올라옴");
        Add(shop, "조준해야 스캔됨", cc, "requireAim", 0, 1, 1, Kind.Bool);
        Add(shop, "스캔 거리", cc, "scanRange", 0.5f, 100, 50, Kind.Float);
        Type ds = typeof(DeliverySystem);
        Add(shop, "배달 걸리는 시간 (초)", ds, "deliveryDelay", 0, 120, 0, Kind.Float);
        Add(shop, "배달 물건 사이 간격 (초)", ds, "coolTime", 0, 10, 1, Kind.Float);

        // 상점 카드: 같은 스크립트(UpgradeCardUI)라서 종류로 골라서 바꿈. 저장 이름도 종류별로 따로
        Type up = typeof(UpgradeCardUI);
        Func<Object, bool> pistol = o => ((UpgradeCardUI)o).type == UpgradeCardUI.UpgradeType.Pistol;
        Func<Object, bool> speed = o => ((UpgradeCardUI)o).type == UpgradeCardUI.UpgradeType.MoveSpeed;
        Filter(Add(shop, "권총 가격 (원)", up, "basePrice", 0, 1000000, 1500, Kind.Int), pistol, "pistol");
        Filter(Add(shop, "이동 속도 카드 첫 가격 (원)", up, "basePrice", 0, 1000000, 3000, Kind.Int, "카드가 상점에 있을 때만"), speed, "speed");
        Filter(Add(shop, "이동 속도 카드 가격 상승 (원)", up, "priceIncrease", 0, 1000000, 2000, Kind.Int), speed, "speed");
        Filter(Add(shop, "이동 속도 카드 최대 단계", up, "maxLevel", 1, 50, 5, Kind.Int), speed, "speed");
        Filter(Add(shop, "이동 속도 카드 1단계당 속도", up, "speedPerLevel", 0, 20, 1, Kind.Float), speed, "speed");

        // 플레이어
        Type pm = typeof(Player_movement);
        Add(player, "시작 돈 (원)", typeof(Player_State), "startingMoney", 0, 10000000, 5000, Kind.Int);
        Add(player, "걷기 속도", pm, "moveSpeed", 0.1f, 100, 15, Kind.Float);
        Add(player, "달리기 속도", pm, "RunningSpeed", 0.1f, 100, 25, Kind.Float);
        Add(player, "앉아서 걷는 속도", pm, "sitSpeed", 0.1f, 100, 10, Kind.Float);
        Add(player, "앉았을 때 키", pm, "sitHeight", 0.1f, 10, 1, Kind.Float);
        Add(player, "앉을 때 카메라 내려가는 정도", pm, "cameraSitOffset", 0, 20, 5, Kind.Float);
        Add(player, "까치발 카메라 올라가는 정도", pm, "cameraLookUpOffset", 0, 20, 3, Kind.Float);
        Add(player, "카메라 높이 바뀌는 속도", pm, "cameraMoveSpeed", 0.1f, 100, 8, Kind.Float);
        Add(player, "시작 높이 고정", pm, "lockHeight", 0, 1, 1, Kind.Bool, "시점이 올라가면 되돌리기");
        Add(player, "시작 높이로 돌아가는 속도", pm, "heightReturnSpeed", 0.1f, 100, 10, Kind.Float);
        Add(player, "마우스 감도 배율", typeof(Player_Look), "sensitivityScale", 0.0001f, 1, 0.01f, Kind.Float, "감도 슬라이더(1~100)에 곱해지는 값");
        Add(player, "물건 집는 거리", typeof(Player_Interaction), "interactRange", 0.5f, 100, 10, Kind.Float);
        Add(player, "던지는 힘", typeof(Player_Interaction), "throwForce", 0, 50, 4, Kind.Float);
        Add(player, "던질 때 위로 힘", typeof(Player_Interaction), "throwUpward", 0, 50, 1.5f, Kind.Float);
        Add(player, "물건 설치 거리", typeof(Player_ItemInstall), "installRange", 0.5f, 100, 10, Kind.Float);
        Add(player, "설치 자리 붙는 범위", typeof(Player_ItemInstall), "snapRadius", 0, 50, 15, Kind.Float);

        // 치트
        AddStatic(cheat, "치트를 써도 기록 인정", typeof(CheatManager), "AllowRecordWithCheat", 0, 1, 0, Kind.Bool, "켜면 치트를 써도 순위표 · 최고 점수에 남음");
    }

    private static Def Add(string tab, string label, Type type, string path, float min, float max, float fallback, Kind kind, string tip = null)
    {
        var d = new Def
        {
            key = type.Name + "." + path, tab = tab, label = label, tip = tip, kind = kind,
            min = min, max = max, fallback = fallback, type = type, path = path,
        };
        Defs.Add(d);
        if (!Tabs.Contains(tab)) Tabs.Add(tab);
        return d;
    }

    private static void Filter(Def d, Func<Object, bool> filter, string keyTag)
    {
        d.filter = filter;
        d.key = d.type.Name + "." + keyTag + "." + d.path;
    }

    private static Def AddStatic(string tab, string label, Type type, string path, float min, float max, float fallback, Kind kind, string tip = null)
    {
        Def d = Add(tab, label, type, path, min, max, fallback, kind, tip);
        d.isStatic = true;
        return d;
    }

    // ---------- 값 읽기 / 쓰기 ----------
    // 저장 이름으로 항목 찾기 (없으면 null)
    public static Def Find(string key)
    {
        foreach (Def d in Defs) if (d.key == key) return d;
        return null;
    }

    public static bool HasValue(Def d) => PlayerPrefs.HasKey(ValuePrefix + d.key);

    // 기본값: 게임 씬에서 읽어둔 값 (아직 없으면 위 목록의 값)
    public static float DefaultOf(Def d) => PlayerPrefs.GetFloat(DefaultPrefix + d.key, d.fallback);

    // 지금 쓰이는 값 (관리자가 바꿨으면 그 값, 아니면 기본값)
    public static float Get(Def d) => HasValue(d) ? PlayerPrefs.GetFloat(ValuePrefix + d.key) : DefaultOf(d);

    public static float Clamp(Def d, float v)
    {
        v = Mathf.Clamp(v, d.min, d.max);
        if (d.kind == Kind.Int) v = Mathf.Round(v);
        if (d.kind == Kind.Bool) v = v >= 0.5f ? 1f : 0f;
        return v;
    }

    // 기본값과 같으면 저장을 지워서 "안 바꾼 상태"로
    public static void Set(Def d, float v)
    {
        v = Clamp(d, v);
        if (Mathf.Approximately(v, DefaultOf(d))) PlayerPrefs.DeleteKey(ValuePrefix + d.key);
        else PlayerPrefs.SetFloat(ValuePrefix + d.key, v);
    }

    public static void ResetAll()
    {
        foreach (Def d in Defs) PlayerPrefs.DeleteKey(ValuePrefix + d.key);
        PlayerPrefs.Save();
    }

    public static void Save() => PlayerPrefs.Save();

    // ---------- 비밀번호 ----------
    // 관리자 창에서 바꾸면 PlayerPrefs에 저장. 안 바꿨으면 AdminPasswordPanel Inspector의 값
    public static string GetPassword(string inspectorDefault) => PlayerPrefs.GetString(PasswordKey, inspectorDefault);
    public static bool HasCustomPassword => PlayerPrefs.HasKey(PasswordKey);
    public static void SetPassword(string pw)
    {
        if (string.IsNullOrEmpty(pw)) PlayerPrefs.DeleteKey(PasswordKey);
        else PlayerPrefs.SetString(PasswordKey, pw);
        PlayerPrefs.Save();
    }

    // ---------- 씬에 적용 ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyStatics();
    }

    // sceneLoaded는 씬 오브젝트의 Awake 다음, Start 전에 불림 → Start에서 값을 쓰는 스크립트도 바뀐 값을 씀
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene();

    // 지금 열린 씬에 전부 적용 (게임 중에 바로 반영하고 싶을 때도 부르면 됨)
    public static void ApplyToScene()
    {
        bool learned = false;
        foreach (Def d in Defs)
        {
            if (d.isStatic) continue;
            Object[] found = Object.FindObjectsByType(d.type, FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool first = true;
            foreach (Object o in found)
            {
                if (d.filter != null && !d.filter(o)) continue;
                // 처음 만난 것의 씬 값을 "기본값"으로 기억 (관리자 창에 기본값으로 보여줌)
                if (first && TryRead(o, d.path, out float sceneValue))
                {
                    PlayerPrefs.SetFloat(DefaultPrefix + d.key, sceneValue);
                    learned = true;
                }
                first = false;
                if (HasValue(d)) Write(o, d.path, Get(d));
            }
        }
        ApplyStatics();
        if (learned) PlayerPrefs.Save();
    }

    // static 변수는 씬이 바뀌어도 남으므로 항상 지금 값(바꾼 값 또는 기본값)을 넣음
    public static void ApplyStatics()
    {
        foreach (Def d in Defs)
            if (d.isStatic) Write(null, d.path, Get(d), d.type);
    }

    // ---------- "phases[1].minInterval" 같은 경로 따라가기 ----------
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static bool TryRead(object root, string path, out float value)
    {
        value = 0f;
        try
        {
            object v = Walk(root, root.GetType(), path, false, 0f);
            if (v == null) return false;
            value = v is bool b ? (b ? 1f : 0f) : Convert.ToSingle(v, CultureInfo.InvariantCulture);
            return true;
        }
        catch { return false; }
    }

    private static void Write(object root, string path, float value, Type staticType = null)
    {
        try { Walk(root, staticType ?? root.GetType(), path, true, value); }
        catch (Exception e) { Debug.LogWarning($"[AdminSettings] {path} 적용 실패: {e.Message}"); }
    }

    // write가 false면 마지막 값을 돌려주고, true면 마지막 칸에 value를 넣음
    private static object Walk(object obj, Type type, string path, bool write, float value)
    {
        string[] parts = path.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            int index = -1;
            int br = part.IndexOf('[');
            if (br >= 0)
            {
                index = int.Parse(part.Substring(br + 1, part.IndexOf(']') - br - 1));
                part = part.Substring(0, br);
            }

            FieldInfo f = type.GetField(part, Flags);
            if (f == null) throw new MissingFieldException(type.Name, part);
            bool last = i == parts.Length - 1;

            if (index < 0)
            {
                if (last)
                {
                    if (!write) return f.GetValue(obj);
                    f.SetValue(obj, ConvertTo(value, f.FieldType));
                    return null;
                }
                obj = f.GetValue(obj);
            }
            else
            {
                if (!(f.GetValue(obj) is Array arr) || index >= arr.Length) return null;   // 칸이 모자라면 건너뜀
                if (last)
                {
                    if (!write) return arr.GetValue(index);
                    arr.SetValue(ConvertTo(value, arr.GetType().GetElementType()), index);
                    return null;
                }
                obj = arr.GetValue(index);
            }
            if (obj == null) return null;
            type = obj.GetType();
        }
        return null;
    }

    private static object ConvertTo(float v, Type t)
    {
        if (t == typeof(bool)) return v >= 0.5f;
        if (t == typeof(int)) return Mathf.RoundToInt(v);
        return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
    }

    // 창에 보여줄 글자
    public static string Format(Def d, float v)
    {
        switch (d.kind)
        {
            case Kind.Bool: return v >= 0.5f ? "켜짐" : "꺼짐";
            case Kind.Int: return Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);
            case Kind.Percent: return (v * 100f).ToString("0.##", CultureInfo.InvariantCulture);
            default: return v.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    // 창에 적은 글자 → 값 (실패하면 false)
    public static bool TryParse(Def d, string text, out float v)
    {
        text = (text ?? "").Replace(",", "").Replace("%", "").Trim();
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
        if (d.kind == Kind.Percent) v /= 100f;
        v = Clamp(d, v);
        return true;
    }
}
