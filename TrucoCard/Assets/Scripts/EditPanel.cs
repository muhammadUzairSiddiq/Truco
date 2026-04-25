using TMPro;
using UnityEngine;

public class EditPanel : MonoBehaviour
{
    [SerializeField] private GameObject usersPanel;
    [SerializeField] private TMP_Text userIdText;
    [SerializeField] private TMP_InputField coinsField;
    [SerializeField] private TMP_Text winsText;
    [SerializeField] private TMP_Text gamesPlayedText;
    [SerializeField] private TMP_Text losesText;
    [SerializeField] private TMP_Text drawsText;

    private UserStats _stats;
    
    public void SetupPanel(UserStats stats)
    {
        _stats = stats;
        coinsField.text = stats.coins.ToString();
        winsText.text = stats.totalGamesWon.ToString();
        gamesPlayedText.text = stats.totalGamesPlayed.ToString();
        losesText.text = stats.totalGamesLost.ToString();
        drawsText.text = stats.totalGamesDrawn.ToString();
    }

    public void SaveData()
    {
        _stats.coins = int.Parse(coinsField.text);
        gameObject.SetActive(false);
        usersPanel.SetActive(true);
    }

    public void Cancel()
    {
        gameObject.SetActive(false);
        usersPanel.SetActive(true);
    }
    
}
