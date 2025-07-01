using System.Text;
using discord_DickBot.Utils;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace discord_DickBot
{
    public static class Program
    {
        private static DiscordSocketClient? bot = new();
        private static CancellationTokenSource? cts;

        public static async Task Main()
        {
            Logger.Bot("Bot starting", "INFO");

            cts = new CancellationTokenSource();

            bot.Log += Log;
            bot.Ready += Client_Ready;
            bot.SlashCommandExecuted += slashCommandHandler;

            await bot.LoginAsync(TokenType.Bot, Secrets.PRODUCTION_TOKEN);
            await bot.StartAsync();

            AppDomain.CurrentDomain.ProcessExit += (_, _) => cts?.Cancel();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts?.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (Exception)
            {
                Logger.Bot("Bot shutting down", "INFO");
            }
        }

        private static Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage.Message);
            return Task.CompletedTask;
        }

        private static async Task slashCommandHandler(SocketSlashCommand command)
        {
            string responseString = string.Empty;
            if (command.Data.Name == "dih")
            {
                var db = new Database();

                var result = await db.DailyRoll(command.User.Id, (ulong)command.GuildId!, command.User.Username);
                var user = await db.GetUserAsync(command.User.Id, (ulong)command.GuildId!);

                switch (result.result)
                {
                    case DickSizeResult.AlreadyRolled:
                        responseString = "**You've already rolled...**\n" +
                                         $"Right now your pp = *{user.DickSize} cm (+0, nothing changed)*\n" +
                                         $"Next attempt tomorrow!";
                        await command.RespondAsync(responseString, ephemeral: true);
                        return;
                    case DickSizeResult.LuckyRoll:
                        responseString = "**Lucky roll!**\n";
                        break;
                }

                responseString +=
                    $"{user.Username}, your pp now = *{user.DickSize} cm ({(result.change >= 0 ? "+" : "-")}{Math.Abs(result.change)})*\n" +
                    $"Now you have *{user.Coins} coins (+{Math.Abs(result.change) / 2})* \n" +
                    $"Additional attempts left: *{user.AdditionalAttempts}*\n" +
                    $"*You place {await db.TopPlacement(command.User.Id, (ulong)command.GuildId!)} place in the chat.*\n" +
                    $"Next attempt tomorrow!";
            }

            await command.RespondAsync($"{responseString}", ephemeral: true);
        }

        public static async Task Client_Ready()
        {
            // Note: Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
            // Descriptions can have a max length of 100.

            // Let's do our global command
            var globalCommand = new SlashCommandBuilder();
            globalCommand.WithName("dih");
            globalCommand.WithDescription("grow ur dih");

            try
            {
                // With global commands we don't need the guild.
                await bot.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (ApplicationCommandException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                string json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }
        }
    }
}
