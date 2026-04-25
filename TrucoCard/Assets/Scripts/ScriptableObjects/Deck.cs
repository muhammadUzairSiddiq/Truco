using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "DeckData", menuName = "ScriptableObjects/MakeDeck", order = 1)]
public class Deck : ScriptableObject
{
    [SerializeField] private DeckCards[] cards;
    
    public DeckCards[] Cards => cards;

    public void Shuffle()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int j = Random.Range(0, cards.Length);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    public void InitializeDeck()
    {
        string json = Resources.Load<TextAsset>("spanish_deck").text;
        Debug.LogWarning("Json is " + json);
        DeckCardsWrapper wrapper = JsonUtility.FromJson<DeckCardsWrapper>(json);
        cards = wrapper.cards.ToArray();
    }

    public void ResetDeck()
    {
        string json = Resources.Load<TextAsset>("spanish_deck").text;
        DeckCardsWrapper wrapper = JsonUtility.FromJson<DeckCardsWrapper>(json);
        cards = wrapper.cards.ToArray();
    }
    
    public DeckCards DrawRandomCard()
    {
        if (cards.Length == 0)
        {
            ResetDeck();
        }
        int randomIndex = Random.Range(0, cards.Length);
        DeckCards drawnCard = cards[randomIndex];
        
        // Remove the drawn card from the deck
        DeckCards[] newCards = new DeckCards[cards.Length - 1];
        for (int i = 0, j = 0; i < cards.Length; i++)
        {
            if (i != randomIndex)
            {
                newCards[j++] = cards[i];
            }
        }
        cards = newCards;
        return drawnCard;
    }
}


[System.Serializable]
public class DeckCardsWrapper
{
    public List<DeckCards> cards;
}

[System.Serializable]
public class DeckCards
{
    public int id;
    public CardSuit suit; // This will automatically map to enum if JSON uses ints
    public int rank;
    public string name;
}