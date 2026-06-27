using TMPro;
using UnityEngine;

public class CoinGetter : MonoBehaviour
{
    [Tooltip("Must be the coin balance label only. Do not use on titles, usernames, or notification text — it overwrites TMP with wallet balance.")]
    [SerializeField] private TMP_Text coins;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InvokeRepeating(nameof(GetCoins), 0.5f, 2f);
    }

    private void GetCoins() => RefreshNow();

    public void RefreshNow()
    {
        if (coins == null) return;
        var wallet = ApiController.GetSessionUser?.Data?.wallet;
        coins.text = wallet != null ? wallet.balance.ToString() : "0";
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
