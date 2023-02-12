using System;
using System.CommandLine;
using System.Threading.Tasks;

internal class CommandLine
{
    public static async Task<int> HandleArgsAsync(string[] args, Action<string?> callback)
    {
        Option<string> mongoUrlOption = new(
            "--mongourl",
            "Connection string of your MongoDB database. If one is already present in the config file, this will overwrite it."
        );
        RootCommand rootCommand = new("Backend for github.com/schmatteo/acc-race-hub");

        rootCommand.AddOption(mongoUrlOption);
        rootCommand.SetHandler(str => { Config.TryParseMongoUrl(str, callback); }, mongoUrlOption);
        return await rootCommand.InvokeAsync(args);
    }
}