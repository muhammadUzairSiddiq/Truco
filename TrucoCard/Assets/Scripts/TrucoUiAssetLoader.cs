using UnityEngine;

/// <summary>Loads Truco UI sprites from Resources/TrucoUI at runtime (build-safe).</summary>
public static class TrucoUiAssetLoader
{
    static Sprite _panel;
    static Sprite _greenBtn;
    static Sprite _usFlag;
    static Sprite _esFlag;

    public static Sprite Panel
    {
        get
        {
            if (_panel == null) _panel = LoadSprite("TrucoUI/Panel");
            return _panel;
        }
    }

    public static Sprite GreenButton
    {
        get
        {
            if (_greenBtn == null) _greenBtn = LoadSprite("TrucoUI/GreenBTN");
            return _greenBtn;
        }
    }

    public static Sprite UsFlag
    {
        get
        {
            if (_usFlag == null) _usFlag = BuildUsFlag();
            return _usFlag;
        }
    }

    public static Sprite EsFlag
    {
        get
        {
            if (_esFlag == null) _esFlag = BuildEsFlag();
            return _esFlag;
        }
    }

    static Sprite LoadSprite(string resourcesPath)
    {
        var sprites = Resources.LoadAll<Sprite>(resourcesPath);
        if (sprites != null && sprites.Length > 0) return sprites[0];
        var tex = Resources.Load<Texture2D>(resourcesPath);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildUsFlag()
    {
        const int w = 64;
        const int h = 44;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var red = new Color(0.78f, 0.12f, 0.18f, 1f);
        var white = Color.white;
        var blue = new Color(0.12f, 0.22f, 0.48f, 1f);
        for (int y = 0; y < h; y++)
        {
            bool stripeWhite = (y / 3) % 2 == 0;
            for (int x = 0; x < w; x++)
            {
                if (x < 26 && y > h - 22) tex.SetPixel(x, y, blue);
                else tex.SetPixel(x, y, stripeWhite ? white : red);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildEsFlag()
    {
        const int w = 64;
        const int h = 44;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var red = new Color(0.76f, 0.12f, 0.18f, 1f);
        var yellow = new Color(0.95f, 0.76f, 0.12f, 1f);
        for (int y = 0; y < h; y++)
        {
            Color c;
            float t = y / (float)(h - 1);
            if (t < 0.25f || t > 0.75f) c = red;
            else c = yellow;
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
