using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Assets/Icon (또는 TopHUD 폴더)에 들어온 그림을 자동으로 UI 스프라이트로 설정
// (Sprite 타입, 9-slice 테두리, 압축 끔)
// 그림을 넣기 전에 이 스크립트를 먼저 넣어두기. 이미 넣었다면 폴더 우클릭 → Reimport
// 에디터 전용 스크립트라 Assets/Editor 폴더에 있어야 게임 빌드할 때 오류가 안 남
public class TopHUDSpriteImporter : AssetPostprocessor
{
    // 왼쪽, 아래, 오른쪽, 위 (픽셀)
    private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "day_bar_mask",   new Vector4(26, 26, 26, 26) },
        { "day_bar_border", new Vector4(26, 26, 26, 26) },
        { "day_bubble",     new Vector4(20, 26, 20, 20) },
        { "hud_chip",       new Vector4(46, 58, 46, 46) },
        { "hud_pill",       new Vector4(22, 22, 22, 22) },
        { "info_round",     new Vector4(20, 20, 20, 20) },
        { "intro_receipt",  new Vector4(0, 44, 0, 24) },   // 위아래 톱니만 고정, 세로로만 늘리기
        { "intro_laser",    new Vector4(24, 0, 24, 0) },   // 양 끝만 고정, 가로로 늘리기
        { "shutter_card",   new Vector4(60, 76, 60, 60) }, // 아래는 그림자까지
    };

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Icon/") && !path.Contains("/TopHUD/")) return;

        TextureImporter ti = (TextureImporter)assetImporter;
        string name = Path.GetFileNameWithoutExtension(path);

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.textureType = TextureImporterType.Sprite;
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = 100f;
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.mipmapEnabled = false;
        s.alphaIsTransparency = true;
        // 줄무늬·점선처럼 반복(Tiled)해서 쓰는 그림은 Repeat (shutter_stripe도 여기 해당)
        bool repeat = name.Contains("stripe") || name.Contains("dash") || name.Contains("dots");
        s.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        // 목록에 있는 그림만 테두리를 넣음 (나머지는 Sprite Editor에서 직접 넣은 값 유지)
        if (Borders.TryGetValue(name, out Vector4 b)) s.spriteBorder = b;
        ti.SetTextureSettings(s);

        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
