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

        IMongoCollection<BsonDocument> raceCollection = database.GetCollection<BsonDocument>("race_results");
        IMongoCollection<BsonDocument> manufacturersCollection = database.GetCollection<BsonDocument>("manufacturers_standings");
        IMongoCollection<DriverInChampionshipStandings> driversCollection = database.GetCollection<DriverInChampionshipStandings>("drivers_standings");
        IMongoCollection<EntrylistEntry> entrylistCollection = database.GetCollection<EntrylistEntry>("entrylist");

        //await InsertRaceIntoDatabase(raceCollection, results);
        //await HandleManufacturersStandings(manufacturersCollection, results);
        await HandleIndividualResults(driversCollection, entrylistCollection, results);
    }

    private static void HandleQualifyingResults(Results results)
    {
        // TODO: handle quali results
        System.Console.WriteLine("q");
        System.Console.WriteLine(results?.ServerName);
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

    private static async Task HandleManufacturersStandings(IMongoCollection<BsonDocument> collection, Results results)
    {
        int totalLaps = results.SessionResult.LeaderBoardLines[0].Timing.LapCount;
        Dictionary<int, int> carPoints = new();
        foreach ((DriverResult driver, int index) in results.SessionResult.LeaderBoardLines.Select((item, index) => (item, index)))
        {
            if (index < 15)
            {
                if (driver.Timing.LapCount >= totalLaps - 5)
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

    private static async Task HandleIndividualResults(IMongoCollection<DriverInChampionshipStandings> collection, IMongoCollection<EntrylistEntry> entrylistCollection, Results results)
    {
        var entrylistQuery = (from doc in entrylistCollection.AsQueryable() select doc).Distinct();
        var entrylist = entrylistQuery.ToList();

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
            BsonDocument documentToInsert = new();
            UpdateDefinition<DriverInChampionshipStandings> pointsToInc;

            if (driverInResults.Any())
            {
                int place = Array.FindIndex(sortedRaceResults[(Maps.Classes)entry.Drivers[0].DriverCategory], e => e == driverInResults.First());
                int points = Maps.Points[place];


                var purple = purples[(Maps.Classes)entry.Drivers[0].DriverCategory].CurrentDriver.PlayerId == entry.Drivers[0].PlayerID;
                if (purple) points += 3;

                //documentToInsert.Add("$inc", new BsonDocument { { "points", points } });
                //documentToInsert.Add("$push", new BsonDocument { { "finishes", new BsonDocument { { "$set", new BsonDocument { { results.ServerName, new Tuple<int, bool, int>(place, purple, points) } } } } });
                //driverToInsert.Finishes.Add(results.ServerName, new Tuple<int, bool, int>(place, purple, points));

                //Builders<DriverInChampionshipStandings>.Update.Set()
                Console.WriteLine(driverInResults.First().CurrentDriver.LastName);
                Console.WriteLine(points);
            }
            else
            {
                driverToInsert.Finishes.Add(results.ServerName, new Tuple<int, bool, int>(-1, false, 0));
                driverToInsert.Points = 0;
                pointsToInc = Builders<DriverInChampionshipStandings>.Update.Inc("points", 0);
            }

            //var update = Builders<DriverInChampionshipStandings>.Update.Combine(
            //    pointsToInc, driverToInsert.ToBsonDocument()
            //);

            UpdateOptions options = new() { IsUpsert = true };
            //await collection.UpdateOneAsync(new BsonDocument { { "playerId", entry.Drivers[0].PlayerID } }, update, options);
        }

        //Dictionary<Maps.Classes, List<DriverInChampionshipStandings>> resultsToInsert = new();
        //foreach ((DriverResult driver, int index) in results.SessionResult.LeaderBoardLines.Select((item, index) => (item, index)))
        //{
        //    IMongoQueryable<Entrylist> query = from doc in entrylistCollection.AsQueryable()
        //                                       where doc.Drivers[0].PlayerID == driver.CurrentDriver.PlayerId
        //                                       select doc;
        //    if (query.Any())
        //    {
        //        IMongoQueryable<DriverInChampionshipStandings> findPlayer = from doc in collection.AsQueryable()
        //                                                                    where doc.PlayerId == driver.CurrentDriver.PlayerId
        //                                                                    select doc;

        //        if (findPlayer.Any())
        //        {

        //        }

        //        //try
        //        //{
        //        //    resultsToInsert[(Maps.Classes)query.FirstOrDefault().Drivers[0].DriverCategory].Add(new DriverInChampionshipStandings { PlayerId = driver.CurrentDriver.PlayerId });
        //        //}
        //        //catch (KeyNotFoundException)
        //        //{
        //        //    resultsToInsert.Add((Maps.Classes)query.FirstOrDefault().Drivers[0].DriverCategory, new List<DriverInChampionshipStandings> { new DriverInChampionshipStandings { PlayerId = driver.CurrentDriver.PlayerId } });
        //        //}
        //    }
        //}
        //foreach (var entry in resultsToInsert[(Maps.Classes)1])
        //{
        //    Console.WriteLine(entry.PlayerId);
        //}
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

        // track: [finishing_position, fastest_lap?, points_for_the_round]
        [BsonElement("finishes")]
        public Dictionary<string, Tuple<int, bool, int>> Finishes { get; set; } = new();

        [BsonElement("roundDropped")]
        public int RoundDropped { get; set; }
    }
}