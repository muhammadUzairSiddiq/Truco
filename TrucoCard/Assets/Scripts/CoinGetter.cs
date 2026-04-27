using TMPro;
using UnityEngine;

public class CoinGetter : MonoBehaviour
{
    [Tooltip("Must be the coin balance label only. Do not use on titles, usernames, or notification text — it overwrites TMP with wallet balance.")]
    [SerializeField] private TMP_Text coins;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InvokeRepeating(nameof(GetCoins), 0.1f, 0.1f);
    }

    private void GetCoins()
    {
        if (ApiController.GetSessionUser.Data.wallet != null)
        {
            coins.text = ApiController.GetSessionUser.Data.wallet.balance.ToString();
        }
        else
        {
            coins.text = "0";
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
