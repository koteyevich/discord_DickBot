using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace discord_DickBot.Structures
{
    public class User
    {
        [BsonId] public ObjectId Id { get; set; } = new();

        [BsonRepresentation(BsonType.String)] public ulong UId { get; set; }
        [BsonRepresentation(BsonType.String)] public ulong CId { get; set; }

        [BsonRepresentation(BsonType.String)] public string? Username { get; set; }

        [BsonRepresentation(BsonType.Int32)] public int DickSize { get; set; }
        [BsonRepresentation(BsonType.Int32)] public int Coins { get; set; } = 0;

        [BsonRepresentation(BsonType.Int32)] public int AdditionalAttempts { get; set; } = 0;
        public DateTime LastAttempt { get; set; }
    }
}
