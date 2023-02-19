using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

internal static class JsonDeser
{
    public static async Task<T> DeserAsync<T>(MemoryStream json)
    {
        try
        {
            var deserialized = await JsonSerializer.DeserializeAsync<T>(json);
            if (deserialized is not null) return deserialized;
        }
        catch (JsonException e)
        {
            await Console.Error.WriteLineAsync($"Error reading JSON {e.Message}");
        }

        throw new JsonException("Cannot deserialize JSON");
    }
}