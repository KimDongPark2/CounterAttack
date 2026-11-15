using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Assets/Icon 폴더 정리 (한 번만 실행하면 됨)
// 메뉴: Tools → 아이콘 폴더 정리 → 1. 미리보기 (Console에 어디로 옮길지 출력만)
//                               → 2. 실행
// - 유니티 방식(AssetDatabase.MoveAsset)으로 옮겨서 씬 · 프리팹에 연결된 그림이 안 끊김
// - 같은 이름 그림이 두 군데 있으면(Intro_sprites (2) 등): 씬에서 쓰는 쪽을 남기고, 안 쓰는 쪽은 _Unused 폴더로
//   (둘 다 쓰고 있으면 하나는 이름 뒤에 _2를 붙여서 같이 둠)
// - 다 옮기고 비어버린 옛날 폴더는 지움
// - TopHUDSpriteImporter.cs(에디터 전용)를 Assets/Editor로 옮김 → 게임 빌드할 때 오류 안 나게
public static class IconFolderOrganizer
{
    private const string Root = "Assets/Icon";
    private const string Unused = "_Unused";

    // 파일 이름으로 정하는 곳 (폴더 규칙보다 먼저)
    private static readonly Dictionary<string, string> ByName = new Dictionary<string, string>
    {
        // 여기저기서 같이 쓰는 것
        { "info_round.png", "Common" },
        { "info_dash.png", "Common" },
        // 핫바
        { "UI_Keycap.png", "HUD/Hotbar" },
        { "ui_card_selected.png", "HUD/Hotbar" },
        { "icon_empty_box.png", "HUD/Hotbar" },
        { "icon_empty_dashed.png", "HUD/Hotbar" },
        // 태블릿
        { "bg_A_pattern.png", "Tablet/Background" },
        { "bg_glow_vignette.png", "Tablet/Background" },
        { "bg_pattern_tile.png", "Tablet/Background" },
        { "btn_normal.png", "Tablet/Buttons" },
        { "btn_hover.png", "Tablet/Buttons" },
        { "btn_pressed.png", "Tablet/Buttons" },
        { "btn_remove_normal.png", "Tablet/Buttons" },
        { "btn_remove_hover.png", "Tablet/Buttons" },
        { "btn_remove_pressed.png", "Tablet/Buttons" },
        { "btn_disabled.png", "Tablet/Buttons" },
        { "btn_remove_disabled.png", "Tablet/Buttons" },
        { "icon_info.png", "Tablet/Apps" },
        { "icon_settings.png", "Tablet/Apps" },
        { "icon_shop.png", "Tablet/Apps" },
        // 로고
        { "counter_attack_horizontal_wordmark.png", "Logo" },
        { "logo_2_scan_v2.png", "Logo" },
        { "Logo2.png", "Logo" },
        { "Logo2-2.png", "Logo" },
        { "Logo2-2-1.png", "Logo" },
        { "Logo(white).png", "Logo" },
    };

    // 폴더째로 정하는 곳 (Icon 기준 경로 → 새 폴더)
    private static readonly (string from, string to)[] ByFolder =
    {
        ("Badge_sprites/TopHUD/Badge", "Badge"),
        ("Intro_sprites/TopHUD/Intro", "Intro"),
        ("Intro_sprites (2)/TopHUD/Intro", "Intro"),
        ("shutter/TopHUD/Shutter", "Shutter"),
        ("Rank_sprites/Rank", "Rank"),
        ("RankBadge_sprites/Rank", "Rank"),
        ("RankBadge_sprites (2)/Rank", "Rank"),
        ("Bank", "Tablet/Finance"),
        ("TopHUD_sprites/TopHUD", "HUD/TopBar"),
        ("DebtTag_sprite/TopHUD", "HUD/TopBar"),
        ("counter_hud_sprites", "HUD/Counter"),
        ("PhaseBanner_sprites/TopHUD/Phase", "HUD/PhaseBanner"),
        ("order_A_sprites_hd/order_A_sprites_hd", "Tablet/Order"),
        ("category_tab_sprites", "Tablet/Category"),
        ("settings_sprites/settings_sprites", "Tablet/Settings"),
        ("info_sprites", "Tablet/Info"),
        ("TopHUD/Info", "Tablet/Info"),
        ("Finance_icons/TopHUD/Shop", "Tablet/Finance"),
    };

