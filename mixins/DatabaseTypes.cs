using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;

internal class DatabaseTypes
{
    public class EntrylistEntry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("drivers")]
        public Driver[]? Drivers { get; set; }

        [BsonElement("raceNumber")]
        public int RaceNumber { get; set; }

        [BsonElement("forcedCarModel")]
        public int ForcedCarModel { get; }

        [BsonElement("overrideDriverInfo")]
        public int OverrideDriverInfo { get; set; }

        [BsonElement("defaultGridPosition")]
        public int DefaultGridPosition { get; set; }

        [BsonElement("isServerAdmin")]
        public int IsServerAdmin { get; set; }
    }
    public class Driver
    {
        [BsonElement("firstName")]
        public string? FirstName { get; set; }

        [BsonElement("lastName")]
        public string? LastName { get; set; }

        [BsonElement("shortName")]
        public string? ShortName { get; set; }

        [BsonElement("nationality")]
        public int Nationality { get; set; }

        [BsonElement("driverCategory")]
        public int DriverCategory { get; set; }

        [BsonElement("playerID")]
        public string? PlayerID { get; set; }
    }
    public class DriverInChampionshipStandings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("playerId")]
        public string? PlayerId { get; set; }

        [BsonElement("points")]
        public int Points { get; set; }

        [BsonElement("pointsWDrop")]
        public int PointsWithDrop { get; set; }

        // track: [finishing_position (-1 = dns, -2 = dnf), fastest_lap?, points_for_the_round]
        [BsonIgnore]
        public Dictionary<string, Tuple<int, bool, int>> OldFinishes { get; set; } = new();

        public class Finish
        {
            [BsonElement("trackName")]
            public string? TrackName { get; set; }

            [BsonElement("finishingPosition")]
            public int FinishingPosition { get; set; }

            [BsonElement("fastestLap")]
            public bool FastestLap { get; set; }

            [BsonElement("points")]
            public int Points { get; set; }
        }

        [BsonElement("finishes")]
        public Finish[]? Finishes { get; set; }

        [BsonElement("roundDropped")]
        public int RoundDropped { get; set; }
    }
}
