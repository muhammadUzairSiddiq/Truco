using TMPro;
using UnityEngine;

public class Userbar : MonoBehaviour
{
    [SerializeField] private TMP_Text userID;
    [SerializeField] private TMP_Text coins;

    private string _userId;
    private UserStats _stats;
    public void InitializeUserBar(UserStats stats)
    {
        _stats = stats;
        coins.text = _stats.coins.ToString();
    }

    public void RemoveUser()
    {
        
    }

    public void EditUser()
    {
    }
    
}
