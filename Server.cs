using System.Net;
using System.Text;

internal class HttpServer
{
    private static HttpListener listener;
    private static readonly List<string> urls = new() { "http://localhost:4001/", "http://127.0.0.1:4001/" };
    private static string pageData = "";
    private static bool runServer = true;

    private static async Task HandleIncomingConnections()
    {
        while (runServer)
        {
            HttpListenerContext ctx = await listener!.GetContextAsync();

            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;

            Console.WriteLine(req.HttpMethod);
            Console.WriteLine(req.UserHostName);
            Console.WriteLine(req.UserAgent);
            System.Console.WriteLine(req.Url?.AbsolutePath);
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
                default:
                    break;
            }

            pageData = req.Url?.AbsolutePath;
            byte[] data = Encoding.UTF8.GetBytes(pageData ?? "");
            res.ContentType = "application/json";
            res.ContentEncoding = Encoding.UTF8;
            res.ContentLength64 = data.LongLength;

            await res.OutputStream.WriteAsync(data, 0, data.Length);
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
        listener = new HttpListener();
        foreach (string url in urls)
        {
            listener.Prefixes.Add(url);
            Console.WriteLine("Listening on {0}", url);
        }
        listener.Start();

        Task listenTask = HandleIncomingConnections();
        listenTask.GetAwaiter().GetResult();

        listener.Close();
    }

    public static void Stop() { runServer = false; }

    private enum DataTypes
    {
        Championship,
        Teams,
        Constructors,
        ClassQualifying,
        RaceResults
    }
}
