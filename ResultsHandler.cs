using System.Configuration;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization.Attributes;

class ResultsHandler
{
    private static string MongoURI = ConfigurationManager.AppSettings.Get("MongoURI") ?? "";

    public static void Handle(Results results)
    {
        switch (results?.sessionType)
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
        MongoClient client = new MongoClient(settings);
        IMongoDatabase database = client.GetDatabase("acc_race_hub");

        var raceCollection = database.GetCollection<BsonDocument>("race_results");
        var manufacturersCollection = database.GetCollection<BsonDocument>("manufacturers_standings");
        var driversCollection = database.GetCollection<BsonDocument>("drivers_standings");
        var entrylistCollection = database.GetCollection<Entrylist>("entrylist");

        await InsertRaceIntoDatabase(raceCollection, results);
        await HandleManufacturersStandings(manufacturersCollection, results);
        await HandleIndividualResults(driversCollection, entrylistCollection, results);
    }

    private static void HandleQualifyingResults(Results results)
    {
        // TODO: handle quali results
        System.Console.WriteLine("q");
        System.Console.WriteLine(results?.serverName);
    }

    private static async Task InsertRaceIntoDatabase(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonArray resultsToInsert = new();
        foreach (var driver in results.sessionResult.leaderBoardLines)
        {
            DriverInRaceResults d = new();
            d.playerId = driver.currentDriver.playerId;
            d.bestLap = driver.timing.bestLap;
            d.lapCount = driver.timing.lapCount;
            d.totalTime = driver.timing.totalTime;
            resultsToInsert.Add(d.ToBsonDocument());
        }

        var searchString = new BsonDocument
        {
            { "race", results.serverName },
            { "track", results.trackName }
        };
        var update = Builders<BsonDocument>.Update.Set("results", resultsToInsert);
        var options = new UpdateOptions { IsUpsert = true };

        await collection.UpdateOneAsync(searchString, update, options);
        Console.WriteLine("Inserted race results into database");
    }

    private static async Task HandleManufacturersStandings(IMongoCollection<BsonDocument> collection, Results results)
    {
        int totalLaps = results.sessionResult.leaderBoardLines[0].timing.lapCount;
        Dictionary<int, int> carPoints = new();
        foreach (var (driver, index) in results.sessionResult.leaderBoardLines.Select((item, index) => (item, index)))
        {
            if (index < 15)
            {
                if (driver.timing.lapCount >= totalLaps - 5)
                {
                    var car = driver.car.carModel;
                    var points = Maps.Points[index + 1];
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

        var options = new UpdateOptions { IsUpsert = true };
        foreach (var item in carPoints)
        {
            var pointsToAdd = Builders<BsonDocument>.Update.Inc("points", item.Value);
            await collection.UpdateOneAsync(new BsonDocument { { "car", Maps.Cars[item.Key] } }, pointsToAdd, options);
            await collection.UpdateOneAsync(new BsonDocument { { "car", Maps.Cars[item.Key] } }, pointsToAdd, options);
        }
    }

    private static async Task HandleIndividualResults(IMongoCollection<BsonDocument> collection, IMongoCollection<Entrylist> entrylistCollection, Results results)
    {
        Dictionary<Maps.Classes, List<DriverInChampionshipStandings>> resultsToInsert = new();
        foreach (var driver in results.sessionResult.leaderBoardLines)
        {
            IMongoQueryable<Entrylist> query = (from doc in entrylistCollection.AsQueryable()
                                                where doc.Drivers[0].PlayerID == driver.currentDriver.playerId
                                                select doc);
            if (query.Any())
            {
                try
                {
                    resultsToInsert[(Maps.Classes)query.FirstOrDefault().Drivers[0].DriverCategory].Add(new DriverInChampionshipStandings { PlayerId = driver.currentDriver.playerId });
                }
                catch (KeyNotFoundException)
                {
                    resultsToInsert.Add((Maps.Classes)query.FirstOrDefault().Drivers[0].DriverCategory, new List<DriverInChampionshipStandings> { new DriverInChampionshipStandings { PlayerId = driver.currentDriver.playerId } });
                }
            }


            //Console.WriteLine(query.FirstOrDefault().RaceNumber);
        }
        foreach (var entry in resultsToInsert[(Maps.Classes)1])
        {
            Console.WriteLine(entry.PlayerId);
        }
    }

    /// Makes a call to the database finding the first occurence. Not really suitable for high frequency of calls as it makes them one by one.
    private static bool DocumentExists(IMongoCollection<BsonDocument> collection, BsonDocument searchDoc)
    {
        BsonDocument? existing = collection.Find(searchDoc).FirstOrDefault();
        if (existing != null)
        {
            return true;
        }
        return false;
    }

    private class DriverInRaceResults
    {
        public string? playerId { get; set; }
        public int bestLap { get; set; }
        public int lapCount { get; set; }
        public int totalTime { get; set; }
    }

#pragma warning disable 8618

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
        public string? Id { get; set; }

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

    //private enum Classes
    //{
    //    pro = 3,
    //    silver = 1,
    //    am = 0
    //}
}

#pragma warning restore 8618