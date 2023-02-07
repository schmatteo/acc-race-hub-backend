using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Configuration;

internal class ResultsHandler
{
    private static readonly string MongoURI = ConfigurationManager.AppSettings.Get("MongoURI") ?? "";

    public static void Handle(Results results)
    {
        switch (results?.SessionType)
        {
            case "Q":
                ResultsHandler.HandleQualifyingResults(results);
                break;
            case "R":
                ResultsHandler.HandleRaceResults(results);
                break;
            case "P":
                System.Console.Error.WriteLine("Practice session results handling is not currently supported");
                break;
            default:
                System.Console.Error.WriteLine("Unknown session type");
                break;
        }
    }

    private static async void HandleRaceResults(Results results)
    {
        MongoClientSettings settings = MongoClientSettings.FromConnectionString(MongoURI);
        settings.LinqProvider = LinqProvider.V3;
        MongoClient client = new(settings);
        IMongoDatabase database = client.GetDatabase("acc_race_hub");

        int lapCount = results.SessionResult.LeaderBoardLines[0].Timing.LapCount;
        int dnfLapCount = (int)(lapCount * 0.9);


        IMongoCollection<BsonDocument> raceCollection = database.GetCollection<BsonDocument>("race_results");
        IMongoCollection<BsonDocument> manufacturersCollection = database.GetCollection<BsonDocument>("manufacturers_standings");
        IMongoCollection<DriverInChampionshipStandings> driversCollection = database.GetCollection<DriverInChampionshipStandings>("drivers_standings");
        IMongoCollection<EntrylistEntry> entrylistCollection = database.GetCollection<EntrylistEntry>("entrylist");

        await InsertRaceIntoDatabase(raceCollection, results);
        await HandleManufacturersStandings(manufacturersCollection, results, dnfLapCount);
        await HandleIndividualResults(driversCollection, entrylistCollection, results, dnfLapCount);
        await HandleDropRound(driversCollection);
        // TODO: add teams points handling
    }

    private static async void HandleQualifyingResults(Results results)
    {
        MongoClientSettings settings = MongoClientSettings.FromConnectionString(MongoURI);
        settings.LinqProvider = LinqProvider.V3;
        MongoClient client = new(settings);
        IMongoDatabase database = client.GetDatabase("acc_race_hub");

        var collection = database.GetCollection<BsonDocument>("race_results");

        await InsertQualifyingIntoDatabase(collection, results);
    }

    private static async Task InsertRaceIntoDatabase(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonArray resultsToInsert = new();
        foreach (DriverResult driver in results.SessionResult.LeaderBoardLines)
        {
            DriverInRaceResults d = new()
            {
                PlayerId = driver.CurrentDriver.PlayerId,
                BestLap = driver.Timing.BestLap,
                LapCount = driver.Timing.LapCount,
                TotalTime = driver.Timing.TotalTime
            };
            _ = resultsToInsert.Add(d.ToBsonDocument());
        }

        BsonDocument searchString = new()
        {
            { "race", results.ServerName },
            { "track", results.TrackName }
        };
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Set("results", resultsToInsert);
        UpdateOptions options = new() { IsUpsert = true };

        _ = await collection.UpdateOneAsync(searchString, update, options);
        Console.WriteLine("Inserted race results into database");
    }

    private static async Task HandleManufacturersStandings(IMongoCollection<BsonDocument> collection, Results results, int dnfLapCount)
    {
        Dictionary<int, int> carPoints = new();
        foreach ((DriverResult driver, int index) in results.SessionResult.LeaderBoardLines.Select((item, index) => (item, index)))
        {
            if (index < 15)
            {
                if (driver.Timing.LapCount >= dnfLapCount)
                {
                    int car = driver.Car.CarModel;
                    int points = Maps.Points[index];
                    try
                    {
                        carPoints[car] += points;
                    }
                    catch (KeyNotFoundException)
                    {
                        carPoints.Add(car, points);
                    }
                }
            }
            else
            {
                break;
            }
        }

        UpdateOptions options = new() { IsUpsert = true };
        foreach (KeyValuePair<int, int> item in carPoints)
        {
            UpdateDefinition<BsonDocument> pointsToAdd = Builders<BsonDocument>.Update.Inc("points", item.Value);
            _ = await collection.UpdateOneAsync(new BsonDocument { { "car", Maps.Cars[item.Key] } }, pointsToAdd, options);
        }
    }

