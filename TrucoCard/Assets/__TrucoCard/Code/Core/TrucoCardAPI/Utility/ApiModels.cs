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


[Serializable]
public class match
{
    public string _id;
    public Tournament tournament;
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