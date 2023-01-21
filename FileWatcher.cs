using System.Text;

class FileWatcher
{
  public void Watch(string path, Action<Results> callback)
  {
    using var watcher = new FileSystemWatcher(@path);

    watcher.NotifyFilter = NotifyFilters.FileName;

    watcher.Created += (sender, e) => OnCreated(sender, e, callback);

    watcher.Filter = "*.json";
    watcher.EnableRaisingEvents = true;

    Console.WriteLine("Press enter to exit.");
    Console.ReadLine();
  }


  private void OnCreated(object sender, FileSystemEventArgs e, Action<Results> callback)
  {
    var text = System.IO.File.ReadAllText(e.FullPath, Encoding.Unicode);
    callback(JsonDeser.Deser(text));
  }

  private void PrintException(Exception? ex)
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
