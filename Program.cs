using MongoDB.Driver;
using System;
using System.Threading.Tasks;

internal class Program
{
    private static MongoUrl? mongoUrl;
    public static async Task Main(string[] args)
    {
        var configDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/acc-race-hub-config.json";
        await CommandLine.HandleArgsAsync(args, async (url) =>
        {
            if (url is null)
            {
                Console.WriteLine("No URL in args");
            }
            // save the string that returned into the config and load it 
            var cfg = await Config.ReadConfig(configDir);
            if (cfg.MongoUrl is not null) mongoUrl = cfg.MongoUrl;
            Console.WriteLine($"real: {cfg.MongoUrl}");
        });

        Parallel.Invoke(
            HttpServer.Run,
            () => FileWatcher.Watch("../../..", results =>
            {
                if ((results?.TrackName) == null)
                {
                    return;
                }

                ResultsHandler.Handle(results, mongoUrl);
            })
        );
    }
}