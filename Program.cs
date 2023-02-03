internal class Program
{
    public static void Main()
    {
        Parallel.Invoke(
            HttpServer.Run,
            () => FileWatcher.Watch("../../.", results =>
            {
                if ((results?.TrackName) == null)
                {
                    return;
                }

                ResultsHandler.Handle(results);
            })
        );
    }
}