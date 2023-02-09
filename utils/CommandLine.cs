using MongoDB.Driver;
using System;
using System.CommandLine;
using System.Threading.Tasks;

class CommandLine
{
    public static async Task<int> HandleArgsAsync(string[] args, Action<string?> callback)
    {
        var mongoUrlOption = new Option<string>(
            name: "--mongourl",
            description: "Connection string of your MongoDB database. If one is already present in the config file, this will overwrite it."
        );
        var rootCommand = new RootCommand("Backend for github.com/schmatteo/acc-race-hub");

        rootCommand.AddOption(mongoUrlOption);
        rootCommand.SetHandler((str) =>
        {
            Config.TryParseMongoUrl(str, callback);
        }, mongoUrlOption);
        return await rootCommand.InvokeAsync(args);
    }
}
