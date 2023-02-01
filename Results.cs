class Results
{
    public string? sessionType { get; set; }
    public string? trackName { get; set; }
    public int sessionIndex { get; set; }
    public int raceWeekendIndex { get; set; }
    public string? metaData { get; set; }
    public string? serverName { get; set; }
    public SessionResult? sessionResult { get; set; }
    public Lap[]? laps { get; set; }
    public Penalty[]? penalties { get; set; }
    public object[]? post_race_penalties { get; set; }
}

class SessionResult
{
    public int bestlap { get; set; }
    public int[]? bestSplits { get; set; }
    public int isWetSession { get; set; }
    public int type { get; set; }
    public DriverResult[]? leaderBoardLines { get; set; }
}

class DriverResult
{
    public Car? car { get; set; }
    public Driver? currentDriver { get; set; }
    public int currentDriverIndex { get; set; }
    public Timing? timing { get; set; }
    public int missingMandatoryPitstop { get; set; }
    public float[]? driverTotalTimes { get; set; }
}

class Car
{
    public int carId { get; set; }
    public int raceNumber { get; set; }
    public int carModel { get; set; }
    public int cupCategory { get; set; }
    public string? carGroup { get; set; }
    public string? teamName { get; set; }
    public int nationality { get; set; }
    public int carGuid { get; set; }
    public int teamGuid { get; set; }
    public Driver[]? drivers { get; set; }
}

class Driver
{
    public string? firstName { get; set; }
    public string? lastName { get; set; }
    public string? shortName { get; set; }
    public string? playerId { get; set; }
}

class Timing
{
    public int lastLap { get; set; }
    public int?[]? lastSplits { get; set; }
    public int bestLap { get; set; }
    public int[]? bestSplits { get; set; }
    public int totalTime { get; set; }
    public int lapCount { get; set; }
    public long lastSplitId { get; set; }
}

class Lap
{
    public int carId { get; set; }
    public int driverIndex { get; set; }
    public int laptime { get; set; }
    public bool isValidForBest { get; set; }
    public int[]? splits { get; set; }
}

class Penalty
{
    public int carId { get; set; }
    public int driverIndex { get; set; }
    public string? reason { get; set; }
    public string? penalty { get; set; }
    public int penaltyValue { get; set; }
    public int violationInLap { get; set; }
    public int clearedInLap { get; set; }
}