    private struct Move
    {
        public string from;
        public string to;
        public string note;
    }

    [MenuItem("Tools/아이콘 폴더 정리/1. 미리보기")]
    private static void Preview()
    {
        List<Move> plan = MakePlan(out List<string> skipped);
        Debug.Log(Describe(plan, skipped));
    }

    [MenuItem("Tools/아이콘 폴더 정리/2. 실행")]
    private static void Run()
    {
        List<Move> plan = MakePlan(out List<string> skipped);
        if (plan.Count == 0)
        {
            EditorUtility.DisplayDialog("아이콘 폴더 정리", "옮길 게 없어요. 이미 정리돼 있어요.", "확인");
            return;
        }
        int unused = plan.Count(m => m.to.Contains("/" + Unused + "/"));
        if (!EditorUtility.DisplayDialog("아이콘 폴더 정리",
                $"그림 {plan.Count}개를 새 폴더로 옮겨요.\n(안 쓰는 중복 {unused}개는 Icon/{Unused}로)\n\n씬에 연결된 그림은 안 끊겨요. 진행할까요?\n(먼저 씬을 저장해두세요)",
                "옮기기", "취소")) return;

        AssetDatabase.SaveAssets();

        // 1) 폴더 먼저 만들기
        foreach (string folder in plan.Select(m => Path.GetDirectoryName(m.to).Replace('\\', '/')).Distinct())
            EnsureFolder(folder);
        AssetDatabase.Refresh();

        // 2) 옮기기
        int ok = 0;
        var log = new StringBuilder("[아이콘 폴더 정리] 결과\n");
        foreach (Move m in plan)
        {
            string err = AssetDatabase.MoveAsset(m.from, m.to);
            if (string.IsNullOrEmpty(err)) { ok++; log.AppendLine($"  {m.from}  →  {m.to}{m.note}"); }
            else log.AppendLine($"  ✗ {m.from} : {err}");
        }

        // 3) 비어버린 옛날 폴더 지우기
        int removed = DeleteEmptyFolders(Root);

        // 4) 에디터 전용 스크립트를 Editor 폴더로 (빌드 오류 방지)
        string importer = "Assets/Scripts/TopHUDSpriteImporter.cs";
        if (File.Exists(importer))
        {
            EnsureFolder("Assets/Editor");
            AssetDatabase.Refresh();
            string err = AssetDatabase.MoveAsset(importer, "Assets/Editor/TopHUDSpriteImporter.cs");
            log.AppendLine(string.IsNullOrEmpty(err) ? "  TopHUDSpriteImporter.cs → Assets/Editor" : "  ✗ TopHUDSpriteImporter.cs : " + err);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"옮김 {ok}/{plan.Count} · 빈 폴더 지움 {removed}개");
        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("아이콘 폴더 정리", $"다 했어요.\n옮김 {ok}/{plan.Count} · 빈 폴더 {removed}개 지움\n자세한 건 Console을 보세요.", "확인");
    }

