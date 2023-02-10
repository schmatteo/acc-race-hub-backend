using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static DatabaseTypes;

internal class ResultsHandler
{
    public static void Handle(Results results, MongoUrl mongoUrl)
    {
        MongoClient client = new(mongoUrl);
        IMongoDatabase database = client.GetDatabase("acc_race_hub");
        switch (results?.SessionType)
        {
            case "Q":
                ResultsHandler.HandleQualifyingResults(results, database);
                break;
            case "R":
                ResultsHandler.HandleRaceResults(results, database);
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
        int lapCount = results.SessionResult.LeaderBoardLines[0].Timing.LapCount;
        int dnfLapCount = (int)(lapCount * 0.9);

        IMongoCollection<BsonDocument> raceCollection = database.GetCollection<BsonDocument>("race_results");
        IMongoCollection<BsonDocument> manufacturersCollection = database.GetCollection<BsonDocument>("manufacturers_standings");
        IMongoCollection<DatabaseTypes.DriversCollection> driversCollection = database.GetCollection<DatabaseTypes.DriversCollection>("drivers_standings");
        IMongoCollection<DatabaseTypes.EntrylistCollection> entrylistCollection = database.GetCollection<DatabaseTypes.EntrylistCollection>("entrylist");
        IMongoCollection<DatabaseTypes.TeamsCollection> teamsCollection = database.GetCollection<DatabaseTypes.TeamsCollection>("teams");

        Task insertRaceTask = InsertRaceIntoDatabaseAsync(raceCollection, results);
        Task updateManufacturersTask = UpdateManufacturersStandingsAsync(manufacturersCollection, results, dnfLapCount);
        Task updateIndividualResultsTask = UpdateIndividualResultsAsync(driversCollection, entrylistCollection, results, dnfLapCount);
        List<Task> tasks = new() { insertRaceTask, updateIndividualResultsTask, updateManufacturersTask };

        //while (tasks.Count > 0)
        //{
        //    Task finishedTask = await Task.WhenAny(tasks);
        //    if (finishedTask == insertRaceTask)
        //    {
        //        Console.WriteLine("Inserted race into the database");
        //    }
        //    else if (finishedTask == updateIndividualResultsTask)
        //    {
        //        Console.WriteLine("Updated individual results");
        //    }
        //    else if (finishedTask == updateManufacturersTask)
        //    {
        //        Console.WriteLine("Updated manufacturers standings");
        //    }
        //    await finishedTask;
        //    _ = tasks.Remove(finishedTask);
        //}

        Task updateDropRoundTask = UpdateDropRound(driversCollection);
        Task updateTeamsTask = UpdateTeamStandingsAsync(driversCollection, teamsCollection);
        List<Task> secondaryTasks = new() { updateDropRoundTask, updateTeamsTask };

        while (secondaryTasks.Count > 0)
        {
            Task finishedTask = await Task.WhenAny(secondaryTasks);
            if (finishedTask == updateDropRoundTask)
            {
                Console.WriteLine("Updated drop rounds");
            }
            else if (finishedTask == updateTeamsTask)
            {
                Console.WriteLine("Updated teams standings");
            }
            await finishedTask;
            _ = secondaryTasks.Remove(finishedTask);
        }
    }

    private static async void HandleQualifyingResults(Results results, IMongoDatabase database)
    {
        IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("race_results");

        await InsertQualifyingIntoDatabaseAsync(collection, results);
        Console.WriteLine("Inserted qualifying results into the database");
    }


    // Race results related tasks

    private static async Task InsertRaceIntoDatabaseAsync(IMongoCollection<BsonDocument> collection, Results results)
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

        try
        {
            _ = await collection.UpdateOneAsync(searchString, update, options);
            Console.WriteLine("Inserted race results into database");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error inserting race results into database {ex.Message}");
        }
    }

