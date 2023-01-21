class Server
{
  public static void Main()
  {
    FileWatcher watcher = new FileWatcher();
    watcher.Watch(".", results =>
    {
      if ((results?.trackName) == null)
      {
        return;
      }
      ResultsHandler.Handle(results);
    });
  }
}