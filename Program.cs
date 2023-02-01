class Program
{
    public static void Main()
    {
        Parallel.Invoke(
            () => HttpServer.Run(),
            () => FileWatcher.Watch("../../..", results =>
            {
                if ((results?.trackName) == null) return;
                ResultsHandler.Handle(results);
            })
        );
    }
}