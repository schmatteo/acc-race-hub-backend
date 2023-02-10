using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

internal class JsonDeser
{
    public static async Task<T> DeserAsync<T>(MemoryStream json)
    {
        try
        {
            T? deserialised = await JsonSerializer.DeserializeAsync<T>(json);
            if (deserialised is not null)
            {
                return deserialised;
            }
        }
        catch (JsonException e)
        {
            Console.Error.WriteLine($"Error reading JSON {e.Message}");
        }
        throw new JsonException("Cannot deserialise JSON");
    }
}