namespace MH.Multiplayer
{
    using Photon.Pun;
    using Photon.Realtime;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    public class MultiplayerRPC : MonoBehaviourPunCallbacks
    {
        #region Tournament RPC Methods

        [PunRPC]
        public void CreateTournamentMatch(string opponentId, string nextMatch)
        {
            CreateAndAskOpponentToJoin(opponentId, nextMatch);

        }

        [PunRPC]
        public void JoinTournamentMatch(string targetMatch, string nextMatch)
        {
            MultiplayerController.JoinSpecificTournamentMatch(JsonUtility.FromJson<TournamentMatch>(targetMatch), JsonUtility.FromJson<TournamentMatch>(nextMatch));
        }

        private async void CreateAndAskOpponentToJoin(string opponentId, string nextMatch)
        {
            Player opponentPlayer = FindPlayerByUserId(opponentId);
            TournamentMatch newMatch = new TournamentMatch();

            List<string> playersId = new List<string> { ApiController.GetSessionUser.Data._id, opponentId };
            List<string> playersName = new List<string> { ApiController.GetSessionUser.Data.username, opponentPlayer.NickName };

            match createdMatchViaApi = await ApiController.CreateTournamentMatch(MultiplayerController.CurrentTournamentRequest.TournamentId, playersId);

            if(createdMatchViaApi != null)
            {
                newMatch = new TournamentMatch
                {
                    matchId = createdMatchViaApi._id,
                    playerIds = playersId,
                    playerNames = playersName,
                    roomName = createdMatchViaApi._id
                };
            }
            else
            {
                newMatch = new TournamentMatch
                {
                    matchId = null,
                    playerIds = playersId,
                    playerNames = playersName,
                    roomName = $"Tournament_{string.Join("vs",playersName)}_{Guid.NewGuid()}"
                };
            }

            photonView.RPC("JoinTournamentMatch", opponentPlayer, JsonUtility.ToJson(newMatch), nextMatch);

            LeanTween.delayedCall(5, () =>
            {
                MultiplayerController.JoinSpecificTournamentMatch(newMatch, JsonUtility.FromJson<TournamentMatch>(nextMatch));
            });

        }

        #endregion

        #region Utility Methods

        private string GetPlayerInitials(string username)
        {
            if (string.IsNullOrEmpty(username))
                return "XX";

            string[] words = username.Split(new char[] { ' ', '_', '-', '.', '@', '#', '$', '%', '^', '&', '*', '(', ')', '+', '=', '{', '}', '[', ']', '|', '\\', ':', ';', '"', '\'', '<', '>', ',', '?', '/', '~', '`' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return "XX";

            if (words.Length == 1)
            {
                string word = words[0];
                return word.Length > 8 ? word.Substring(0, 8) : word;
            }

            string firstName = words[0];
            string secondWord = words[1];
            string secondWordFirstLetter = secondWord.Length > 0 ? secondWord[0].ToString().ToUpper() : "";
            int secondWordLength = secondWord.Length;

            return firstName + secondWordFirstLetter + secondWordLength;
        }

        private Player FindPlayerByUserId(string userId)
        {
            return PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(player =>
            {
                // First try to match against custom properties "userId"
                if (player.CustomProperties.TryGetValue("userId", out object customUserId))
                {
                    return customUserId.ToString() == userId;
                }

                // Fallback to Photon's built-in UserId property
                return player.UserId == userId;
            });
        }

        #endregion
    }
}