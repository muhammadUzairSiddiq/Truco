using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive, safe-area layout for confirmation, notification and progress prompts.</summary>
public static class TrucoThemeRuntime
{
    public sealed class Prompt
    {
        public TextMeshProUGUI Title { get; internal set; }
        public Button Secondary { get; internal set; }
        public Button Primary { get; internal set; }
        internal TextMeshProUGUI Body;
        internal ScrollRect Scroll;

        public void SetBody(string message)
        {
            Body.text = message;
            Canvas.ForceUpdateCanvases();
            if (Scroll == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(Scroll.content);
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1f;
        }
    }

    public static Prompt BuildPrompt(RectTransform root, int order, bool loading = false, bool twoActions = false)
    {
        foreach (Transform child in root) child.gameObject.SetActive(false);

        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (root.GetComponent<GraphicRaycaster>() == null) root.gameObject.AddComponent<GraphicRaycaster>();
        var scrim = root.GetComponent<Image>();
        if (scrim == null) scrim = root.gameObject.AddComponent<Image>();
        TrucoMenuTheme.Surface(scrim, new Color(0.075f, 0.045f, 0.03f, .72f), false);
        scrim.raycastTarget = true;

        var safe = TrucoClubUI.Rect("PromptSafeArea", root, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<TrucoSafeArea>();
        float bottom = loading ? .38f : .29f;
        var frame = TrucoClubUI.Panel("PromptFrame", safe, TrucoClubUI.Raised,
            new Vector2(.055f, bottom), new Vector2(.945f, 1f - bottom));
        var card = TrucoClubUI.Panel("PromptCard", frame.transform, TrucoClubUI.Ink,
            Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3)).rectTransform;

        var prompt = new Prompt();
        prompt.Title = TrucoClubUI.Text(card, "", 48, TrucoClubUI.Gold,
            new Vector2(.045f, loading ? .56f : .73f), new Vector2(.955f, .94f), true, TextAlignmentOptions.Center);
        prompt.Title.name = "PromptTitle";
        prompt.Title.fontSizeMin = 28;
        prompt.Title.richText = false;
        prompt.Title.overflowMode = TextOverflowModes.Ellipsis;

        var rule = TrucoClubUI.Panel("PromptRule", card, TrucoClubUI.Raised,
            new Vector2(.09f, loading ? .54f : .72f), new Vector2(.91f, loading ? .54f : .72f),
            new Vector2(0, -1), new Vector2(0, 1));
        rule.raycastTarget = false;

        if (loading)
        {
            prompt.Body = TrucoClubUI.Text(card, "", 38, TrucoClubUI.Cream,
                new Vector2(.06f, .09f), new Vector2(.94f, .51f), false, TextAlignmentOptions.Center);
        }
        else
        {
            var viewport = TrucoClubUI.Rect("PromptBodyViewport", card, new Vector2(.075f, .31f), new Vector2(.925f, .69f));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            prompt.Scroll = viewport.gameObject.AddComponent<ScrollRect>();
            prompt.Scroll.viewport = viewport;
            prompt.Scroll.horizontal = false;
            prompt.Scroll.movementType = ScrollRect.MovementType.Clamped;
            prompt.Scroll.scrollSensitivity = 40;
            prompt.Body = TrucoClubUI.Text(viewport, "", 39, TrucoClubUI.Cream,
                new Vector2(0, 1), Vector2.one, false, TextAlignmentOptions.Top);
            var body = prompt.Body.rectTransform;
            body.pivot = new Vector2(.5f, 1);
            body.offsetMin = body.offsetMax = Vector2.zero;
            prompt.Body.enableAutoSizing = false;
            prompt.Body.fontSize = 39;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            prompt.Scroll.content = body;

            if (twoActions)
                prompt.Secondary = AddAction(card, "PromptSecondary", .055f, .49f, true);
            prompt.Primary = AddAction(card, "PromptPrimary", twoActions ? .51f : .18f, twoActions ? .945f : .82f, false);
        }
        prompt.Body.name = "PromptBody";
        prompt.Body.richText = false;
        return prompt;
    }

    static Button AddAction(RectTransform card, string name, float left, float right, bool secondary)
    {
        var button = TrucoClubUI.Button(card, name, null, new Vector2(left, .065f), new Vector2(right, .265f), secondary);
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSizeMin = 21;
        label.fontSizeMax = 35;
        label.richText = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return button;
    }

    public static void SetAction(Button button, string label, System.Action onClick)
    {
        button.GetComponentInChildren<TextMeshProUGUI>().text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}
