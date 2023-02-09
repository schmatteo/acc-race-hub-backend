using MongoDB.Driver;
using System;
using System.Threading.Tasks;

internal class Program
{
    private static MongoUrl? mongoUrl;
    public static async Task Main(string[] args)
    {
        // Load config. If program is ran with --mongourl flag, given url overwrites the one that's in the config
        var configDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/acc-race-hub-config.json";
        await CommandLine.HandleArgsAsync(args, async (argsUrl) =>
        {
            var cfg = await Config.ReadConfig(configDir);
            if (cfg.MongoUrl is not null) mongoUrl = cfg.MongoUrl;
            if (argsUrl is not null)
            {
                MongoUrl formattedUrl = new(argsUrl);
                mongoUrl = formattedUrl;
                cfg.SetMongoUrl(formattedUrl);
                await Config.WriteToConfig(configDir, cfg);
            }
            else
            {
                if (cfg.MongoUrl is null)
                {
                    throw new Exception("No MongoDB URL. Try running the app with --mongourl <url> flag");
                }
            }
        });

        Parallel.Invoke(
            HttpServer.Run,
            () => FileWatcher.Watch("../../..", results =>
            {
                if (results?.TrackName is null)
                {
                    return;
                }

                if (mongoUrl is not null)
                {
                    ResultsHandler.Handle(results, mongoUrl);
                }
                else
                {
                    Console.Error.WriteLine("Cannot process results file. MongoDB URL is null. Try closing the application and reopening it with --mongourl flag");
                }
            })
        );
    }
}