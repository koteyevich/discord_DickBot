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
                        responseString = "**:flushed: You've already rolled...**\n" +
                                         $":eggplant: Right now your pp = *{user.DickSize} cm (+0, nothing changed)*\n" +
                                         $":alarm_clock: Next attempt tomorrow!";
                        await command.RespondAsync(responseString, ephemeral: true);
                        return;
                    case DickSizeResult.LuckyRoll:
                        responseString = "**:four_leaf_clover: Lucky roll!**\n";
                        break;
                }

                responseString +=
                    $":eggplant: {user.Username}, your pp now = *{user.DickSize} cm ({(result.change >= 0 ? "+" : "-")}{Math.Abs(result.change)})*\n" +
                    $":coin: Now you have *{user.Coins} coins (+{Math.Abs(result.change) / 2})* \n" +
                    $"Additional attempts left: *{user.AdditionalAttempts}*\n" +
                    $":trophy: *You place {await db.TopPlacement(command.User.Id, (ulong)command.GuildId!)} place in the chat.*\n" +
                    $":arrow_right_hook: Next attempt tomorrow!";
            }

            if (command.Data.Name == "top-dih")
            {
                var db = new Database();
                var list = await db.TopTenChat((ulong)command.GuildId!);
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("**Top 10 dih in chat**");
                for (int i = 0; i < list.Count; i++)
                {
                    sb.AppendLine($"***[{i + 1}]*** *{list[i].Username} - {list[i].DickSize} cm*");
                }

                responseString = sb.ToString();
            }

            if (command.Data.Name == "global-dih")
            {
                var db = new Database();
                var list = await db.TopTenGlobal((ulong)command.GuildId!);
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("**Top 10 dih globally**");
                for (int i = 0; i < list.Count; i++)
                {
                    sb.AppendLine($"***[{i + 1}]*** *{list[i].Username} - {list[i].DickSize} cm*");
                }

                responseString = sb.ToString();
            }

            await command.RespondAsync($"{responseString}", ephemeral: true);
        }

        private static async Task Client_Ready()
        {
            // Note: Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
            // Descriptions can have a max length of 100.

            // Let's do our global command
            var dihCommand = new SlashCommandBuilder();
            dihCommand.WithName("dih");
            dihCommand.WithDescription("grow ur dih");

            var topDihCommand = new SlashCommandBuilder();
            topDihCommand.WithName("top-dih");
            topDihCommand.WithDescription("top 10 dih in chat");

            var globalDihCommand = new SlashCommandBuilder();
            globalDihCommand.WithName("global-dih");
            globalDihCommand.WithDescription("top 10 dih across all chats");

            try
            {
                var guild = bot.GetGuild(1389602334136467546);

                // With global commands we don't need the guild.
                await bot.CreateGlobalApplicationCommandAsync(dihCommand.Build());
                await bot.CreateGlobalApplicationCommandAsync(topDihCommand.Build());
                await bot.CreateGlobalApplicationCommandAsync(globalDihCommand.Build());


                await guild.CreateApplicationCommandAsync(dihCommand.Build());
                await guild.CreateApplicationCommandAsync(topDihCommand.Build());
                await guild.CreateApplicationCommandAsync(globalDihCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
    }
}