    private static async Task UpdateManufacturersStandingsAsync(IMongoCollection<BsonDocument> collection, Results results, int dnfLapCount)
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
        List<Task> documentsToInsert = new();
        foreach (KeyValuePair<int, int> item in carPoints)
        {
            UpdateDefinition<BsonDocument> pointsToAdd = Builders<BsonDocument>.Update.Inc("points", item.Value);
            documentsToInsert.Add(collection.UpdateOneAsync(new BsonDocument { { "car", Maps.Cars[item.Key] } }, pointsToAdd, options));
        }
        await Task.WhenAll(documentsToInsert);
    }

    private static async Task UpdateIndividualResultsAsync(IMongoCollection<DatabaseTypes.DriverInChampionshipStandings> collection, IMongoCollection<DatabaseTypes.EntrylistEntry> entrylistCollection, Results results, int dnfLapCount)
    {
        try
        {
            List<DatabaseTypes.EntrylistEntry> entrylist = await entrylistCollection.Find(_ => true).ToListAsync();

            Dictionary<Maps.Classes, DriverResult[]> sortedRaceResults = results.SessionResult.LeaderBoardLines
            .Join(entrylist,
                result => result.CurrentDriver.PlayerId,
                entry => entry.Drivers.Select(d => d.PlayerID).FirstOrDefault(),
                (result, entry) => new { Result = result, Class = (Maps.Classes)(entry.Drivers.FirstOrDefault(d => d.PlayerID == result.CurrentDriver.PlayerId)?.DriverCategory ?? 0) })
            .GroupBy(x => x.Class)
            .ToDictionary(x => x.Key, x => x.Select(x => x.Result).ToArray());

            Dictionary<Maps.Classes, DriverResult> purples = GetFastestLap(sortedRaceResults);

            List<Task> documentsToInsert = new();

            foreach (DatabaseTypes.EntrylistEntry entry in entrylist)
            {
                IQueryable<DriverResult> driverInResults = from doc in results.SessionResult.LeaderBoardLines.AsQueryable()
                                                           where doc.CurrentDriver.PlayerId == entry.Drivers![0].PlayerID
                                                           select doc;

                DatabaseTypes.DriverInChampionshipStandings driverToInsert = new() { PlayerId = entry.Drivers?[0].PlayerID };
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
                        int place = Array.FindIndex(sortedRaceResults[(Maps.Classes)(entry.Drivers?[0].DriverCategory ?? 0)], e => e == driverInResults.First());
                        int points = Maps.Points[place];


                        bool purple = purples[(Maps.Classes)(entry.Drivers?[0].DriverCategory ?? 0)].CurrentDriver.PlayerId == entry.Drivers?[0].PlayerID;
                        if (purple)
                        {
                            points += 3;
                        }

                        updates = new(points, place + 1, purple, results.TrackName);
                    }
                }
                else
                {
                    updates = new(0, -1, false, results.TrackName);
                }

                UpdateDefinition<DatabaseTypes.DriverInChampionshipStandings> update = Builders<DatabaseTypes.DriverInChampionshipStandings>.Update.Combine(
                    updates.PointsDefinition, updates.FinishesDefinition
                );

                UpdateOptions options = new() { IsUpsert = true };
                documentsToInsert.Add(collection.UpdateOneAsync(new BsonDocument { { "playerId", entry.Drivers?[0].PlayerID } }, update, options));
            }
            await Task.WhenAll(documentsToInsert);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching entrylist {ex.Message}");
        }


    }

    private static async void UpdateDropRound(IMongoCollection<DatabaseTypes.DriverInChampionshipStandings> collection)
    {
        IAsyncCursor<DatabaseTypes.DriverInChampionshipStandings> cursor = await collection.Find(_ => true).ToCursorAsync();

        try
        {
            while (await cursor.MoveNextAsync())
            {
                DropRoundDefinitions updates;
                List<Task> documentsToInsert = new();
                foreach (DatabaseTypes.DriverInChampionshipStandings driver in cursor.Current)
                {
                    if (driver.Finishes?.Length > 1)
                    {
                        IOrderedEnumerable<DatabaseTypes.DriverInChampionshipStandings.Finish> finishesSorted = driver.Finishes.OrderBy(x => x.Points);
                        DatabaseTypes.DriverInChampionshipStandings.Finish? worstFinish = finishesSorted.FirstOrDefault();
                        int droppedRound = Array.FindIndex(driver.Finishes, x => x == worstFinish);
                        int pointsWithDrop = finishesSorted.Skip(1).Sum(x => x.Points);

                        DropRoundDefinitions updates = new(droppedRound, pointsWithDrop);

                        UpdateDefinition<DatabaseTypes.DriverInChampionshipStandings> query = Builders<DatabaseTypes.DriverInChampionshipStandings>.Update.Combine(updates.DropRoundIndex, updates.PointsWithDrop);
                        documentsToInsert.Add(collection.UpdateOneAsync(new BsonDocument { { "playerId", driver.PlayerId } }, query));
                    }
                }
            }
        }
        finally
        {
            foreach (var team in teams)
            {
                foreach (var driver in team.Value)
                {
                    queriesTasks.Add(driver);
                }
            }
            while (queriesTasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(queriesTasks);

                var currentDriver = await finishedTask;

                if (currentDriver.Any())
                {
                    Console.WriteLine(currentDriver.First().PlayerId);
                }
                queriesTasks.Remove(finishedTask);
            }
            //await Task.WhenAll(updates);
            cursor.Dispose();
        }
    }

    private static Dictionary<Maps.Classes, DriverResult> GetFastestLap(Dictionary<Maps.Classes, DriverResult[]> results)
    {
        Dictionary<Maps.Classes, DriverResult> fastest = new();
        foreach (KeyValuePair<Maps.Classes, DriverResult[]> entry in results)
        {
            int purple = entry.Value.Min(x => x.Timing.BestLap);
            IEnumerable<DriverResult> holder = from doc in entry.Value
                                               where doc.Timing.BestLap == purple
                                               select doc;
            fastest.Add(entry.Key, holder.First());
        }
        return fastest;
    }


    // Qualifying results related tasks

    private static async Task InsertQualifyingIntoDatabaseAsync(IMongoCollection<BsonDocument> collection, Results results)
    {
        BsonArray resultsToInsert = new();
        foreach (DriverResult driver in results.SessionResult.LeaderBoardLines)
        {
            IEnumerable<int> query = from doc in results.Laps
                                     where doc.CarId == driver.Car.CarId
                                     where doc.IsValidForBest
                                     select doc.Laptime;

            List<int> laps = query.ToList();
            QualifyingResult result = new(driver.CurrentDriver.PlayerId, driver.Timing.BestLap, driver.Timing.LapCount, laps);
            _ = resultsToInsert.Add(result.ToBsonDocument());
        }

        UpdateOptions options = new() { IsUpsert = true };
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Set("qualifyingResults", resultsToInsert);

        try
        {
            _ = await collection.UpdateOneAsync(new BsonDocument { { "race", results.ServerName }, { "track", results.TrackName } }, update, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error inserting qualifying results into database {ex.Message}");
        }
    }


    // Types

    private class DriverInRaceResults
    {
        [BsonElement("playerId")]
        public string? PlayerId { get; set; }

        [BsonElement("bestLap")]
        public int BestLap { get; set; }

        [BsonElement("lapCount")]
        public int LapCount { get; set; }

        [BsonElement("totalTime")]
        public int TotalTime { get; set; }
    }

    private class DriverInChampionshipDefinitions
    {
        public UpdateDefinition<DatabaseTypes.DriverInChampionshipStandings> PointsDefinition { get; }
        public UpdateDefinition<DatabaseTypes.DriverInChampionshipStandings> FinishesDefinition { get; }

        public DriverInChampionshipDefinitions(int points, int finishingPosition, bool fastestLap, string trackName)
        {
            PointsDefinition = Builders<DatabaseTypes.DriverInChampionshipStandings>.Update.Inc("points", points);
            DatabaseTypes.DriverInChampionshipStandings.Finish finishToPush = new() { TrackName = trackName, FinishingPosition = finishingPosition, FastestLap = fastestLap, Points = points };
            FinishesDefinition = Builders<DatabaseTypes.DriverInChampionshipStandings>.Update.Push("finishes", finishToPush.ToBsonDocument());
        }
    }

    private class DropRoundDefinitions
    {
        public UpdateDefinition<DatabaseTypes.DriverInChampionshipStandings> DropRoundIndex { get; }
        public UpdateDefinition<DatabaseTypes.DriverInChampionshipStandings> PointsWithDrop { get; }
        public DropRoundDefinitions(int dropRoundIndex, int pointsWithDrop)
        {
            DropRoundIndex = Builders<DatabaseTypes.DriverInChampionshipStandings>.Update.Set("roundDropped", dropRoundIndex);
            PointsWithDrop = Builders<DatabaseTypes.DriverInChampionshipStandings>.Update.Set("pointsWDrop", pointsWithDrop);
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