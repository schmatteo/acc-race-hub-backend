using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

internal class JsonDeser
{
    public static async Task<Results> DeserAsync(MemoryStream json)
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
        return new Results();
    }
}