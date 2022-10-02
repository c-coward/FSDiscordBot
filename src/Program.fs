namespace MusicBot

module Program =

    open DSharpPlus
    open DSharpPlus.CommandsNext
    open DSharpPlus.Lavalink
    open DSharpPlus.Net
    open Microsoft.Extensions.Configuration
    open System.IO
    open System.Threading.Tasks

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


        // Set up the client commands
        let client = new DiscordClient(config)

        let commandsConfig = CommandsNextConfiguration()
        commandsConfig.CaseSensitive <- false
        commandsConfig.StringPrefixes <- ["~"]

        let commands = client.UseCommandsNext(commandsConfig)
        commands.RegisterCommands<Music>()

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
        printfn "Bot connected to discord..."

        // Connect to the lavalink server
        lavalink.ConnectAsync(lavalinkConfig)
        |> Async.AwaitTask |> Async.RunSynchronously
        |> ignore
        printfn "Connected to lavalink..."

        // Avoid early termination
        Task.Delay(-1)
        |> Async.AwaitTask |> Async.RunSynchronously

        1