using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

internal class HttpServer
{
    private static HttpListener? _listener;
    private static readonly List<string> Urls = new() { "http://localhost:4001/", "http://127.0.0.1:4001/" };
    private static string _pageData = "";
    private static bool _runServer = true;

    private static async Task HandleIncomingConnections()
    {
        while (_runServer)
        {
            var ctx = await _listener!.GetContextAsync();

            var req = ctx.Request;
            var res = ctx.Response;

            Console.WriteLine(req.HttpMethod);
            Console.WriteLine(req.UserHostName);
            Console.WriteLine(req.UserAgent);
            Console.WriteLine(req.Url?.AbsolutePath);
            Console.WriteLine();

            switch (req.Url?.AbsolutePath)
            {
                case "/championship":
                    GetData(DataTypes.Championship);
                    break;
                case "/teams":
                    GetData(DataTypes.Teams);
                    break;
                case "/constructors":
                    GetData(DataTypes.Constructors);
                    break;
                case "/classqualifying":
                    GetData(DataTypes.ClassQualifying);
                    break;
                case "/raceresults":
                    GetData(DataTypes.RaceResults);
                    break;
            }

            _pageData = req.Url?.AbsolutePath;
            var data = Encoding.UTF8.GetBytes(_pageData ?? "");
            res.ContentType = "application/json";
            res.ContentEncoding = Encoding.UTF8;
            res.ContentLength64 = data.LongLength;

            await res.OutputStream.WriteAsync(data);
            res.Close();
        }
    }

    private static void GetData(DataTypes type)
    {
        switch (type)
        {
            case DataTypes.Championship:
                Console.WriteLine("Championship");
                break;
            case DataTypes.Teams:
                Console.WriteLine("Teams");
                break;
            case DataTypes.Constructors:
                Console.WriteLine("Constructors");
                break;
            case DataTypes.ClassQualifying:
                Console.WriteLine("Class Qualifying");
                break;
            case DataTypes.RaceResults:
                Console.WriteLine("Race Results");
                break;
            default:
                Console.Error.WriteLine("Unknown data type query");
                break;
        }
    }

    public static void Run()
    {
        _listener = new HttpListener();
        foreach (var url in Urls)
        {
            _listener.Prefixes.Add(url);
            Console.WriteLine($"Listening on {url}");
        }

        _listener.Start();

        var listenTask = HandleIncomingConnections();
        listenTask.GetAwaiter().GetResult();

        _listener.Close();
    }

    public static void Stop()
    {
        _runServer = false;
    }

    private enum DataTypes
    {
        Championship,
        Teams,
        Constructors,
        ClassQualifying,
        RaceResults
    }
}