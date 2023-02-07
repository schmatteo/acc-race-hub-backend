using System.Text;

internal class FileWatcher
{
    public static void Watch(string path, Action<Results> callback)
    {
        using FileSystemWatcher watcher = new(@path);

        watcher.NotifyFilter = NotifyFilters.FileName;

        watcher.Created += (sender, e) => OnCreated(sender, e, callback);

        watcher.Filter = "*.json";
        watcher.EnableRaisingEvents = true;
        Console.WriteLine($"Listening for new files in {watcher.Path}");
        _ = Console.ReadLine();
    }


    private static void OnCreated(object sender, FileSystemEventArgs e, Action<Results> callback)
    {
        string text = System.IO.File.ReadAllText(e.FullPath, Encoding.Unicode);
        callback(JsonDeser.Deser(text));
    }

    private static void PrintException(Exception? ex)
    {
        if (ex != null)
        {
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine("Stacktrace:");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
            PrintException(ex.InnerException);
        }
    }
}
