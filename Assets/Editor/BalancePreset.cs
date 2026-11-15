using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 밸런스 한 번에 바꾸기 (Game 씬을 열어둔 채로)
// 메뉴: Tools → 밸런스 → 시연용 적용
//   시작 돈 1,500 → 5,000원 · 도둑 30% → 15% · 급한 손님 40% → 30%
//   점심 러시 간격 6~8초 → 7~9초 · 퇴근 러시 5~7초 → 6~8초
// 바꾼 뒤 씬 저장(Ctrl+S)하면 끝. Ctrl+Z로 되돌릴 수 있음
public static class BalancePreset
{
    private const int StartingMoney = 5000;
    private const float ThiefChance = 0.15f;
    private const float HurryChance = 0.30f;

    [MenuItem("Tools/밸런스/시연용 적용")]
    private static void Apply()
    {
        var log = new StringBuilder();

        foreach (Player_State ps in Object.FindObjectsByType<Player_State>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(ps, "밸런스 적용");
            log.AppendLine($"시작 돈: {ps.startingMoney:N0} → {StartingMoney:N0}원");
            ps.startingMoney = StartingMoney;
            EditorUtility.SetDirty(ps);
        }

        foreach (CustomerSpawner sp in Object.FindObjectsByType<CustomerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(sp, "밸런스 적용");
            log.AppendLine($"도둑: {sp.thiefChance * 100:0}% → {ThiefChance * 100:0}%");
            log.AppendLine($"급한 손님: {sp.hurryChance * 100:0}% → {HurryChance * 100:0}%");
            sp.thiefChance = ThiefChance;
            sp.hurryChance = HurryChance;
            if (sp.phases != null)
            {
                foreach (CustomerSpawner.Phase p in sp.phases)
                {
                    if (p == null || p.name == null) continue;
                    if (p.name.Contains("점심")) Set(p, 7f, 9f, log);
                    else if (p.name.Contains("퇴근")) Set(p, 6f, 8f, log);
                }
            }
            EditorUtility.SetDirty(sp);
        }

        if (log.Length == 0)
        {
            EditorUtility.DisplayDialog("밸런스", "Game 씬을 열고 다시 눌러주세요.\n(Player_State · CustomerSpawner를 못 찾았어요)", "확인");
            return;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[밸런스] 시연용 적용\n" + log);
        EditorUtility.DisplayDialog("밸런스", log + "\n씬을 저장(Ctrl+S)하면 끝이에요.", "확인");
    }

    private static void Set(CustomerSpawner.Phase p, float min, float max, StringBuilder log)
    {
        log.AppendLine($"{p.name} 간격: {p.minInterval:0}~{p.maxInterval:0}초 → {min:0}~{max:0}초");
        p.minInterval = min;
        p.maxInterval = max;
    }
}
