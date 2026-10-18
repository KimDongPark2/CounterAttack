using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// TopHUD 폴더에 들어온 그림을 자동으로 UI 스프라이트로 설정
// (Sprite 타입, 9-slice 테두리, 압축 끔)
// 그림을 넣기 전에 이 스크립트를 먼저 넣어두기. 이미 넣었다면 TopHUD 폴더 우클릭 → Reimport
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
    };

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/TopHUD/")) return;

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
        s.wrapMode = name == "day_stripe" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        s.spriteBorder = Borders.TryGetValue(name, out Vector4 b) ? b : Vector4.zero;
        ti.SetTextureSettings(s);

        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
