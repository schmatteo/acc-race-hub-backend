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

        public Driver[]? Drivers { get; set; }

        public int RaceNumber { get; set; }

        public int ForcedCarModel { get; set;  }

        public int OverrideDriverInfo { get; set; }

        public int DefaultGridPosition { get; set; }

        public int IsServerAdmin { get; set; }
    }

    public class Driver
    {
        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? ShortName { get; set; }

        public int Nationality { get; set; }

        public int DriverCategory { get; set; }
        // might cause chaos
        public string? PlayerID { get; set; }
    }

    public class DriversCollection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? PlayerId { get; set; }

        public int Points { get; set; }

        [BsonElement("pointsWDrop")] public int PointsWithDrop { get; set; }

        // track: [finishing_position (-1 = dns, -2 = dnf), fastest_lap?, points_for_the_round]
        [BsonIgnore] public Dictionary<string, Tuple<int, bool, int>> OldFinishes { get; set; } = new();

        public Finish[]? Finishes { get; set; }

        public int RoundDropped { get; set; }

        public class Finish
        {
            public string? TrackName { get; set; }

            public int FinishingPosition { get; set; }

            public bool FastestLap { get; set; }

            public int Points { get; set; }
        }
    }

    public class TeamsCollection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("team")] public string? TeamName { get; set; }

        public string[] Drivers { get; set; } = new string[2];

        public Maps.Classes? Class { get; set; }

        public int Points { get; set; }

        [BsonElement("pointsWDrop")] public int PointsWithDrop { get; set; }
    }
    
    public class DriverInRaceResults
    {
        public string? PlayerId { get; set; }

        public int BestLap { get; set; }

        public int LapCount { get; set; }

        public int TotalTime { get; set; }
    }
}