    // ---------- 계획 ----------
    private static List<Move> MakePlan(out List<string> skipped)
    {
        skipped = new List<string>();
        var wanted = new List<(string from, string to)>();

        foreach (string guid in AssetDatabase.FindAssets("", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) continue;
            string rel = path.Substring(Root.Length + 1);
            string file = Path.GetFileName(path);
            string dir = Path.GetDirectoryName(rel).Replace('\\', '/');

            // 이미 새 폴더에 있는 건 그대로
            if (IsNewFolder(dir)) continue;

            string dest = null;
            if (ByName.TryGetValue(file, out string byName)) dest = byName;
            else
            {
                foreach (var r in ByFolder)
                    if (dir == r.from || dir.StartsWith(r.from + "/")) { dest = r.to; break; }
                if (dest == null && dir.Length == 0) dest = "Misc";   // Icon 바로 아래 남은 그림
            }
            if (dest == null) { skipped.Add(path); continue; }

            string to = $"{Root}/{dest}/{file}";
            if (to != path) wanted.Add((path, to));
        }

        // 같은 자리로 가는 게 여러 개면 (중복) 씬에서 쓰는 쪽을 남김
        HashSet<string> used = CollectUsedGuids();
        var plan = new List<Move>();
        foreach (var group in wanted.GroupBy(w => w.to))
        {
            var items = group.OrderBy(w => w.from.Contains(" (2)") ? 1 : 0).ToList();
            if (items.Count == 1 && !File.Exists(group.Key))
            {
                plan.Add(new Move { from = items[0].from, to = items[0].to, note = "" });
                continue;
            }

            int primary = items.FindIndex(w => used.Contains(AssetDatabase.AssetPathToGUID(w.from)));
            if (primary < 0) primary = 0;
            bool targetTaken = File.Exists(group.Key);   // 이미 새 폴더에 같은 이름이 있으면
            int n = 2;
            for (int i = 0; i < items.Count; i++)
            {
                var w = items[i];
                bool isUsed = used.Contains(AssetDatabase.AssetPathToGUID(w.from));
                if (i == primary && !targetTaken)
                    plan.Add(new Move { from = w.from, to = w.to, note = "" });
                else if (isUsed)
                    plan.Add(new Move { from = w.from, to = Suffix(w.to, n++), note = "  (중복이지만 씬에서 쓰는 중이라 _" + (n - 1) + "을 붙임)" });
                else
                    plan.Add(new Move { from = w.from, to = UniqueIn($"{Root}/{Unused}", Path.GetFileName(w.to), plan), note = "  (안 쓰는 중복)" });
            }
        }
        return plan.OrderBy(m => m.to).ToList();
    }

    private static readonly string[] NewTops = { "Common", "HUD", "Tablet", "Logo", "Intro", "Shutter", "Badge", "Rank", "Misc", Unused };

    private static bool IsNewFolder(string dir)
    {
        if (dir.Length == 0) return false;
        string top = dir.Split('/')[0];
        return NewTops.Contains(top);
    }

    // 씬 · 프리팹 · 애니메이션 · 머티리얼 등에서 쓰고 있는 GUID 전부
    private static HashSet<string> CollectUsedGuids()
    {
        var set = new HashSet<string>();
        var rx = new Regex("guid: ([0-9a-f]{32})");
        string[] exts = { ".unity", ".prefab", ".asset", ".controller", ".anim", ".mat", ".overrideController", ".spriteatlas", ".spriteatlasv2" };
        foreach (string f in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            if (!exts.Contains(Path.GetExtension(f))) continue;
            try
            {
                foreach (Match m in rx.Matches(File.ReadAllText(f))) set.Add(m.Groups[1].Value);
            }
            catch { }
        }
        return set;
    }

    // ---------- 도우미 ----------
    private static string Suffix(string path, int n)
    {
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        return $"{dir}/{Path.GetFileNameWithoutExtension(path)}_{n}{Path.GetExtension(path)}";
    }

    private static string UniqueIn(string folder, string file, List<Move> plan)
    {
        string to = $"{folder}/{file}";
        int n = 2;
        while (File.Exists(to) || plan.Any(m => m.to == to)) to = Suffix($"{folder}/{file}", n++);
        return to;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static int DeleteEmptyFolders(string folder)
    {
        int count = 0;
        foreach (string sub in AssetDatabase.GetSubFolders(folder)) count += DeleteEmptyFolders(sub);
        if (folder != Root && AssetDatabase.GetSubFolders(folder).Length == 0 && AssetDatabase.FindAssets("", new[] { folder }).Length == 0)
        {
            if (AssetDatabase.DeleteAsset(folder)) count++;
        }
        return count;
    }

    private static string Describe(List<Move> plan, List<string> skipped)
    {
        var sb = new StringBuilder($"[아이콘 폴더 정리] 미리보기 · {plan.Count}개 옮김 (아직 안 옮김)\n");
        foreach (var g in plan.GroupBy(m => Path.GetDirectoryName(m.to).Replace('\\', '/')))
        {
            sb.AppendLine($"■ {g.Key}");
            foreach (Move m in g) sb.AppendLine($"    {Path.GetFileName(m.to)}   ← {m.from}{m.note}");
        }
        if (skipped.Count > 0)
        {
            sb.AppendLine("■ 규칙에 없어서 그대로 두는 것");
            foreach (string s in skipped) sb.AppendLine("    " + s);
        }
        return sb.ToString();
    }
}
