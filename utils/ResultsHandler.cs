using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using static DatabaseTypes;

internal class ResultsHandler
{
    public static void Handle(Results results, MongoUrl mongoUrl)
    {
        MongoClient client = new(mongoUrl);
        var database = client.GetDatabase("acc_race_hub");
        switch (results.SessionType)
        {
            case "Q":
                HandleQualifyingResults(results, database);
                break;
            case "R":
                HandleRaceResults(results, database);
                break;
            case "FP":
                Console.Error.WriteLine("Practice session results handling is not currently supported");
                break;
            default:
                Console.Error.WriteLine("Unknown session type");
                break;
        }
    }

    private static async void HandleRaceResults(Results results, IMongoDatabase database)
    {
        var lapCount = results.SessionResult.LeaderBoardLines[0].Timing.LapCount;
        var dnfLapCount = (int)(lapCount * 0.9);

        var raceCollection = database.GetCollection<BsonDocument>("race_results");
        var manufacturersCollection = database.GetCollection<BsonDocument>("manufacturers_standings");
        var driversCollection = database.GetCollection<DriversCollection>("drivers_standings");
        var entrylistCollection = database.GetCollection<EntrylistCollection>("entrylist");
        var teamsCollection = database.GetCollection<TeamsCollection>("teams");

        var insertRaceTask = InsertRaceIntoDatabaseAsync(raceCollection, results);
        var updateManufacturersTask = UpdateManufacturersStandingsAsync(manufacturersCollection, results, dnfLapCount);
        var updateIndividualResultsTask =
            UpdateIndividualResultsAsync(driversCollection, entrylistCollection, results, dnfLapCount);
        List<Task> tasks = new() { insertRaceTask, updateIndividualResultsTask, updateManufacturersTask };

        while (tasks.Count > 0)
        {
            var finishedTask = await Task.WhenAny(tasks);
            if (finishedTask == insertRaceTask)
                Console.WriteLine("Inserted race into the database");
            else if (finishedTask == updateIndividualResultsTask)
                Console.WriteLine("Updated individual results");
            else if (finishedTask == updateManufacturersTask) Console.WriteLine("Updated manufacturers standings");
            await finishedTask;
            _ = tasks.Remove(finishedTask);
        }

        UpdateDropRound(driversCollection);
        Console.WriteLine("Updated drop rounds");
        UpdateTeamsChampionship(teamsCollection, driversCollection);
    }

    private static async void HandleQualifyingResults(Results results, IMongoDatabase database)
    {
        var collection = database.GetCollection<BsonDocument>("race_results");

        await InsertQualifyingIntoDatabaseAsync(collection, results);
        Console.WriteLine("Inserted qualifying results into the database");
    }


    // Race results related tasks

    private static async Task InsertRaceIntoDatabaseAsync(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonArray resultsToInsert = new();
        foreach (var driver in results.SessionResult.LeaderBoardLines)
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
        var update = Builders<BsonDocument>.Update.Set("results", resultsToInsert);
        UpdateOptions options = new() { IsUpsert = true };

        try
        {
            _ = await collection.UpdateOneAsync(searchString, update, options);
            Console.WriteLine("Inserted race results into database");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error inserting race results into database {ex.Message}");
        }
    }

    private static async Task UpdateManufacturersStandingsAsync(IMongoCollection<BsonDocument> collection,
        Results results, int dnfLapCount)
    {
        Dictionary<int, int> carPoints = new();
        foreach (var (driver, index) in results.SessionResult.LeaderBoardLines.Select(
                     (item, index) => (item, index)))
            if (index < 15)
            {
                if (driver.Timing.LapCount < dnfLapCount) continue;
                var car = driver.Car.CarModel;
                var points = Maps.Points[index];
                try
                {
                    carPoints[car] += points;
                }
                catch (KeyNotFoundException)
                {
                    carPoints.Add(car, points);
                }
            }
            else
            {
                break;
            }

        UpdateOptions options = new() { IsUpsert = true };
        var documentsToInsert = (from item in carPoints 
            let pointsToAdd = Builders<BsonDocument>.Update.Inc("points", item.Value) 
            select collection.UpdateOneAsync(new BsonDocument { { "car", Maps.Cars[item.Key] } }, pointsToAdd, options))
            .Cast<Task>()
            .ToList();

        await Task.WhenAll(documentsToInsert);
    }

