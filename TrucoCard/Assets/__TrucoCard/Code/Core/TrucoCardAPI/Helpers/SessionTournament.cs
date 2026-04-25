using System;
using System.Collections.Generic;
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

        foreach (Tournament tournament in tournamentDtos)
        {
            if (tournament.status == "registration")
            {
                _activeTournaments.Add(tournament);
            }
        }
    }
}
