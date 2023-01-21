class ResultsHandler
{
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
    System.Console.WriteLine("race");
    System.Console.WriteLine(results?.serverName);
  }

  private static void HandleQualifyingResults(Results results)
  {
    System.Console.WriteLine("q");
    System.Console.WriteLine(results?.serverName);
  }
}
