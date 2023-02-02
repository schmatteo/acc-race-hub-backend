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
        var collection = database.GetCollection<BsonDocument>("race_results");
        await InsertRaceIntoDatabase(collection, results);
    }

    private static void HandleQualifyingResults(Results results)
    {
        // TODO: handle quali results
        System.Console.WriteLine("q");
        System.Console.WriteLine(results?.serverName);
    }

    private static async Task InsertRaceIntoDatabase(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonDocument searchQuery = new BsonDocument { { "race", results.serverName } };
        bool docExists = DocumentExists(collection, searchQuery);

        BsonArray resultsToPush = new BsonArray();
        foreach (var driver in results.sessionResult.leaderBoardLines)
        {
            DriverInResults d = new DriverInResults();
            d.playerId = driver.currentDriver.playerId;
            d.bestLap = driver.timing.bestLap;
            d.lapCount = driver.timing.lapCount;
            d.totalTime = driver.timing.totalTime;
            resultsToPush.Add(d.ToBsonDocument());
        }

        if (!docExists)
        {
            await collection.InsertOneAsync(new BsonDocument
            {
                { "race", results.serverName },
                { "track", results.trackName},
                { "results", resultsToPush },
                { "qualifyingResults", new BsonArray() }
            });
        } 
        else
        {
            var update = Builders<BsonDocument>.Update.Set("results", resultsToPush);
            await collection.UpdateOneAsync(searchQuery, update);
        }

        Console.WriteLine("Inserted race results into database");
    }

    private static bool DocumentExists(IMongoCollection<BsonDocument> collection, BsonDocument searchDoc)
    {
        var existing = collection.Find(searchDoc).FirstOrDefault();
        if (existing != null)
        {
            return true;
        }
        return false;
    }

    private class DriverInResults
    {
        public string? playerId { get; set; }
        public int bestLap { get; set; }
        public int lapCount { get; set; }
        public int totalTime { get; set; }
    }
}
