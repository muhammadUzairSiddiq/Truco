using UnityEngine;

/// <summary>
/// Runtime slices from the Truco gameplay / tournament sprite sheet
/// (Resources/TrucoUI/TrucoGameplayUI). Presentation only — never used by rules.
/// </summary>
public static class TrucoGameplayUiSprites
{
    static Sprite[] _all;
    static bool _tried;

    public static Sprite Back => Get(1);
    public static Sprite Refresh => Get(0);
    public static Sprite Home => Get(2);
    public static Sprite CoinsTab => Get(3);
    public static Sprite Friends => Get(4);
    public static Sprite Bell => Get(5);
    public static Sprite Laurel => Get(6);
    public static Sprite Corner => Get(7);
    public static Sprite DividerThin => Get(8);
    public static Sprite Divider => Get(9);
    public static Sprite PanelGreen => Get(10);
    public static Sprite BarDark => Get(11);
    public static Sprite PanelBrown => Get(12);
    public static Sprite BarBrown => Get(13);
    public static Sprite PanelGrey => Get(14);
    public static Sprite ButtonGreenLong => Get(15);
    public static Sprite ButtonGreenLongAlt => Get(16);
    public static Sprite BarGold => Get(17);
    public static Sprite ButtonGreen => Get(18);
    public static Sprite ButtonGold => Get(19);
    public static Sprite ButtonRound => Get(20);
    public static Sprite IconPlayers => Get(21);
    public static Sprite IconFlor => Get(22);
    public static Sprite IconCoins => Get(23);
    public static Sprite IconTrophy => Get(24);
    public static Sprite IconGroup => Get(25);
    public static Sprite IconLock => Get(26);
    public static Sprite IconLockOpen => Get(27);
    public static Sprite IconEye => Get(28);
    public static Sprite IconChevron => Get(30);
    public static Sprite IconPerson => Get(31);
    public static Sprite IconCards => Get(32);
    public static Sprite IconCoin => Get(33);

    public static Sprite Get(int index)
    {
        Ensure();
        if (_all == null || index < 0 || index >= _all.Length) return null;
        return _all[index];
    }

    static void Ensure()
    {
        if (_tried) return;
        _tried = true;
        var loaded = Resources.LoadAll<Sprite>("TrucoUI/TrucoGameplayUI");
        if (loaded == null || loaded.Length == 0)
            loaded = Resources.LoadAll<Sprite>("TrucoUI/truco gameplay UI");
        if (loaded == null || loaded.Length == 0) return;
        System.Array.Sort(loaded, (a, b) => TrailingIndex(a != null ? a.name : "").CompareTo(TrailingIndex(b != null ? b.name : "")));
        _all = loaded;
    }

    static int TrailingIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return int.MaxValue;
        int us = name.LastIndexOf('_');
        if (us < 0 || us + 1 >= name.Length) return int.MaxValue;
        return int.TryParse(name.Substring(us + 1), out int n) ? n : int.MaxValue;
    }

    public static void Apply(ImageLike img, Sprite sprite, bool sliced = false)
    {
        if (img.image == null || sprite == null) return;
        img.sprite = sprite;
        img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        img.color = Color.white;
        img.preserveAspect = !sliced;
    }

    public struct ImageLike
    {
        public UnityEngine.UI.Image image;
        public Sprite sprite
        {
            get => image != null ? image.sprite : null;
            set { if (image != null) image.sprite = value; }
        }
        public UnityEngine.UI.Image.Type type
        {
            get => image != null ? image.type : UnityEngine.UI.Image.Type.Simple;
            set { if (image != null) image.type = value; }
        }
        public Color color
        {
            get => image != null ? image.color : Color.white;
            set { if (image != null) image.color = value; }
        }
        public bool preserveAspect
        {
            get => image != null && image.preserveAspect;
            set { if (image != null) image.preserveAspect = value; }
        }
        public static implicit operator ImageLike(UnityEngine.UI.Image img) => new ImageLike { image = img };
    }
}
