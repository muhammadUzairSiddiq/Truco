namespace MH.Multiplayer
{
    using Photon.Pun;
    using System;
    using UnityEngine;

    /// <summary>Compatibility RPCs accept only the complete, already-published opening plan.</summary>
    public class MultiplayerRPC : MonoBehaviourPunCallbacks
    {
        [Serializable] sealed class OpeningTables { public TournamentMatch[] tables; }

        [PunRPC]
        public void CreateTournamentMatch(string opponentId, string nextMatch, PhotonMessageInfo info)
        {
            // The current controller creates the full opening plan. The obsolete per-opponent
            // RPC must never create another paid match or invent a room after a failed request.
            Debug.LogWarning("[MultiplayerRPC] Legacy per-opponent creation ignored; a confirmed opening plan is required.");
        }

        [PunRPC]
        public void BeginTournamentTable(string matchJson, string bracketJson, PhotonMessageInfo info) =>
            JoinPublishedOpeningTable(matchJson, info);

        [PunRPC]
        public void JoinTournamentMatch(string targetMatch, string nextMatch, PhotonMessageInfo info) =>
            JoinPublishedOpeningTable(targetMatch, info);

        static void JoinPublishedOpeningTable(string matchJson, PhotonMessageInfo info)
        {
            var request = MultiplayerController.CurrentTournamentRequest;
            var room = PhotonNetwork.CurrentRoom;
            if (request == null || room == null || info.Sender == null || PhotonNetwork.MasterClient == null
                || info.Sender.ActorNumber != PhotonNetwork.MasterClient.ActorNumber
                || !room.CustomProperties.TryGetValue("tournamentId", out object tournament)
                || tournament as string != request.TournamentId) return;
            // A master RPC is merely a refresh hint. Its JSON can never assign a table,
            // alter a pairing or finalize a result without an authenticated server snapshot.
            _ = MultiplayerController.RefreshAuthoritativeStateAsync();
        }
    }
}
