using MongoDB.Driver;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

internal class Config
{
    [JsonRequired]
    [JsonPropertyName("mongoUrl")]
    public string MongoDeserialisedUrl { get; set; } = "";

    [JsonIgnore]
    public MongoUrl? MongoUrl { get; private set; } = null;

    public void SetMongoUrl(MongoUrl url)
    {
        MongoUrl = url;
        MongoDeserialisedUrl = url.ToString();
    }

    public static async Task<Config> ReadConfig(string path)
    {
        if (File.Exists(path))
        {
            string text = await File.ReadAllTextAsync(path);
            byte[] byteArray = Encoding.UTF8.GetBytes(text);
            MemoryStream stream = new(byteArray);

            Config cfg = await JsonDeser.DeserAsync<Config>(stream);

            return TryParseMongoUrl(cfg.MongoDeserialisedUrl, out MongoUrl? url) ? new Config() { MongoUrl = url } : cfg;
        }
        else
        {
            return new Config();
        }
        throw new Exception("Cannot read config");
    }

    public static async Task WriteToConfig(string path, Config config)
    {
        MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, config);
        string stringToWrite = Encoding.UTF8.GetString(stream.ToArray());
        await File.WriteAllTextAsync(path, stringToWrite);
    }

    // If string is a valid MongoUrl, return callback with string that was passed to the function, if it's not valid return callback with null
    public static void TryParseMongoUrl(in string url, Action<string?> callback)
    {
        try
        {
            _ = new MongoUrl(@url);
            callback(url);
        }
        catch (MongoConfigurationException)
        {
            Console.Error.WriteLine("Mongo URL is invalid. Perhaps try wrapping it in quotation marks");
            callback(null);
        }
        catch (ArgumentNullException)
        {
            callback(null);
        }
    }

    // When this method returns, mongoUrl parameter contains the result of parsing or null on failure
    public static bool TryParseMongoUrl(in string url, out MongoUrl? mongoUrl)
    {
        try
        {
            mongoUrl = new MongoUrl(@url);
            return true;
        }
        catch (Exception)
        {
            mongoUrl = null;
            return false;
        }
    }

}

