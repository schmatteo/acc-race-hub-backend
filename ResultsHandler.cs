using System.Configuration;
using MongoDB.Driver;
using MongoDB.Bson;

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
      default:
        System.Console.Error.WriteLine("Unknown session type");
        break;
    }
  }
  private static void HandleRaceResults(Results results)
  {
    // var client = new MongoClient(MongoURI);
    // var database = client.GetDatabase("acc_race_hub");
    // var collection = database.GetCollection<BsonDocument>("race_results");
    // var lol = collection.Find(new BsonDocument()).ToList();
    // foreach (var l in lol)
    // {
    //   System.Console.WriteLine(l);
    // }
    var res = results.sessionResult!.leaderBoardLines!;
    foreach (var l in res)
    {
      System.Console.WriteLine(l.car!.raceNumber);
    }
  }

  private static void HandleQualifyingResults(Results results)
  {
    System.Console.WriteLine("q");
    System.Console.WriteLine(results?.serverName);
  }
}
