using MongoDB.Driver;
using System;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

class Config
{
    [JsonRequired]
    [JsonPropertyName("mongoUrl")]
    public string MongoDeserialisedUrl { get; init; } = "";

    [JsonIgnore]
    public MongoUrl? MongoUrl { get; init; } = null;

    public static async Task<Config> ReadConfig(string path)
    {
        if (File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path);
            byte[] byteArray = Encoding.UTF8.GetBytes(text);
            MemoryStream stream = new(byteArray);

            var cfg = await JsonDeser.DeserConfigAsync(stream);

            return TryParseMongoUrl(cfg.MongoDeserialisedUrl, out MongoUrl? url) ? new Config() { MongoUrl = url } : cfg;
        }
        else
        {
            try
            {
                File.Create(path);
            }
            catch (Exception)
            {
                throw new Exception("Cannot create a config file");
            }
        }
        throw new Exception("Cannot read config");
    }

    //public static async Task WriteToConfig(string path, Config config)
    //{

    //}

    // If string is a valid MongoUrl, return callback with string that was passed to the function, if it's not valid return callback with null
    public static void TryParseMongoUrl(string url, Action<string?> callback)
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
    public static bool TryParseMongoUrl(string url, out MongoUrl? mongoUrl)
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

