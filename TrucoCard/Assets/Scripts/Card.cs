using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Card : MonoBehaviour,IPointerDownHandler
{
    public CardSuit suit;
    public int value;
    
    
    [SerializeField] private bool isDummy;
    [SerializeField] private Sprite whiteCard;
    [SerializeField] private List<Sprite> swordsSprite;
    [SerializeField] private List<Sprite> clubsSprite;
    [SerializeField] private List<Sprite> coinsSprite;
    [SerializeField] private List<Sprite> cupsSprite;
    
    
    
    private bool _canSelect = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _canSelect = !isDummy;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetupCard(CardSuit cardSuit, int cardValue)
    {
        suit = cardSuit;
        value = cardValue;
        var img = GetComponent<Image>();
        if (img == null) return;
        Sprite s = GetSprite(cardSuit, cardValue);
        // Never clear a good face with null — that leaves a blank white Image (disconnect regression).
        if (s != null)
            img.sprite = s;
        img.color = Color.white;
    }

    private Sprite GetSprite(CardSuit cardSuit, int cardValue)
    {
        switch (cardSuit)
        {
            case CardSuit.Clubs:
            {
                if (cardValue <= 7)
                {
                    return clubsSprite[cardValue - 1];
                }
                else
                {
                    return clubsSprite[cardValue - 3]; 
                }
            }
            case CardSuit.Coins:
            {
                if (cardValue <= 7)
                {
                    return coinsSprite[cardValue - 1];
                }
                else
                {
                    return coinsSprite[cardValue - 3]; 
                }
            }
            case CardSuit.Cups:
            {
                if (cardValue <= 7)
                {
                    return cupsSprite[cardValue - 1];
                }
                else
                {
                    return cupsSprite[cardValue - 3]; 
                }
            }
            case CardSuit.Swords:
            {
                if (cardValue <= 7)
                {
                    return swordsSprite[cardValue - 1];
                }
                else
                {
                    return swordsSprite[cardValue - 3]; 
                }
            }
        }

        return null;
    }

    public bool CanUserSelect() => _canSelect;

    public void SetSelectable(bool can) => _canSelect = can;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance.IsMyTurn() && _canSelect && GameManager.Instance.CanPlayCard())
        {
            _canSelect = false;
            GameManager.Instance.CardSelected(transform,suit, value);
        }
    }

    public void CommitPlayForTimeout()
    {
        if (!GameManager.Instance.IsMyTurn() || !GameManager.Instance.CanPlayCard() || !_canSelect) return;
        _canSelect = false;
        GameManager.Instance.CardSelected(transform, suit, value);
    }
}

public enum CardSuit
{
    Swords,
    Clubs,
    Cups,
    Coins
}