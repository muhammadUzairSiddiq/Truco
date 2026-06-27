using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Reemplaza al panel clásico "Looking for match" en el flujo 1v1: espera y cuenta 3-2-1 antes de Gameplay (solo el master carga la escena).</summary>
public class OneVsOnePhotonSessionUi : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private Image _panelBackground;

    void Awake()
    {
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }

    public void SetRuntimeWiring(GameObject overlayRoot, TMP_Text status, TMP_Text countdown, Image panelBg = null)
    {
        _overlayRoot = overlayRoot;
        _statusText = status;
        _countdownText = countdown;
        _panelBackground = panelBg != null ? panelBg : overlayRoot != null ? overlayRoot.GetComponent<Image>() : null;
    }

    /// <summary>Llamado al entrar a la sala Photon (1 jugador aún).</summary>
    public void Initialize()
    {
        StopAllCoroutines();
        if (_overlayRoot == null) return;
        _overlayRoot.SetActive(true);
        if (_countdownText != null) _countdownText.text = string.Empty;
        if (_statusText != null)
        {
            _statusText.text = TrucoTextosClient.EsperandoRivalSala;
            _statusText.color = Color.white;
        }
        ApplyPanelStyle(waiting: true);
    }

    /// <summary>Segundo jugador listo: cuenta y carga partida.</summary>
    public void MatchFound()
    {
        StopAllCoroutines();
        if (_overlayRoot != null) _overlayRoot.SetActive(true);
        if (_statusText != null)
        {
            _statusText.text = TrucoTextosClient.CargandoJuego + "\n" + TrucoTextosClient.PartidaEncontrada;
            _statusText.color = Color.white;
        }
        if (_countdownText != null)
        {
            _countdownText.text = "3";
            _countdownText.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        }
        ApplyPanelStyle(waiting: false);
        if (_panelBackground != null) TrucoUiMotion.PopIn(_panelBackground.transform, 0.3f, 0.85f);
        StartCoroutine(CountdownRoutine());
    }

    void ApplyPanelStyle(bool waiting)
    {
        if (_panelBackground == null && _overlayRoot != null)
            _panelBackground = _overlayRoot.GetComponent<Image>();
        if (_panelBackground == null) return;

        var panel = TrucoUiAssetLoader.Panel;
        if (panel != null)
        {
            _panelBackground.sprite = panel;
            _panelBackground.type = Image.Type.Sliced;
            _panelBackground.color = new Color(1f, 1f, 1f, 0.97f);
        }
        else
        {
            _panelBackground.color = waiting
                ? new Color(0.1f, 0.22f, 0.14f, 0.95f)
                : new Color(0.12f, 0.32f, 0.2f, 0.96f);
        }

        if (_statusText != null)
        {
            _statusText.color = Color.white;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.enableAutoSizing = true;
            _statusText.fontSizeMin = 20f;
            _statusText.fontSizeMax = 32f;
        }
        if (_countdownText != null)
        {
            _countdownText.color = TrucoUiTheme.EntryPrizeAccent;
            _countdownText.fontStyle = FontStyles.Bold;
            _countdownText.fontSize = 64f;
        }
    }

    IEnumerator CountdownRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (_countdownText != null) _countdownText.text = "2";
        yield return new WaitForSeconds(1f);
        if (_countdownText != null) _countdownText.text = "1";
        yield return new WaitForSeconds(1f);
        LoadGameplayScene();
    }

    static void LoadGameplayScene()
    {
        if (!PhotonNetwork.InRoom) return;
        if (PhotonNetwork.IsMasterClient)
            TrucoSceneTransition.GoPhoton("Gameplay");
        else if (SceneManager.GetActiveScene().name != "Gameplay")
            TrucoSceneTransition.Go("Gameplay");
    }

    public void HideOverlay()
    {
        StopAllCoroutines();
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }

    public void ResetState()
    {
        StopAllCoroutines();
        HideOverlay();
    }
}
