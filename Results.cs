#pragma warning disable 8618

using System.Text.Json.Serialization;

internal class Results
{
    [JsonPropertyName("sessionType")]
    public string SessionType { get; set; }

    [JsonPropertyName("trackName")]
    public string TrackName { get; set; }

    [JsonPropertyName("sessionIndex")]
    public int SessionIndex { get; set; }

    [JsonPropertyName("raceWeekendIndex")]
    public int RaceWeekendIndex { get; set; }

    [JsonPropertyName("metaData")]
    public string MetaData { get; set; }

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; }

    [JsonPropertyName("sessionResult")]
    public SessionResult SessionResult { get; set; }

    [JsonPropertyName("laps")]
    public Lap[] Laps { get; set; }

    [JsonPropertyName("penalties")]
    public CPenalty[] Penalties { get; set; }

    [JsonPropertyName("post_race_penalties")]
    public object[] PostRacePenalties { get; set; }
}

internal class SessionResult
{
    [JsonPropertyName("bestlap")]
    public int BestLap { get; set; }

    [JsonPropertyName("bestSplits")]
    public int[] BestSplits { get; set; }

    [JsonPropertyName("isWetSession")]
    public int IsWetSession { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("leaderBoardLines")]
    public DriverResult[] LeaderBoardLines { get; set; }
}

internal class DriverResult
{
    [JsonPropertyName("car")]
    public Car Car { get; set; }

    [JsonPropertyName("currentDriver")]
    public Driver CurrentDriver { get; set; }

    [JsonPropertyName("currentDriverIndex")]
    public int CurrentDriverIndex { get; set; }

    [JsonPropertyName("timing")]
    public Timing Timing { get; set; }

    [JsonPropertyName("missingMandatoryPitstop")]
    public int MissingMandatoryPitstop { get; set; }

    [JsonPropertyName("driverTotalTimes")]
    public float[] DriverTotalTimes { get; set; }
}

internal class Car
{
    [JsonPropertyName("carId")]
    public int CarId { get; set; }

    [JsonPropertyName("raceNumber")]
    public int RaceNumber { get; set; }

    [JsonPropertyName("carModel")]
    public int CarModel { get; set; }

    [JsonPropertyName("cupCategory")]
    public int CupCategory { get; set; }

    [JsonPropertyName("carGroup")]
    public string CarGroup { get; set; }

    [JsonPropertyName("teamName")]
    public string TeamName { get; set; }

    [JsonPropertyName("nationality")]
    public int Nationality { get; set; }

    [JsonPropertyName("carGuid")]
    public int CarGuid { get; set; }

    [JsonPropertyName("teamGuid")]
    public int TeamGuid { get; set; }

    [JsonPropertyName("drivers")]
    public Driver[] Drivers { get; set; }
}

internal class Driver
{
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string LastName { get; set; }

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; }

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; }
}

internal class Timing
{
    [JsonPropertyName("lastLap")]
    public int LastLap { get; set; }

    [JsonPropertyName("lastSplits")]
    public int[] LastSplits { get; set; }

    [JsonPropertyName("bestLap")]
    public int BestLap { get; set; }

    [JsonPropertyName("bestSplits")]
    public int[] BestSplits { get; set; }

    [JsonPropertyName("totalTime")]
    public int TotalTime { get; set; }

    [JsonPropertyName("lapCount")]
    public int LapCount { get; set; }

    [JsonPropertyName("lastSplitId")]
    public long LastSplitId { get; set; }
}

internal class Lap
{
    [JsonPropertyName("carId")]
    public int CarId { get; set; }

    [JsonPropertyName("driverIndex")]
    public int DriverIndex { get; set; }

    [JsonPropertyName("laptime")]
    public int Laptime { get; set; }

    [JsonPropertyName("isValidForBest")]
    public bool IsValidForBest { get; set; }

    [JsonPropertyName("splits")]
    public int[] Splits { get; set; }
}

internal class CPenalty
{
    [JsonPropertyName("carId")]
    public int CarId { get; set; }

    [JsonPropertyName("driverIndex")]
    public int DriverIndex { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }

    [JsonPropertyName("penalty")]
    public string Penalty { get; set; }

    [JsonPropertyName("penaltyValue")]
    public int PenaltyValue { get; set; }

    [JsonPropertyName("violationInLap")]
    public int ViolationInLap { get; set; }

    [JsonPropertyName("clearedInLap")]
    public int ClearedInLap { get; set; }
}

#pragma warning restore 8618