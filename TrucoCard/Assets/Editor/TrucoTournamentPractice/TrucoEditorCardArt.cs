using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Loads the exact sprite references used by the live Gameplay scene without opening it.</summary>
public static class TrucoEditorCardArt
{
    static Dictionary<CardSuit, List<Sprite>> _faces;
    public static Sprite Face(DeckCards card)
    {
        if (card == null) return null;
        Load();
        int index = card.rank <= 7 ? card.rank - 1 : card.rank - 3;
        return _faces.TryGetValue(card.suit, out var sprites) && index >= 0 && index < sprites.Count ? sprites[index] : null;
    }
    static void Load()
    {
        if (_faces != null) return;
        _faces = new Dictionary<CardSuit, List<Sprite>>();
        string scriptGuid = Regex.Match(File.ReadAllText("Assets/Scripts/Card.cs.meta"), @"guid: (\w+)").Groups[1].Value;
        string source = File.ReadAllText("Assets/Prefabs/Card.prefab");
        string data = Regex.Matches(source, @"--- !u!114 &[^\n]+\n(.*?)(?=\n--- |\z)", RegexOptions.Singleline)
            .Cast<Match>().Select(m => m.Value).First(b => b.Contains("guid: " + scriptGuid) && Regex.IsMatch(b, @"swordsSprite:\r?\n  -"));
        foreach (var entry in new[] { (CardSuit.Swords, "swordsSprite"), (CardSuit.Clubs, "clubsSprite"), (CardSuit.Cups, "cupsSprite"), (CardSuit.Coins, "coinsSprite") })
        {
            var list = new List<Sprite>();
            string refs = Regex.Match(data, @"(?m)^  " + entry.Item2 + @":\r?\n((?:  - [^\n]+(?:\r?\n|\z))+)").Groups[1].Value;
            foreach (Match reference in Regex.Matches(refs, @"fileID: (-?\d+), guid: (\w+)"))
            {
                long id = long.Parse(reference.Groups[1].Value);
                string path = AssetDatabase.GUIDToAssetPath(reference.Groups[2].Value);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                var face = sprites.FirstOrDefault(sprite => {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId); return localId == id;
                });
                // Some legacy single-card textures retain a sliced local ID in their importer.
                // The Gameplay scene overrides that stale prefab ID with the same sole sprite.
                list.Add(face != null ? face : sprites.Length == 1 ? sprites[0] : null);
            }
            _faces[entry.Item1] = list;
        }
    }
}
