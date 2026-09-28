namespace MH.Multiplayer
{
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;

    public class MultiplayerCallbacks : MonoBehaviourPunCallbacks
    {
        #region Private Variables

        private bool _isTournamentModeActive = false;

        #endregion

        #region Tournament Mode Control

        public void EnableTournamentMode()
        {
            _isTournamentModeActive = true;
        }

        public void DisableTournamentMode()
        {
            _isTournamentModeActive = false;
        }

        #endregion

        #region Photon Callbacks - Room Events

        public override void OnJoinedRoom()
        {
            if (_isTournamentModeActive && IsTournamentRoom())
            {
                MultiplayerController.OnJoinedRoom();
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (_isTournamentModeActive && IsTournamentRoom())
            {
                MultiplayerController.OnPlayerEnteredRoom(newPlayer);
            }
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if (_isTournamentModeActive && changedProps.ContainsKey("tournamentRegistered"))
                MultiplayerController.ValidateRoom();
        }

        public override void OnJoinRoomFailed(short code, string message)
        {
            if (!_isTournamentModeActive) return;
            // A failed rejoin attempt must not erase the active match while the reconnect
            // manager still owns its retry window and cached room identity.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Gameplay"
                && MultiplayerController.ActiveMatch != null) return;
            MultiplayerController.LeaveTournamentMatchmaking();
            AppManager.Instance?.DisplayNotification("No se pudo entrar al torneo: " + message);
        }

        public override void OnCreateRoomFailed(short code, string message)
        {
            if (!_isTournamentModeActive) return;
            MultiplayerController.LeaveTournamentMatchmaking();
            AppManager.Instance?.DisplayNotification("No se pudo crear la mesa del torneo. Inscripción/reembolso pendientes de revisión: " + message);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (_isTournamentModeActive && IsTournamentRoom())
            {
                MultiplayerController.OnPlayerLeftRoom(otherPlayer);
            }
        }

        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            if (_isTournamentModeActive)
                MultiplayerController.OnRoomPropertiesUpdate(propertiesThatChanged);
        }

        public override void OnLeftRoom()
        {
            if (_isTournamentModeActive)
            {
                MultiplayerController.OnLeftRoom();
            }
        }

        #endregion

        #region Photon Callbacks - Connection Events

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_isTournamentModeActive && MultiplayerController.IsTournamentActive)
                return;
            if (_isTournamentModeActive)
                DisableTournamentMode();
        }

        public override void OnConnectedToMaster()
        {
            TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
            if (_isTournamentModeActive)
            {
                MultiplayerController.OnConnectedToMasterForTournament();
            }
        }

        #endregion

        #region Utility Methods

        private bool IsTournamentRoom()
        {
            if (PhotonNetwork.CurrentRoom == null) return false;
            string name = PhotonNetwork.CurrentRoom.Name ?? "";
            if (name.Contains("TournamentMatchmaking") || name.Contains("TournamentMatch")
                || name.StartsWith("TMatch_") || name.StartsWith("THub_"))
                return true;
            return PhotonNetwork.CurrentRoom.CustomProperties != null
                   && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("tournamentId");
        }

        #endregion
    }
}
