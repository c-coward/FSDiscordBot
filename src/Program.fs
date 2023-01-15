namespace MusicBot

open DSharpPlus
open DSharpPlus.SlashCommands
open DSharpPlus.CommandsNext
open DSharpPlus.Interactivity
open DSharpPlus.Interactivity.Extensions
open DSharpPlus.Interactivity.Enums
open DSharpPlus.Lavalink
open DSharpPlus.Net
open Microsoft.Extensions.Configuration
open System
open System.IO
open System.Threading.Tasks
open MusicBot.Cmd

module Program =

    let appConfig =
        ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config/AppSettings.json", true, true)
            .Build()
    
    [<EntryPoint>]
    let main argv =
        printfn "Starting bot..."
    
        let config = DiscordConfiguration()
        config.Token <- appConfig.["Token"]
        config.TokenType <- TokenType.Bot
        config.Intents <-
            DiscordIntents.AllUnprivileged
            + DiscordIntents.MessageContents

        // Set up the client commands
        let client = new DiscordClient(config)
        
        let commandsConfig = CommandsNextConfiguration()
        commandsConfig.CaseSensitive <- false
        commandsConfig.StringPrefixes <- ["~"]

        let commands = client.UseCommandsNext(commandsConfig)
        commands.RegisterCommands<Music>()
        commands.RegisterCommands<Dice>()
        printfn "Commands registered"

        // Set up slash commands
        let slash = client.UseSlashCommands()
        slash.RegisterCommands<Wizard>()
        printfn "Slash commands registered"

        // Enables interactivity
        let interactionsConfig = InteractivityConfiguration()
        interactionsConfig.PollBehaviour <- PollBehaviour.KeepEmojis
        interactionsConfig.ButtonBehavior <-
            ButtonPaginationBehavior.DeleteMessage
        interactionsConfig.AckPaginationButtons <- true
        interactionsConfig.Timeout <- TimeSpan.FromSeconds(120)

        client.UseInteractivity(interactionsConfig) |> ignore
        printfn "Interactivity setup"

        // Set up the Lavalink connection
        let endpoint = ConnectionEndpoint(
            appConfig.["LLHostName"], appConfig.["LLPort"] |> int)

        let lavalinkConfig = LavalinkConfiguration()
        lavalinkConfig.Password <- appConfig.["LLPassword"]
        lavalinkConfig.RestEndpoint <- endpoint
        lavalinkConfig.SocketEndpoint <- endpoint

        let lavalink = client.UseLavalink()

        // Connect the client
        client.ConnectAsync()
        |> Async.AwaitTask |> Async.RunSynchronously
        printfn "Connected to discord..."

        // Connect to the lavalink server
        lavalink.ConnectAsync(lavalinkConfig)
        |> Async.AwaitTask |> Async.RunSynchronously
        |> ignore
        printfn "Connected to lavalink..."

        // Avoid early termination
        Task.Delay(-1)
        |> Async.AwaitTask |> Async.RunSynchronously

        1