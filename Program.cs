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
            var cfg = await Config.ReadConfig(configDir);
            // need to handle a situation where both command line and file urls are null
            if (cfg.MongoUrl is not null) mongoUrl = cfg.MongoUrl;
            Console.WriteLine($"URL from the config: {cfg.MongoUrl}");
            if (url is null)
            {
                Console.WriteLine("No URL in args");
            }
            else
            {
                MongoUrl formattedUrl = new(@url);
                cfg.SetMongoUrl(formattedUrl);
                await Config.WriteToConfig(configDir, cfg);
                Console.WriteLine($"URL from command line: {cfg.MongoUrl}");
            }
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