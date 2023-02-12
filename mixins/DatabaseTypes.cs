using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

internal class DatabaseTypes
{
    public class EntrylistCollection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("drivers")] public Driver[]? Drivers { get; set; }

        [BsonElement("raceNumber")] public int RaceNumber { get; set; }

        [BsonElement("forcedCarModel")] public int ForcedCarModel { get; }

        [BsonElement("overrideDriverInfo")] public int OverrideDriverInfo { get; set; }

        [BsonElement("defaultGridPosition")] public int DefaultGridPosition { get; set; }

        [BsonElement("isServerAdmin")] public int IsServerAdmin { get; set; }
    }

    public class Driver
    {
        [BsonElement("firstName")] public string? FirstName { get; set; }

        [BsonElement("lastName")] public string? LastName { get; set; }

        [BsonElement("shortName")] public string? ShortName { get; set; }

        [BsonElement("nationality")] public int Nationality { get; set; }

        [BsonElement("driverCategory")] public int DriverCategory { get; set; }

        [BsonElement("playerID")] public string? PlayerID { get; set; }
    }

    public class DriversCollection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("playerId")] public string? PlayerId { get; set; }

        [BsonElement("points")] public int Points { get; set; }

        [BsonElement("pointsWDrop")] public int PointsWithDrop { get; set; }

        // track: [finishing_position (-1 = dns, -2 = dnf), fastest_lap?, points_for_the_round]
        [BsonIgnore] public Dictionary<string, Tuple<int, bool, int>> OldFinishes { get; set; } = new();

        [BsonElement("finishes")] public Finish[]? Finishes { get; set; }

        [BsonElement("roundDropped")] public int RoundDropped { get; set; }

        public class Finish
        {
            [BsonElement("trackName")] public string? TrackName { get; set; }

            [BsonElement("finishingPosition")] public int FinishingPosition { get; set; }

            [BsonElement("fastestLap")] public bool FastestLap { get; set; }

            [BsonElement("points")] public int Points { get; set; }
        }
    }

    public class TeamsCollection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("team")] public string? TeamName { get; set; }

        [BsonElement("drivers")] public string[] Drivers { get; set; } = new string[2];

        [BsonElement("class")] public Maps.Classes? Class { get; set; }

        [BsonElement("points")] public int Points { get; set; }

        [BsonElement("pointsWDrop")] public int PointsWithDrop { get; set; }
    }
}