    private static async Task HandleIndividualResults(IMongoCollection<DriverInChampionshipStandings> collection, IMongoCollection<EntrylistEntry> entrylistCollection, Results results, int dnfLapCount)
    {
        var entrylist = await entrylistCollection.Find(_ => true).ToListAsync();

        var sortedRaceResults = results.SessionResult.LeaderBoardLines
            .Join(entrylist,
                result => result.CurrentDriver.PlayerId,
                entry => entry.Drivers.Select(d => d.PlayerID).FirstOrDefault(),
                (result, entry) => new { Result = result, Class = (Maps.Classes)entry.Drivers.FirstOrDefault(d => d.PlayerID == result.CurrentDriver.PlayerId).DriverCategory })
            .GroupBy(x => x.Class)
            .ToDictionary(x => x.Key, x => x.Select(x => x.Result).ToArray());
        var purples = GetFastestLap(sortedRaceResults);
        foreach (var entry in entrylist)
        {
            var driverInResults = from doc in results.SessionResult.LeaderBoardLines.AsQueryable()
                                  where doc.CurrentDriver.PlayerId == entry.Drivers[0].PlayerID
                                  select doc;

            DriverInChampionshipStandings driverToInsert = new() { PlayerId = entry.Drivers[0].PlayerID };
            DriverInChampionshipDefinitions updates;
            BsonDocument documentToInsert = new();

            if (driverInResults.Any())
            {
                bool dnf = driverInResults.First().Timing.LapCount < dnfLapCount;
                if (dnf)
                {
                    updates = new(0, -2, false, results.TrackName);
                }
                else
                {
                    int place = Array.FindIndex(sortedRaceResults[(Maps.Classes)entry.Drivers[0].DriverCategory], e => e == driverInResults.First());
                    int points = Maps.Points[place];


                    var purple = purples[(Maps.Classes)entry.Drivers[0].DriverCategory].CurrentDriver.PlayerId == entry.Drivers[0].PlayerID;
                    if (purple) points += 3;

                    updates = new(points, place + 1, purple, results.TrackName);
                }
            }
            else
            {
                updates = new(0, -1, false, results.TrackName);
            }

            var update = Builders<DriverInChampionshipStandings>.Update.Combine(
                updates.PointsDefinition, updates.FinishesDefinition
            );

            UpdateOptions options = new() { IsUpsert = true };
            await collection.UpdateOneAsync(new BsonDocument { { "playerId", entry.Drivers[0].PlayerID } }, update, options);
        }
    }

    private static async Task HandleDropRound(IMongoCollection<DriverInChampionshipStandings> collection)
    {
        var cursor = await collection.Find(_ => true).ToCursorAsync();

        try
        {
            while (cursor.MoveNext())
            {
                foreach (var driver in cursor.Current)
                {
                    if (driver.Finishes.Length > 1)
                    {
                        var finishesSorted = driver.Finishes.OrderBy(x => x.Points);
                        var worstFinish = finishesSorted.FirstOrDefault();
                        var droppedRound = Array.FindIndex(driver.Finishes, x => x == worstFinish);
                        int pointsWithDrop = finishesSorted.Skip(1).Sum(x => x.Points);

                        DropRoundDefinitions updates = new(droppedRound, pointsWithDrop);

                        var query = Builders<DriverInChampionshipStandings>.Update.Combine(updates.DropRoundIndex, updates.PointsWithDrop);
                        await collection.UpdateOneAsync(new BsonDocument { { "playerId", driver.PlayerId } }, query);
                    }
                }
            }
        }
        finally
        {
            cursor.Dispose();
        }
    }

    private static async Task InsertQualifyingIntoDatabase(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonArray resultsToInsert = new();
        foreach (var driver in results.SessionResult.LeaderBoardLines)
        {
            var query = from doc in results.Laps
                        where doc.CarId == driver.Car.CarId
                        where doc.IsValidForBest
                        select doc.Laptime;
            var laps = query.ToList();
            QualifyingResult result = new(driver.CurrentDriver.PlayerId, driver.Timing.BestLap, driver.Timing.LapCount, laps);
            resultsToInsert.Add(result.ToBsonDocument());
        }

        UpdateOptions options = new() { IsUpsert = true };
        var update = Builders<BsonDocument>.Update.Set("qualifyingResults", resultsToInsert);
        await collection.UpdateOneAsync(new BsonDocument { { "race", results.ServerName }, { "track", results.TrackName} }, update, options);
    }

