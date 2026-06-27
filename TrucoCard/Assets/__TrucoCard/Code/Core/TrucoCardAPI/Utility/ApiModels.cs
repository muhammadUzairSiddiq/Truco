using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All API DTO models (from Swagger) combined into one file.
/// </summary>

[Serializable]
public class Tournament
{
    public string _id;
    public string name;
    public string description;
    public string type;
    public int entryFee;
    public int maxPlayers;
    public string status;
    public List<User> participants;
    public List<User> champion;

    public string startDate;
    public string endDate;
    public int tournamentAwardPercentage;
    public int prizePool;
    public List<match> matches;

}

[Serializable]
public class TournamentsListResponse
{
    public bool success;
    public List<Tournament> tournaments;
}

[Serializable]
public class  Participants
{
    public string id;
    public string name;
    public string email;
}

/// <summary>Nested tournament on a match (id only) — avoid referencing full <see cref="Tournament"/> which would cycle with <c>Tournament.matches</c> and break Unity serialization.</summary>
[Serializable]
public class MatchTournamentRef
{
    public string _id;
    public string name;
}

[Serializable]
public class match
{
    public string _id;
    public MatchTournamentRef tournament;
    public List<User> players;
    public string status;
    public User winner;
    public string finishedAt;
    public string createdAt;
}


[Serializable]
public class User
{
    public string _id;
    public string username;
    public string email;
    public string role;
    public string avatar;
    public bool emailVerified;
    public string status;
    public wallet wallet;
    public stats stats;
    public string createdAt;
}

[Serializable]
public class UserProfileResponse
{
    public bool success;
    public User user;
}

[Serializable]
    public class wallet
    {
        public int balance = 100; 
    }

    [Serializable]
    public class stats
    {
        public int wins;
        public int losses;
        public int matchesPlayed;
    }


    [Serializable]
    public class TransactionDto
    {
        public string _id;
        public User user;
        public string type;
        public int amount;
        public string reason;
        public int balanceBefore;
        public int balanceAfter;
        public TransactionMetaDto meta;
        public string createdAt;
    }

    [Serializable]
    public class TransactionMetaDto
    {
    }

    [Serializable]
    public class AlertDto
    {
        public string _id;
        public string type;
        public string severity;
        public string title;
        public string description;
        public User player;
        public match match;
        public Tournament tournament;
        public AlertMetadataDto metadata;
        public string status;
        public User acknowledgedBy;
        public string acknowledgedAt;
        public User resolvedBy;
        public string resolvedAt;
        public string resolution;
        public string source;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    public class AlertMetadataDto
    {
        // Store unknown/dynamic values
        public string rawJson;
    }


[System.Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[Serializable]
public class RegisterRequest
{
    public string name;
    public string email;
    public string password;
}

[Serializable]
public class RegisterResponse
{
    public string id;
    public string username;
    public string email;
    public string message;

    public string AuthToken { get { return id; } }
}

[Serializable]
public class EmailBodyRequest
{
    public string email;
}

[Serializable]
public class OTPRequest
{
    public string email;
    public string otp;
}




[Serializable]
public class EnterTournamentResponse
{
    public bool ok;
    public int coins;
}

[System.Serializable]
public class ValidatePrivateTournamentRequest
{
    public string password;
}
[Serializable]
public class  ValidatePrivateTournamentResponse
{
    public bool success;
    public bool valid;
}

[Serializable]
public class CreateTournamentMatchResponse
{
    public bool ok;
    public match match;
}

[Serializable]
public class CreateTournamentMatchRequest
{
    public List<string> participants;
    public int entryFee;
    public string gameType;
}


[System.Serializable]
public class AdminCheckResponse
{
    public bool ok;
    public string role;
}

[System.Serializable]
public class FinalizeChampionRequest
{
    public string championId;
}

[Serializable]
public class CreateTournamentRequestAdmin
{
    public string name;
    public string description;
    public string startDate;
    public string endDate;
    public int maxPlayers;
    public int entryFee;
}

// --- 1v1 player rooms (GET /matches, POST /matches/player-create, join, photon-room) ---

[Serializable]
public class PlayerCreateMatchRequest
{
    public string name;
    /// <summary>Backend expects "public" or "private" (not bool isPublic).</summary>
    public string type;
    public int cost;
    public int prize;
    public int maxPlayers;
    public string password;
    /// <summary>Game variant: true = Con Flor, false = Sin Flor.</summary>
    public bool withFlor = true;
}

[Serializable]
public class PlayerMatchJoinRequest
{
    public string password;
}

/// <summary>Register Photon custom room name with backend (admin can trace exact room).</summary>
[Serializable]
public class RegisterPhotonRoomRequest
{
    public string photonRoomName;
    public string roomName;
}

[Serializable]
public class MatchResultSubmitRequest
{
    public string winnerId;
    public string status;
}

[Serializable]
public class Player1v1Match
{
    public string _id;
    public string name;
    public int entryFee;
    public bool isPublic;
    public int maxPlayers;
    public string status;
    public string photonRoomName;
    public string photonRoom;
    public string roomName;
    public string passwordRequired;
    public int playerCount;
    public int currentPlayers;
    public string createdBy;
    /// <summary>Some APIs send host id as <c>hostId</c> instead of <c>createdBy</c>.</summary>
    public string hostId;
    /// <summary>Only the host / creator should receive this; joiners never get it in list payloads (backend as authority).</summary>
    public string joinCode;
    /// <summary>Opcional: "public" / "private" si el API no usa solo isPublic (bool).</summary>
    public string access;
    /// <summary>API: "public" | "private".</summary>
    public string type;
    public int cost;
    public int prize;
    /// <summary>Game variant from backend: true = Con Flor, false = Sin Flor.</summary>
    public bool withFlor = true;
    public User[] players;
}

[Serializable]
public class PlayerCreateMatchResponse
{
    public bool ok;
    public bool success;
    public string message;
    public string error;
    public Player1v1Match match;
}

[Serializable]
public class PlayerJoinMatchResponse
{
    public bool ok;
    public bool success;
    public string message;
    public string error;
    public Player1v1Match match;
    public int coins;
}

/// <summary>Join response when match is nested under <c>data</c> (same style as match list).</summary>
[Serializable]
public class PlayerJoinMatchDataInner
{
    public Player1v1Match match;
    public int coins;
}

[Serializable]
public class PlayerJoinMatchWrappedRoot
{
    public bool ok;
    public bool success;
    public string message;
    public string error;
    public PlayerJoinMatchDataInner data;
}

/// <summary>Join response when the API puts the full match object directly in <c>data</c>.</summary>
[Serializable]
public class PlayerJoinMatchFlattenedDataRoot
{
    public bool ok;
    public bool success;
    public string message;
    public string error;
    public Player1v1Match data;
}

[Serializable]
public class MatchesListEnvelope
{
    public bool success;
    public bool ok;
    public Player1v1Match[] matches;
}

[Serializable]
public class MatchesListDataInner
{
    public Player1v1Match[] matches;
}

[Serializable]
public class MatchesListDataRoot
{
    public MatchesListDataInner data;
    public Player1v1Match[] matches;
}