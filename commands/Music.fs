namespace MusicBot

open System
open System.Threading.Tasks
open DSharpPlus
open DSharpPlus.CommandsNext
open DSharpPlus.CommandsNext.Attributes
open DSharpPlus.Entities
open DSharpPlus.Lavalink

type Music () =

    inherit BaseCommandModule ()

    (***** HELPER FUNCTIONS *****)
    let botIsConnected (ctx: CommandContext) = task {
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        return bot.VoiceState <> null
    }

    let connectTo (ctx: CommandContext) = task {
        let userVC = ctx.Member.VoiceState.Channel
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let! conn = node.ConnectAsync(userVC)
        return conn
    }

    let getConn (ctx: CommandContext) = task {
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let conn = node.GetGuildConnection(ctx.Guild)

        return conn
    }

    let disconnectCurrent (ctx: CommandContext, sameAsAuthor: bool) = task {
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let conn = node.GetGuildConnection(ctx.Guild)

        let userVC = ctx.Member.VoiceState.Channel
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        let botVC = bot.VoiceState.Channel

        if sameAsAuthor = (userVC = botVC) then
            conn.DisconnectAsync() |> ignore
    }

    [<Command "join">]
    [<Aliases ("j")>]
    [<Description "Join the current voice channel">]
    member this.Join (ctx: CommandContext) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel
        let lavalink = ctx.Client.GetLavalink()
        let node = lavalink.ConnectedNodes.Values |> Seq.head
        let! inVC = botIsConnected ctx

        if inVC then
            do! disconnectCurrent (ctx, false)

        connectTo ctx |> ignore
    }

    [<Command "leave">]
    [<Aliases ("l", "quit", "die")>]
    [<Description "Leave the current voice channel">]
    member this.Leave (ctx: CommandContext) : Task = task {
        let userCh = ctx.Member.VoiceState.Channel
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        let botCh = bot.VoiceState.Channel

        do! disconnectCurrent (ctx, true)
    }

    [<Command "play">]
    [<Aliases ("p", "add", "search")>]
    [<Description "Add a new song to the queue">]
    member this.Play (ctx: CommandContext, [<RemainingText>] search: string) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel // Ensure the user is connected
        let! inVC = botIsConnected ctx
        let! conn = if not inVC then connectTo ctx
                    else getConn ctx

        if conn.Channel = userVC then
            let! searchResult = conn.GetTracksAsync(search)
            do! conn.PlayAsync(searchResult.Tracks |> Seq.head)
            return ()

    }