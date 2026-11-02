using UnityEngine;

// 이번 판을 하는 사람 (명찰에서 적은 이름 + 찍은 사진)
// static이라 씬이 바뀌어도 그대로 남음 (Intro → Game → 결과)
// 다른 스크립트에서:  PlayerSession.Name,  PlayerSession.Photo
public static class PlayerSession
{
    public static string Name { get; private set; } = "";
    public static Texture2D Photo { get; private set; }           // 사진을 안 찍었으면 null
    public static string Id { get; private set; } = "";            // 같은 사람이 "다시 하기"를 해도 같은 값 (순위표에서 한 사람 = 한 줄)
    public static bool Active => !string.IsNullOrEmpty(Name);

    // 명찰에서 "근무 시작"을 누르면 부름 (새 사람)
    public static void Begin(string name, Texture2D photo)
    {
        Clear();
        Name = name;
        Photo = photo;
        Id = System.Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    // 처음으로 돌아가서 다음 사람이 할 때 (사진은 메모리에서 지움)
    public static void Clear()
    {
        if (Photo != null) Object.Destroy(Photo);
        Photo = null;
        Name = "";
        Id = "";
    }
}
