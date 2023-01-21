using System.Text.Json;
class JsonDeser
{
  public static Results Deser(string json)
  {
    try
    {
      var deserialised = JsonSerializer.Deserialize<Results>(json);
      if (deserialised != null)
      {
        return deserialised;
      }
    }
    catch (JsonException e)
    {
      Console.Error.WriteLine(String.Format("Error reading JSON {}", e));
    }
    return new Results();
  }
}