    private static Dictionary<Maps.Classes, DriverResult> GetFastestLap(Dictionary<Maps.Classes, DriverResult[]> results)
    {
        Dictionary<Maps.Classes, DriverResult> fastest = new();
        foreach (var entry in results)
        {
            var purple = entry.Value.Min(x => x.Timing.BestLap);
            var holder = from doc in entry.Value
                         where doc.Timing.BestLap == purple
                         select doc;
            fastest.Add(entry.Key, holder.First());
        }
        return fastest;
    }

    private class DriverInRaceResults
    {
        [BsonElement("playerId")]
        public string PlayerId { get; set; }

        [BsonElement("bestLap")]
        public int BestLap { get; set; }

        [BsonElement("lapCount")]
        public int LapCount { get; set; }

        [BsonElement("totalTime")]
        public int TotalTime { get; set; }
    }

    private class EntrylistEntry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("drivers")]
        public Driver[] Drivers { get; set; }

        [BsonElement("raceNumber")]
        public int RaceNumber { get; set; }

        [BsonElement("forcedCarModel")]
        public int ForcedCarModel { get; }

        [BsonElement("overrideDriverInfo")]
        public int OverrideDriverInfo { get; set; }

        [BsonElement("defaultGridPosition")]
        public int DefaultGridPosition { get; set; }

        [BsonElement("isServerAdmin")]
        public int IsServerAdmin { get; set; }
    }

    private class Driver
    {
        [BsonElement("firstName")]
        public string FirstName { get; set; }

        [BsonElement("lastName")]
        public string LastName { get; set; }

        [BsonElement("shortName")]
        public string ShortName { get; set; }

        [BsonElement("nationality")]
        public int Nationality { get; set; }

        [BsonElement("driverCategory")]
        public int DriverCategory { get; set; }

        [BsonElement("playerID")]
        public string PlayerID { get; set; }
    }

    private class DriverInChampionshipStandings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("playerId")]
        public string PlayerId { get; set; }

        [BsonElement("points")]
        public int Points { get; set; }

        [BsonElement("pointsWDrop")]
        public int PointsWithDrop { get; set; }

        // track: [finishing_position (-1 = dns, -2 = dnf), fastest_lap?, points_for_the_round]
        [BsonIgnore]
        public Dictionary<string, Tuple<int, bool, int>> OldFinishes { get; set; } = new();

        public class Finish
        {
            [BsonElement("trackName")]
            public string TrackName { get; set; }

            [BsonElement("finishingPosition")]
            public int FinishingPosition { get; set; }

            [BsonElement("fastestLap")]
            public bool FastestLap { get; set; }

            [BsonElement("points")]
            public int Points { get; set; }
        }

        [BsonElement("finishes")]
        public Finish[] Finishes { get; set; }

        [BsonElement("roundDropped")]
        public int RoundDropped { get; set; }
    }

    private class DriverInChampionshipDefinitions
    {
        public UpdateDefinition<DriverInChampionshipStandings> PointsDefinition { get; }
        public UpdateDefinition<DriverInChampionshipStandings> FinishesDefinition { get; }

        public DriverInChampionshipDefinitions(int points, int finishingPosition, bool fastestLap, string trackName)
        {
            PointsDefinition = Builders<DriverInChampionshipStandings>.Update.Inc("points", points);
            var finishToPush = new DriverInChampionshipStandings.Finish() { TrackName = trackName, FinishingPosition = finishingPosition, FastestLap = fastestLap, Points = points };
            FinishesDefinition = Builders<DriverInChampionshipStandings>.Update.Push("finishes", finishToPush.ToBsonDocument());
        }
    }

    private class DropRoundDefinitions
    {
        public UpdateDefinition<DriverInChampionshipStandings> DropRoundIndex { get; }
        public UpdateDefinition<DriverInChampionshipStandings> PointsWithDrop { get; }
        public DropRoundDefinitions(int dropRoundIndex, int pointsWithDrop)
        {
            DropRoundIndex = Builders<DriverInChampionshipStandings>.Update.Set("roundDropped", dropRoundIndex);
            PointsWithDrop = Builders<DriverInChampionshipStandings>.Update.Set("pointsWDrop", pointsWithDrop);
        }
    }

    private class QualifyingResult
    {
        public string PlayerId { get; }
        public int BestLap { get; }
        public int LapCount { get; }
        public List<int> Laps { get; }

        public QualifyingResult(string playerId, int bestLap, int lapCount, List<int> laps)
        {
            PlayerId = playerId;
            BestLap = bestLap;
            LapCount = lapCount;
            Laps = laps;
        }
    }
}