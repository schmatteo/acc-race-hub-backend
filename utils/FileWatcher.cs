using System;
using System.IO;
using System.Text;
using System.Threading;

internal static class FileWatcher
{
    private static FileSystemWatcher watcher;
    public static void Watch(string path, Action<Results> callback)
    {

        watcher = new(path);

        watcher.NotifyFilter = NotifyFilters.FileName;

        watcher.Created += (_, e) => OnCreated(e, callback);

        watcher.Filter = "*.json";
        watcher.EnableRaisingEvents = true;
        Console.WriteLine($"Listening for new files in {watcher.Path}");
        Thread.Sleep(Timeout.Infinite);
    }


    private static async void OnCreated(FileSystemEventArgs e, Action<Results> callback)
    {
        var text = await File.ReadAllTextAsync(e.FullPath, Encoding.Unicode);
        var byteArray = Encoding.UTF8.GetBytes(text);
        MemoryStream stream = new(byteArray);
        callback(await JsonDeser.DeserAsync<Results>(stream));
    }
}