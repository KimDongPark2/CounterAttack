using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 로컬 순위표 (이 컴퓨터에만 저장)
// 저장 위치: Application.persistentDataPath/Ranking/
//   ranking.json  ← 이름 · 점수 · 별 · 매출 · 날짜 · 사진 파일 이름
//   photos/*.jpg  ← 순위 안에 든 사람 사진만
// 한 사람(명찰 한 번) = 한 줄. "다시 하기"로 또 하면 더 높을 때만 바뀜
// 다른 스크립트에서:  RankingStore.Entries (1등부터),  RankingStore.LoadPhoto(entry)
public static class RankingStore
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string name;
        public int score;
        public int stars;
        public int revenue;
        public int served;
        public string date;
        public string photo;   // 사진 파일 이름 (사진이 없으면 "")
    }

    [Serializable]
    private class Data
    {
        public List<Entry> entries = new List<Entry>();
    }

    // 등록 결과
    public struct Result
    {
        public int rank;        // 1부터. 순위 밖이면 0
        public bool improved;   // 이번 판으로 순위표에 새로 들어갔거나 자기 기록을 넘었는지
        public bool newTop;     // 1등을 새로 차지했는지 (신기록)
    }

    public const int MaxEntries = 10;
    public static string Folder => Path.Combine(Application.persistentDataPath, "Ranking");
    private static string FilePath => Path.Combine(Folder, "ranking.json");
    private static string PhotoFolder => Path.Combine(Folder, "photos");

    private static Data cache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => cache = null;

    // 1등부터 점수 높은 순
    public static List<Entry> Entries
    {
        get { Load(); return cache.entries; }
    }

    public static int TopScore => Entries.Count > 0 ? Entries[0].score : 0;

    // ---------- 등록 ----------
    public static Result Submit(string id, string name, int score, int stars, int revenue, int served, Texture2D photo)
    {
        Load();
        List<Entry> list = cache.entries;
        Result result = new Result();
        int prevTop = list.Count > 0 ? list[0].score : int.MinValue;

        Entry mine = list.Find(e => e.id == id);
        if (mine != null && mine.score >= score)
        {
            result.rank = list.IndexOf(mine) + 1;   // 자기 기록을 못 넘음 → 그대로
            return result;
        }

        if (mine == null) mine = new Entry { id = id, photo = "" };
        else list.Remove(mine);

        mine.name = name;
        mine.score = score;
        mine.stars = stars;
        mine.revenue = revenue;
        mine.served = served;
        mine.date = DateTime.Now.ToString("MM-dd HH:mm");

        // 같은 점수면 먼저 한 사람이 위
        int index = list.FindIndex(e => e.score < score);
        if (index < 0) index = list.Count;
        list.Insert(index, mine);

        // 10등 밖으로 밀려난 줄 지우기 (사진도 같이)
        while (list.Count > MaxEntries)
        {
            Entry last = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            DeletePhoto(last);
        }

        if (!list.Contains(mine))
        {
            Save();
            return result;   // 순위 밖
        }

        if (photo != null) SavePhoto(mine, photo);

        result.rank = list.IndexOf(mine) + 1;
        result.improved = true;
        result.newTop = result.rank == 1 && score > prevTop;
        Save();
        return result;
    }

    // ---------- 사진 ----------
    // 순위표 화면에서 사진 불러오기 (다 쓰면 Object.Destroy로 지우기). 사진이 없으면 null
    public static Texture2D LoadPhoto(Entry e)
    {
        if (e == null || string.IsNullOrEmpty(e.photo)) return null;
        string path = Path.Combine(PhotoFolder, e.photo);
        if (!File.Exists(path)) return null;
        try
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Ranking] 사진을 못 읽었어요: " + ex.Message);
        }
        return null;
    }

    private static void SavePhoto(Entry e, Texture2D photo)
    {
        try
        {
            Directory.CreateDirectory(PhotoFolder);
            string file = e.id + ".jpg";
            File.WriteAllBytes(Path.Combine(PhotoFolder, file), photo.EncodeToJPG(85));
            e.photo = file;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Ranking] 사진을 저장 못 했어요: " + ex.Message);
        }
    }

    private static void DeletePhoto(Entry e)
    {
        if (e == null || string.IsNullOrEmpty(e.photo)) return;
        try
        {
            string path = Path.Combine(PhotoFolder, e.photo);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Ranking] 사진을 못 지웠어요: " + ex.Message);
        }
    }

    // ---------- 전부 지우기 (시연 끝나고) ----------
    public static void Clear()
    {
        try
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Ranking] 순위표 폴더를 못 지웠어요: " + ex.Message);
        }
        cache = new Data();
        PlayerPrefs.DeleteKey("BestScore");   // 인트로의 최고 점수도 같이
        PlayerPrefs.Save();
        Debug.Log("[Ranking] 순위표와 사진을 전부 지웠어요.");
    }

    // ---------- 파일 ----------
    private static void Load()
    {
        if (cache != null) return;
        cache = new Data();
        try
        {
            if (File.Exists(FilePath))
            {
                Data d = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                if (d != null && d.entries != null)
                {
                    d.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));
                    d.entries = d.entries.OrderByDescending(e => e.score).ToList();   // 같은 점수는 원래 순서 그대로
                    cache = d;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Ranking] 순위표 파일을 못 읽어서 새로 시작해요: " + ex.Message);
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonUtility.ToJson(cache, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Ranking] 순위표를 저장 못 했어요: " + ex.Message);
        }
    }
}