    private static async Task UpdateIndividualResultsAsync(IMongoCollection<DriversCollection> collection,
        IMongoCollection<EntrylistCollection> entrylistCollection, Results results, int dnfLapCount)
    {
        try
        {
            var entrylist = await entrylistCollection.Find(_ => true).ToListAsync();

            var sortedRaceResults = results.SessionResult.LeaderBoardLines
                .Join(entrylist,
                    result => result.CurrentDriver.PlayerId,
                    entry => (entry.Drivers 
                              ?? throw new InvalidOperationException("Cannot match a driver from entrylist with a driver in race results"))
                        .Select(d => d.PlayerID).FirstOrDefault(),
                    (result, entry) => new
                    {
                        Result = result,
                        Class = (Maps.Classes)((entry.Drivers ?? throw new InvalidOperationException("Cannot match a driver from entrylist with a driver in race results"))
                            .FirstOrDefault(d => d.PlayerID == result.CurrentDriver.PlayerId)?.DriverCategory ?? 0)
                    })
                .GroupBy(x => x.Class)
                .ToDictionary(x => x.Key, x => x.Select(r => r.Result).ToArray());

            var purples = GetFastestLap(sortedRaceResults);

            List<Task> documentsToInsert = new();

            foreach (var entry in entrylist)
            {
                IQueryable<DriverResult> driverInResults =
                    from doc in results.SessionResult.LeaderBoardLines.AsQueryable()
                    where doc.CurrentDriver.PlayerId == entry.Drivers![0].PlayerID
                    select doc;

                DriverInChampionshipDefinitions updates;

                if (driverInResults.Any())
                {
                    var dnf = driverInResults.First().Timing.LapCount < dnfLapCount;
                    if (dnf)
                    {
                        updates = new DriverInChampionshipDefinitions(0, -2, false, results.TrackName);
                    }
                    else
                    {
                        var place = Array.FindIndex(
                            sortedRaceResults[(Maps.Classes)(entry.Drivers?[0].DriverCategory ?? 0)],
                            e => e == driverInResults.First());
                        var points = Maps.Points[place];


                        var purple =
                            purples[(Maps.Classes)(entry.Drivers?[0].DriverCategory ?? 0)].CurrentDriver.PlayerId ==
                            entry.Drivers?[0].PlayerID;
                        if (purple) points += 3;

                        updates = new DriverInChampionshipDefinitions(points, place + 1, purple, results.TrackName);
                    }
                }
                else
                {
                    updates = new DriverInChampionshipDefinitions(0, -1, false, results.TrackName);
                }

                var update = Builders<DriversCollection>.Update.Combine(
                    updates.PointsDefinition, updates.FinishesDefinition
                );

                UpdateOptions options = new() { IsUpsert = true };
                documentsToInsert.Add(
                    collection.UpdateOneAsync(new BsonDocument { { "playerId", entry.Drivers?[0].PlayerID } }, update,
                        options));
            }

            await Task.WhenAll(documentsToInsert);
        }
        catch (NullReferenceException)
        {
            await Console.Error.WriteLineAsync("Entrylist possibly not up to date");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error fetching entrylist {ex.Message}");
        }
    }

    private static async void UpdateDropRound(IMongoCollection<DriversCollection> collection)
    {
        var cursor = await collection.Find(_ => true).ToCursorAsync();

        try
        {
            while (await cursor.MoveNextAsync())
            {
                var documentsToInsert = (from driver in cursor.Current 
                    where driver.Finishes?.Length > 1 
                    let finishesSorted = driver.Finishes.OrderBy(x => x.Points) 
                    let worstFinish = finishesSorted.FirstOrDefault() 
                    let droppedRound = Array.FindIndex(driver.Finishes, x => x == worstFinish) 
                    let pointsWithDrop = finishesSorted.Skip(1).Sum(x => x.Points) 
                    let updates = new DropRoundDefinitions(droppedRound, pointsWithDrop) 
                    let query = Builders<DriversCollection>.Update.Combine(updates.DropRoundIndex, updates.PointsWithDrop) 
                    select collection.UpdateOneAsync(new BsonDocument { { "playerId", driver.PlayerId } }, query))
                    .Cast<Task>()
                    .ToList();

                await Task.WhenAll(documentsToInsert);
            }
        }
        finally
        {
            cursor.Dispose();
        }
    }

