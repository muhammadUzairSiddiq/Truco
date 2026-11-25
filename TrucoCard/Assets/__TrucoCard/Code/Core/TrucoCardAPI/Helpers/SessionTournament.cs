using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SessionTournament : MonoBehaviour
{
    [SerializeField] private List<Tournament> _activeTournaments = new List<Tournament>();

    public IReadOnlyList<Tournament> TournamentsList => _activeTournaments;
    public Tournament[] TournamentsArray => _activeTournaments.ToArray();

    public void UpdateTournaments(List<Tournament> tournamentDtos)
    {
        Debug.Log("[SessionTournament] - Updated active tournaments count: " + tournamentDtos.Count);

        _activeTournaments.Clear();

        string playerId = ApiController.GetSessionUser?.Data?._id;
        _activeTournaments.AddRange(tournamentDtos.Where(t => IsBrowsable(t, playerId))
            .OrderByDescending(t => t.HasPlayer(playerId)));
    }

    public static bool IsBrowsable(Tournament tournament, string playerId) => tournament != null
        && (tournament.status == "registration" || tournament.status == "starting" || tournament.status == "playing"
            || (tournament.status == "active" || tournament.status == "reconciliation_required") && tournament.HasPlayer(playerId));
}
