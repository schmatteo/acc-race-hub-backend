using System;
using System.Threading.Tasks;
using MongoDB.Driver;

internal class Program
{
    private static MongoUrl? _mongoUrl;

    public static async Task Main(string[] args)
    {
        // Load config. If program is ran with --mongourl flag, given url overwrites the one that's in the config
        var configDir =
            $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/acc-race-hub-config.json";
        _ = await CommandLine.HandleArgsAsync(args, async argsUrl =>
        {
            var cfg = await Config.ReadConfig(configDir);
            if (cfg.MongoUrl is not null) _mongoUrl = cfg.MongoUrl;

            if (argsUrl is not null)
            {
                MongoUrl formattedUrl = new(argsUrl);
                _mongoUrl = formattedUrl;
                cfg.SetMongoUrl(formattedUrl);
                await Config.WriteToConfig(configDir, cfg);
            }
            else
            {
                if (cfg.MongoUrl is null)
                    throw new Exception("No MongoDB URL. Try running the app with --mongourl <url> flag");
            }
        });

        Parallel.Invoke(
            HttpServer.Run,
            () => FileWatcher.Watch("../../..", results =>
            {
                if (results?.TrackName is null) return;

                if (_mongoUrl is not null)
                    ResultsHandler.Handle(results, _mongoUrl);
                else
                    Console.Error.WriteLine(
                        "Cannot process results file. MongoDB URL is null. Try closing the application and reopening it with --mongourl flag");
            })
        );
    }
}