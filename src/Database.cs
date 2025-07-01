using System.Security.Cryptography;
using discord_DickBot.Exceptions;
using discord_DickBot.Structures;
using MongoDB.Driver;

namespace discord_DickBot
{
    public enum DickSizeResult
    {
        LuckyRoll,
        BasicRoll,
        AlreadyRolled
    }

    public class Database
    {
        //* the brain of the bot

        //constants
        private const string ip = "192.168.222.222";
        private const int port = 27017; // default mongodb port

        private const string database_name = "discordDick";

        // collections
        private readonly IMongoCollection<User> userCollection;


        /// <summary>
        /// Constructor that connects to the MongoDB, gets the database, and inside that database gets a collection of users
        /// </summary>
        public Database()
        {
            string connectionString = $"mongodb://{Secrets.DB_USERNAME}:{Secrets.DB_PASSWORD}@{ip}:{port}";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(database_name);

            userCollection = database.GetCollection<User>("Users");
        }


        public async Task<User> GetUserAsync(ulong userId, ulong chatId)
        {
            var user = await userCollection.Find(u => u.UId == userId && u.CId == chatId).FirstOrDefaultAsync();
            if (user == null)
            {
                await createUserAsync(userId, chatId);
                user = await userCollection.Find(u => u.UId == userId && u.CId == chatId).FirstOrDefaultAsync();
            }

            return user;
        }

        private async Task createUserAsync(ulong userId, ulong chatId)
        {
            var user = new User { UId = userId, CId = chatId };
            await userCollection.InsertOneAsync(user);
        }

        public async Task<(DickSizeResult result, int change)> DailyRoll(ulong userId, ulong chatId, string name)
        {
            var user = await GetUserAsync(userId, chatId);

            // get the date in yekaterinburg time
            var ekaterinburgTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Yekaterinburg");
            var ekaterinburgNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ekaterinburgTimeZone);
            var ekaterinburgToday = ekaterinburgNow.Date;

            var lastAttemptEkaterinburg = TimeZoneInfo.ConvertTimeFromUtc(user.LastAttempt, ekaterinburgTimeZone).Date;

            bool isAdditionalAttempt = lastAttemptEkaterinburg == ekaterinburgToday;

            if (isAdditionalAttempt && user.AdditionalAttempts <= 0)
            {
                return (DickSizeResult.AlreadyRolled, 0);
            }

            // finally, roll if nothing bad happens
            (bool isLucky, int change) = await roll(userId, chatId, isAdditionalAttempt, name);
            return (isLucky ? DickSizeResult.LuckyRoll : DickSizeResult.BasicRoll, change);
        }

        private async Task<(bool isLucky, int change)> roll(ulong userId, ulong chatId, bool isAdditionalAttempt,
            string name)
        {
            int change = RandomNumberGenerator.GetInt32(1, 11); // 1 to 10
            int luck = RandomNumberGenerator.GetInt32(1, 1001);
            int roll = RandomNumberGenerator.GetInt32(1, 101);

            bool isLucky = luck <= 25;

            if (isLucky)
            {
                change = RandomNumberGenerator
                    .GetInt32(5, 11); // 5 to 10, people were whining about how lucky roll gave them 4 cm (2*2)
                change *= 2; // 10 to 20
            }
            else if (roll <= 20)
            {
                change = -Math.Abs(change); // -1 to -10
            }

            // gotta do this dumbass conversion because I'm making the bot as close to the original as possible...
            // the timezone of the original bot is yekaterinburg, that's like +4 or 5 hours from UTC.
            var ekaterinburgTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Yekaterinburg");
            var ekaterinburgNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ekaterinburgTimeZone);
            var ekaterinburgToday = ekaterinburgNow.Date;

            var ekaterinburgTodayUtc = TimeZoneInfo.ConvertTimeToUtc(ekaterinburgToday, ekaterinburgTimeZone);

            var update = Builders<User>.Update
                    .Inc(u => u.DickSize, change)
                    .Inc(u => u.Coins, Math.Abs(change) / 2) /* half of what user grown
                                                                          (7 cm/2 = 3 coins (int rounding is taken in an account)) */
                    .Set(u => u.LastAttempt, ekaterinburgTodayUtc)
                    .Set(u => u.Username, name); /* cache name for leaderboards, because
                                                               pulling the name from uids is a recipe for a quick rate limit */

            if (isAdditionalAttempt)
            {
                update = update.Inc(u => u.AdditionalAttempts, -1);
            }

            await userCollection.UpdateOneAsync(u => u.UId == userId && u.CId == chatId, update);

            return (isLucky, change);
        }

        public async Task BuyAdditionalAttempt(ulong userId, ulong chatId)
        {
            var user = await GetUserAsync(userId, chatId);

            if (user.Coins < 25)
            {
                throw new Message("Недостаточно монет.");
            }

            var update = Builders<User>.Update.Inc(u => u.AdditionalAttempts, 1).Inc(u => u.Coins, -25);
            await userCollection.UpdateOneAsync(u => u.UId == userId && u.CId == chatId, update);
        }

        public async Task<int> TopPlacement(ulong userId, ulong chatId)
        {
            var filter = Builders<User>.Filter.Eq(u => u.CId, chatId);
            var sort = Builders<User>.Sort.Descending(u => u.DickSize);
            var cursor = await userCollection.FindAsync(filter, new FindOptions<User> { Sort = sort });
            var userList = await cursor.ToListAsync();

            int index = userList.FindIndex(u => u.UId == userId);
            return index >= 0 ? index + 1 : 0; // Return 1-based rank or 0 if user not found
        }

        public async Task<List<User>> TopTenChat(ulong chatId)
        {
            var filter = Builders<User>.Filter.Eq(u => u.CId, chatId);
            var sort = Builders<User>.Sort.Descending(u => u.DickSize);

            var cursor = await userCollection.FindAsync(filter, new FindOptions<User> { Sort = sort, Limit = 10 });
            var userList = await cursor.ToListAsync();

            return userList;
        }

        public async Task<List<User>> TopTenGlobal(ulong commandGuildId)
        {
            var filter = Builders<User>.Filter.Empty;
            var sort = Builders<User>.Sort.Descending(u => u.DickSize);

            var cursor = await userCollection.FindAsync(filter, new FindOptions<User> { Sort = sort, Limit = 10 });
            var userList = await cursor.ToListAsync();

            return userList;
        }
    }
}
