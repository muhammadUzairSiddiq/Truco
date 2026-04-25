using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>Reemplaza al panel clásico "Looking for match" en el flujo 1v1: espera y cuenta 3-2-1 antes de Gameplay (solo el master carga la escena).</summary>
public class OneVsOnePhotonSessionUi : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _countdownText;

    void Awake()
    {
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }

    public void SetRuntimeWiring(GameObject overlayRoot, TMP_Text status, TMP_Text countdown)
    {
        _overlayRoot = overlayRoot;
        _statusText = status;
        _countdownText = countdown;
    }

    /// <summary>Llamado al entrar a la sala Photon (1 jugador aún).</summary>
    public void Initialize()
    {
        if (_overlayRoot == null) return;
        _overlayRoot.SetActive(true);
        if (_countdownText != null) _countdownText.text = string.Empty;
        if (_statusText != null) _statusText.text = TrucoTextosClient.BuscandoOponente;
    }

    /// <summary>Segundo jugador listo: cuenta y carga partida.</summary>
    public void MatchFound()
    {
        if (_statusText != null) _statusText.text = TrucoTextosClient.PartidaEncontrada;
        if (_countdownText != null) _countdownText.text = "3";
        StartCoroutine(CountdownRoutine());
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
