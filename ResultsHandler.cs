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
        IMongoCollection<BsonDocument> driversCollection = database.GetCollection<BsonDocument>("drivers_standings");
        IMongoCollection<Entrylist> entrylistCollection = database.GetCollection<Entrylist>("entrylist");

        await InsertRaceIntoDatabase(raceCollection, results);
        await HandleManufacturersStandings(manufacturersCollection, results);
        HandleIndividualResults(driversCollection, entrylistCollection, results);
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
                    int points = Maps.Points[index + 1];
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
            _ = await collection.UpdateOneAsync(new BsonDocument { { "car", Maps.Cars[item.Key] } }, pointsToAdd, options);
        }
    }

    private static void HandleIndividualResults(IMongoCollection<BsonDocument> collection, IMongoCollection<Entrylist> entrylistCollection, Results results)
    {
        Dictionary<Maps.Classes, List<DriverInChampionshipStandings>> resultsToInsert = new();
        foreach (DriverResult driver in results.SessionResult.LeaderBoardLines)
        {
            IMongoQueryable<Entrylist> query = from doc in entrylistCollection.AsQueryable()
                                               where doc.Drivers[0].PlayerID == driver.CurrentDriver.PlayerId
                                               select doc;
            if (query.Any())
            {
                try
                {
                    resultsToInsert[(Maps.Classes)query.FirstOrDefault().Drivers[0].DriverCategory].Add(new DriverInChampionshipStandings { PlayerId = driver.CurrentDriver.PlayerId });
                }
                catch (KeyNotFoundException)
                {
                    resultsToInsert.Add((Maps.Classes)query.FirstOrDefault().Drivers[0].DriverCategory, new List<DriverInChampionshipStandings> { new DriverInChampionshipStandings { PlayerId = driver.CurrentDriver.PlayerId } });
                }
            }


            //Console.WriteLine(query.FirstOrDefault().RaceNumber);
        }
        //foreach (var entry in resultsToInsert[(Maps.Classes)1])
        //{
        //    Console.WriteLine(entry.PlayerId);
        //}
        foreach (KeyValuePair<Maps.Classes, List<DriverInChampionshipStandings>> e in resultsToInsert)
        {
            Console.WriteLine(e.Key);
        }
    }

    /// Makes a call to the database finding the first occurence. Not really suitable for high frequency of calls as it makes them one by one.
    private static bool DocumentExists(IMongoCollection<BsonDocument> collection, BsonDocument searchDoc)
    {
        BsonDocument? existing = collection.Find(searchDoc).FirstOrDefault();
        return existing != null;
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

    private class Entrylist
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

        //[BsonElement("points")]
        //public int Points { get; set; }

        //[BsonElement("pointsWDrop")]
        //public int PointsWDrop { get; set; }

        //[BsonElement("finishes")]
        //public Dictionary<string, object[]> Finishes { get; set; }

        //[BsonElement("roundDropped")]
        //public int RoundDropped { get; set; }
    }
}