using System.Configuration;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

class ResultsHandler
{
    private static string MongoURI = ConfigurationManager.AppSettings.Get("MongoURI") ?? "";
    private static MongoClient client = new MongoClient(MongoURI);
    private static IMongoDatabase database = client.GetDatabase("acc_race_hub");

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
                System.Console.Error.WriteLine("Practice session result handling is not currently supported");
                break;
            default:
                System.Console.Error.WriteLine("Unknown session type");
                break;
        }
    }

    private static async void HandleRaceResults(Results results)
    {
        var raceCollection = database.GetCollection<BsonDocument>("race_results");
        var manufacturersCollection = database.GetCollection<BsonDocument>("manufacturers_standings");

        await InsertRaceIntoDatabase(raceCollection, results);
        await HandleManufacturersStandings(manufacturersCollection, results);
    }

    private static void HandleQualifyingResults(Results results)
    {
        // TODO: handle quali results
        System.Console.WriteLine("q");
        System.Console.WriteLine(results?.serverName);
    }

    private static async Task InsertRaceIntoDatabase(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonArray resultsToPush = new();
        foreach (var driver in results.sessionResult.leaderBoardLines)
        {
            DriverInRaceResults d = new();
            d.playerId = driver.currentDriver.playerId;
            d.bestLap = driver.timing.bestLap;
            d.lapCount = driver.timing.lapCount;
            d.totalTime = driver.timing.totalTime;
            resultsToPush.Add(d.ToBsonDocument());
        }

        var searchString = new BsonDocument
        {
            { "race", results.serverName },
            { "track", results.trackName }
        };
        var update = Builders<BsonDocument>.Update.Set("results", resultsToPush);
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
                    var points = Maps.points[index];
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
            await collection.UpdateOneAsync(new BsonDocument { { "car", Maps.cars[item.Key] } }, pointsToAdd, options);
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
}
