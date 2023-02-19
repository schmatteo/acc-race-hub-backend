using System;
using System.IO;
using System.Text;

internal static class FileWatcher
{
    public static void Watch(string path, Action<Results> callback)
    {
        using FileSystemWatcher watcher = new(path);

        watcher.NotifyFilter = NotifyFilters.FileName;

        watcher.Created += (_, e) => OnCreated(e, callback);

        watcher.Filter = "*.json";
        watcher.EnableRaisingEvents = true;
        Console.WriteLine($"Listening for new files in {watcher.Path}");
        _ = Console.ReadLine();
    }


    private static async void OnCreated(FileSystemEventArgs e, Action<Results> callback)
    {
        var text = await File.ReadAllTextAsync(e.FullPath, Encoding.Unicode);
        var byteArray = Encoding.UTF8.GetBytes(text);
        MemoryStream stream = new(byteArray);
        callback(await JsonDeser.DeserAsync<Results>(stream));
    }
}