    private static async void UpdateTeamsChampionship(IMongoCollection<TeamsCollection> teamsCollection,
        IMongoCollection<DriversCollection> driversCollection)
    {
        var cursor = await teamsCollection.Find(_ => true).ToCursorAsync();
        try
        {
            while (await cursor.MoveNextAsync())
            {
                // create a query which will find points of each of the drivers in the team (Drivers array in TeamsCollection) and sum them up
                var documentsToInsert = (from team in cursor.Current
                    let projection = Builders<DriversCollection>.Projection.Combine(
                        Builders<DriversCollection>.Projection.Include("points")
                        , Builders<DriversCollection>.Projection.Include("pointsWDrop"))
                    let drivers = team.Drivers
                    let query = Builders<DriversCollection>.Filter.In("playerId", drivers)
                    let driver = driversCollection.Find(query)
                    let driverPoints = driver.Project(projection).ToList()
                    let pointsSum = driverPoints.Sum(x => x["points"].AsInt32)
                    // throws an exception if there is no pointsWDrop field in the document
                    let pointsWDropSum = driverPoints.Sum(x => x["pointsWDrop"].AsInt32)
                    let updatePoints = Builders<TeamsCollection>.Update.Set("points", pointsSum)
                    let updateDropPoints = Builders<TeamsCollection>.Update.Set("pointsWDrop", pointsWDropSum)
                    let query2 = Builders<TeamsCollection>.Update.Combine(updatePoints, updateDropPoints)
                    select teamsCollection.UpdateOneAsync(new BsonDocument { { "team", team.TeamName } }, query2))
                    .Cast<Task>()
                    .ToList();
                
                await Task.WhenAll(documentsToInsert);
            }
        }
        finally
        {
            cursor.Dispose();
        }
        
    }
    
    private static Dictionary<Maps.Classes, DriverResult> GetFastestLap(
        Dictionary<Maps.Classes, DriverResult[]> results)
    {
        Dictionary<Maps.Classes, DriverResult> fastest = new();
        foreach (var entry in results)
        {
            var purple = entry.Value.Min(x => x.Timing.BestLap);
            IEnumerable<DriverResult> holder = from doc in entry.Value
                where doc.Timing.BestLap == purple
                select doc;
            fastest.Add(entry.Key, holder.First());
        }

        return fastest;
    }


    // Qualifying results related tasks

    private static async Task InsertQualifyingIntoDatabaseAsync(IMongoCollection<BsonDocument> collection,
        Results results)
    {
        BsonArray resultsToInsert = new();
        foreach (var driver in results.SessionResult.LeaderBoardLines)
        {
            var query = from doc in results.Laps
                where doc.CarId == driver.Car.CarId
                where doc.IsValidForBest
                select doc.Laptime;

            var laps = query.ToList();
            QualifyingResult result = new(driver.CurrentDriver.PlayerId, driver.Timing.BestLap, driver.Timing.LapCount,
                laps);
            _ = resultsToInsert.Add(result.ToBsonDocument());
        }

        UpdateOptions options = new() { IsUpsert = true };
        var update = Builders<BsonDocument>.Update.Set("qualifyingResults", resultsToInsert);

        try
        {
            _ = await collection.UpdateOneAsync(
                new BsonDocument { { "race", results.ServerName }, { "track", results.TrackName } }, update, options);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error inserting qualifying results into database {ex.Message}");
        }
    }


    // Types

    private class DriverInRaceResults
    {
        [BsonElement("playerId")] public string? PlayerId { get; set; }

        [BsonElement("bestLap")] public int BestLap { get; set; }

        [BsonElement("lapCount")] public int LapCount { get; set; }

        [BsonElement("totalTime")] public int TotalTime { get; set; }
    }

    private class DriverInChampionshipDefinitions
    {
        public DriverInChampionshipDefinitions(int points, int finishingPosition, bool fastestLap, string trackName)
        {
            PointsDefinition = Builders<DriversCollection>.Update.Inc("points", points);
            DriversCollection.Finish finishToPush = new()
            {
                TrackName = trackName, FinishingPosition = finishingPosition, FastestLap = fastestLap, Points = points
            };
            FinishesDefinition = Builders<DriversCollection>.Update.Push("finishes", finishToPush.ToBsonDocument());
        }

        public UpdateDefinition<DriversCollection> PointsDefinition { get; }
        public UpdateDefinition<DriversCollection> FinishesDefinition { get; }
    }

    private class DropRoundDefinitions
    {
        public DropRoundDefinitions(int dropRoundIndex, int pointsWithDrop)
        {
            DropRoundIndex = Builders<DriversCollection>.Update.Set("roundDropped", dropRoundIndex);
            PointsWithDrop = Builders<DriversCollection>.Update.Set("pointsWDrop", pointsWithDrop);
        }

        public UpdateDefinition<DriversCollection> DropRoundIndex { get; }
        public UpdateDefinition<DriversCollection> PointsWithDrop { get; }
    }

    private class QualifyingResult
    {
        public QualifyingResult(string playerId, int bestLap, int lapCount, List<int> laps)
        {
            PlayerId = playerId;
            BestLap = bestLap;
            LapCount = lapCount;
            Laps = laps;
        }

        private string PlayerId { get; }
        private int BestLap { get; }
        private int LapCount { get; }
        private List<int> Laps { get; }
    }
}