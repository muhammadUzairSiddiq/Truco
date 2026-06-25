using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for ALL backend endpoints. Edit this asset (Resources/TrucoApiEndpoints)
/// to change the base URL or to add/override any route WITHOUT touching code.
///
/// When the backend dev gives you a new route (e.g. the "match end" API), just add a row in
/// <see cref="endpoints"/> with the matching key (e.g. <c>MatchEnd</c>) and its path
/// (e.g. <c>/matches/{0}/end</c>). <see cref="ApiConfig"/> will pick it up automatically.
///
/// Use <c>{0}</c> as the id placeholder inside a path template.
/// </summary>
[CreateAssetMenu(fileName = "TrucoApiEndpoints", menuName = "TrucoCard/API Endpoints", order = 0)]
public class TrucoApiEndpointsSO : ScriptableObject
{
    [Header("Base URL (no trailing slash)")]
    [Tooltip("e.g. https://srv983121.hstgr.cloud/api")]
    public string baseUrl = "https://srv983121.hstgr.cloud/api";

    [System.Serializable]
    public class Endpoint
    {
        [Tooltip("Logical key. Known keys: MatchList, PlayerCreateMatch, PlayerJoinMatch, MatchRegisterPhotonRoom, MatchSubmitResult, MatchPlayerLeave, MatchEnd, GetMatch, GetMyMatches, EnterTournament, ValidatePrivateTournament, FinalizeTournament, FinalizeMatch, CreateTournamentMatch. You may also add brand-new keys and read them with ApiConfig.Custom(key, args).")]
        public string key;

        [Tooltip("Path that begins with '/'. Use {0} where an id goes. Example: /matches/{0}/end")]
        public string pathTemplate;
    }

    [Header("Endpoint overrides / new backend routes")]
    [Tooltip("Add the match-end route here when the backend dev provides it: key = MatchEnd, path = /matches/{0}/end")]
    public List<Endpoint> endpoints = new List<Endpoint>();

    public bool TryGet(string key, out string template)
    {
        template = null;
        if (endpoints == null || string.IsNullOrEmpty(key)) return false;
        for (int i = 0; i < endpoints.Count; i++)
        {
            var e = endpoints[i];
            if (e != null && e.key == key && !string.IsNullOrEmpty(e.pathTemplate))
            {
                template = e.pathTemplate;
                return true;
            }
        }
        return false;
    }
}
