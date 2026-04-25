using UnityEngine;
using UnityEngine.UI;

public class AllTournamentsOverlay : MonoBehaviour
{
    [SerializeField] private TournamentCardAdminItem _tournamentCardAdminItemPrefab;
    [SerializeField] private RectTransform _tournamentCardParentTransform;
    [SerializeField] private Button _closeBtn;

    private void Start()
    {
        _closeBtn.onClick.RemoveAllListeners();
        _closeBtn.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
        });
    }

    public void Init(Tournament[] tournaments)
    {
        foreach (Transform child in _tournamentCardParentTransform)
        {
            Destroy(child.gameObject);
        }
        foreach (var tournament in tournaments)
        {
            var tournamentCard = Instantiate(_tournamentCardAdminItemPrefab, _tournamentCardParentTransform);
            tournamentCard.Init(tournament);
        }

        AppManager.Instance.HideLoadingUI();

        gameObject.SetActive(true);

        _tournamentCardParentTransform.anchoredPosition = new Vector2(_tournamentCardParentTransform.anchoredPosition.x, 0);

    }

}
