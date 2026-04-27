using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
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
        if (_overlayRoot == null) return;
        _overlayRoot.SetActive(true);
        if (_countdownText != null) _countdownText.text = string.Empty;
        if (_statusText != null)
        {
            _statusText.text = TrucoTextosClient.EsperandoRivalSala;
            _statusText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
        }
        ApplyPanelStyle(waiting: true);
    }

    /// <summary>Segundo jugador listo: cuenta y carga partida.</summary>
    public void MatchFound()
    {
        if (_statusText != null)
        {
            _statusText.text = TrucoTextosClient.CargandoJuego + "\n" + TrucoTextosClient.PartidaEncontrada;
            _statusText.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        }
        if (_countdownText != null)
        {
            _countdownText.text = "3";
            _countdownText.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        }
        ApplyPanelStyle(waiting: false);
        StartCoroutine(CountdownRoutine());
    }

    void ApplyPanelStyle(bool waiting)
    {
        if (_panelBackground == null) return;
        if (waiting)
        {
            _panelBackground.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            return;
        }
        // Clear “loading / starting” card
        _panelBackground.color = new Color(0.98f, 0.99f, 1f, 0.98f);
    }

    IEnumerator CountdownRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (_countdownText != null) _countdownText.text = "2";
        yield return new WaitForSeconds(1f);
        if (_countdownText != null) _countdownText.text = "1";
        yield return new WaitForSeconds(1f);
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.LoadLevel("Gameplay");
    }

    public void HideOverlay()
    {
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }
}
