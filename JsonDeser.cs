using System;
using System.Text.Json;

internal class JsonDeser
{
    public static Results Deser(string json)
    {
        try
        {
            Results deserialised = JsonSerializer.Deserialize<Results>(json);
            if (deserialised != null)
            {
                return deserialised;
            }
        }
        catch (JsonException e)
        {
            Console.Error.WriteLine($"Error reading JSON {e}");
        }
        return new Results();
    }
}