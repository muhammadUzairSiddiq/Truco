namespace MH.Multiplayer
{
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;

    public class MultiplayerCallbacks : MonoBehaviourPunCallbacks
    {
        #region Private Variables

        private bool _isTournamentModeActive = false;
        private double _nextTransportTick;

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

        void Update()
        {
            if (!_isTournamentModeActive || Time.realtimeSinceStartupAsDouble < _nextTransportTick) return;
            _nextTransportTick = Time.realtimeSinceStartupAsDouble + 0.5;
            MultiplayerController.TickAssignedTransport();
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
            if (_isTournamentModeActive)
                MultiplayerController.ValidateRoom();
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!_isTournamentModeActive) return;
            MultiplayerController.ValidateRoom();
            _ = MultiplayerController.RefreshAuthoritativeStateAsync();
        }

        public override void OnJoinRoomFailed(short code, string message)
        {
            if (_isTournamentModeActive) MultiplayerController.OnAssignedRoomFailed("join", code);
        }

        public override void OnCreateRoomFailed(short code, string message)
        {
            if (_isTournamentModeActive) MultiplayerController.OnAssignedRoomFailed("create", code);
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

        #endregion

        #region Photon Callbacks - Connection Events

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_isTournamentModeActive && MultiplayerController.IsTournamentActive)
            {
                MultiplayerController.OnAssignedRoomDisconnected(cause);
                return;
            }
            if (_isTournamentModeActive)
                DisableTournamentMode();
        }

        public override void OnConnectedToMaster()
        {
            TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
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
