namespace MH.Multiplayer
{
    using Photon.Pun;
    using Photon.Realtime;

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

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (_isTournamentModeActive && IsTournamentRoom())
            {
                MultiplayerController.OnPlayerLeftRoom(otherPlayer);
            }
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
            if (_isTournamentModeActive)
            {
                DisableTournamentMode();
            }
        }

        public override void OnConnectedToMaster()
        {
            if (_isTournamentModeActive)
            {
                MultiplayerController.OnConnectedToMasterForTournament();
            }
        }

        #endregion

        #region Utility Methods

        private bool IsTournamentRoom()
        {
            if (PhotonNetwork.CurrentRoom?.CustomProperties == null) return false;

            return PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("tournamentId") ||
                   PhotonNetwork.CurrentRoom.Name.Contains("TournamentMatchmaking") ||
                   PhotonNetwork.CurrentRoom.Name.Contains("TournamentMatch");
        }

        #endregion
    }
}