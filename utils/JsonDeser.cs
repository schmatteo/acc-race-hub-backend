using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

internal class JsonDeser
{
    public static async Task<Results> DeserResultsAsync(MemoryStream json)
    {
        try
        {
            Results? deserialised = await JsonSerializer.DeserializeAsync<Results>(json);
            if (deserialised != null)
            {
                return deserialised;
            }
        }
        catch (JsonException e)
        {
            Console.Error.WriteLine($"Error reading JSON {e}");
        }
        throw new JsonException("Invalid results JSON");
    }

    public static async Task<Config> DeserConfigAsync(MemoryStream json)
    {
        try
        {
            Config? deserialised = await JsonSerializer.DeserializeAsync<Config>(json);
            if (deserialised != null)
            {
                return deserialised;
            }
        }
        catch (JsonException e)
        {
            Console.Error.WriteLine($"Error reading JSON {e}");
        }
        throw new JsonException("Error reading config");
    }
}