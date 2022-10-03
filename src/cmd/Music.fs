namespace MusicBot

open System
open System.Threading.Tasks
open DSharpPlus
open DSharpPlus.CommandsNext
open DSharpPlus.CommandsNext.Attributes
open DSharpPlus.Entities
open DSharpPlus.Lavalink
open System.Collections.Generic

type Music () =
    inherit BaseCommandModule ()
    
    member val Players = PlayerQueue ()

    (***** HELPER FUNCTIONS *****)
    member this.botIsConnected (ctx: CommandContext) = task {
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        return bot.VoiceState <> null
    }

    member this.connectTo (ctx: CommandContext) = task {
        let userVC = ctx.Member.VoiceState.Channel
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let! conn = node.ConnectAsync(userVC)
        conn.add_PlaybackStarted(this.Players.PlaybackStartedEvent)
        conn.add_PlaybackFinished(this.Players.PlaybackFinishedEvent)
        return conn
    }

    member this.findConnection (ctx: CommandContext) = task {
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let conn = node.GetGuildConnection(ctx.Guild)
        return conn
    }

    member this.disconnectCurrent (ctx: CommandContext, sameAsAuthor: bool) = task {
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let conn = node.GetGuildConnection(ctx.Guild)

        let userVC = ctx.Member.VoiceState.Channel
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        let botVC = bot.VoiceState.Channel

        if sameAsAuthor = (userVC = botVC) then
            do! this.Players.DropConnection(conn)
    }


    [<Command "join">]
    [<Aliases ("j")>]
    [<Description "Join the current voice channel">]
    member this.Join (ctx: CommandContext) : Task = task {
        let! inVC = this.botIsConnected ctx

        if inVC then
            do! this.disconnectCurrent (ctx, false)

        this.connectTo ctx |> ignore
    }

    [<Command "leave">]
    [<Aliases ("l", "quit", "die")>]
    [<Description "Leave the current voice channel">]
    member this.Leave (ctx: CommandContext) : Task = task {
        let userCh = ctx.Member.VoiceState.Channel
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        let botCh = bot.VoiceState.Channel

        do! this.disconnectCurrent (ctx, true)
    }

    [<Command "play">]
    [<Aliases ("p", "add")>]
    [<Description "Add a new song to the queue">]
    member this.Play (ctx: CommandContext, [<RemainingText>] search: string) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel
        let! inVC = this.botIsConnected ctx
        let! conn = if not inVC then this.connectTo ctx
                    else this.findConnection ctx

        if conn.Channel = userVC then
            let! searchResult = conn.GetTracksAsync(search)
            let track = searchResult.Tracks |> Seq.head
            let song = Song (track, ctx.Message)
            do! this.Players.AddTrack(conn, song)
    }

    [<Command "pause">]
    [<Aliases ("stop")>]
    [<Description "Pause the current track">]
    member this.Pause (ctx: CommandContext) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel
        let! conn = this.findConnection ctx
        if userVC = conn.Channel then do! conn.PauseAsync()
    }

    [<Command "resume">]
    [<Description "Resume the current track">]
    member this.Resume (ctx: CommandContext) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel
        let! conn = this.findConnection ctx
        if userVC = conn.Channel then do! conn.ResumeAsync()
    }

    [<Command "skip">]
    [<Description "Skip the current track">]
    member this.Skip (ctx: CommandContext) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel
        let! conn = this.findConnection ctx
        if userVC = conn.Channel then do! conn.StopAsync()
    }

    [<Command "clear">]
    [<Aliases ("clr", "skipall")>]
    [<Description "Clears out the queue">]
    member this.Clear (ctx: CommandContext) : Task = task {
        let userVC = ctx.Member.VoiceState.Channel
        let! conn = this.findConnection ctx
        if userVC = conn.Channel then this.Players.ClearQueue(conn)
    }

    [<Command "current">]
    [<Aliases ("curr", "now", "playing")>]
    [<Description "Get the currently playing track">]
    member this.Current (ctx: CommandContext) : Task = task {
        let! conn = this.findConnection ctx
        match this.Players.GetCurrentSong(conn) with
        | Some song ->
            let embed = DiscordEmbedBuilder()
            embed.Title <- "Currently Playing"
            embed.Description <- $"[{song.Track.Title}]({song.Track.Uri}) [{song.Message.Author.Mention}]"
            do! this.Players.UpdatePlayMessage(conn, song.Message, embed)
        | None -> ()
    }