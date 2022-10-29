namespace MusicBot.Cmd

open System
open System.Threading.Tasks
open DSharpPlus.CommandsNext
open DSharpPlus.CommandsNext.Attributes
open DSharpPlus.Entities
open DSharpPlus.Lavalink

open MusicBot.Util.PlayerQueue

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

    member this.verifyConnection (ctx: CommandContext) = task {
        let userVC = ctx.Member.VoiceState.Channel
        let! conn = this.findConnection (ctx)
        if userVC = conn.Channel then
            return conn
        else return null
    }

    member this.disconnectCurrent (ctx: CommandContext, sameAsAuthor: bool) = task {
        let node = ctx.Client.GetLavalink().ConnectedNodes.Values |> Seq.head
        let conn = node.GetGuildConnection(ctx.Guild)

        let userVC = ctx.Member.VoiceState.Channel
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        let botVC = bot.VoiceState.Channel

        if sameAsAuthor = (userVC = botVC) then
            do! conn.DisconnectAsync()
    }

    member this.StringTime (time: TimeSpan) =
        let hours = time.TotalHours |> int
        let minutes = time.Minutes
        let seconds = time.Seconds

        if hours > 0 then
            $"{hours}:%02i{minutes}:%02i{seconds}"
        else
            $"%02i{minutes}:%02i{seconds}"

    (***** COMMANDS *****)
    [<Command "join">]
    [<Aliases ("j")>]
    [<Description "Join the current voice channel">]
    member this.Join (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! inVC = this.botIsConnected ctx

        if inVC then
            do! this.disconnectCurrent (ctx, false)

        this.connectTo ctx |> ignore
    }

    [<Command "leave">]
    [<Aliases ("l", "quit", "die")>]
    [<Description "Leave the current voice channel">]
    member this.Leave (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let userCh = ctx.Member.VoiceState.Channel
        let! bot = ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id)
        let botCh = bot.VoiceState.Channel

        do! this.disconnectCurrent (ctx, true)
        do! ctx.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("👋"))
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
            printfn $"{searchResult.LoadResultType}"
            if searchResult.LoadResultType = LavalinkLoadResultType.PlaylistLoaded then
                printfn "Adding playlist"
                for track in searchResult.Tracks do
                    let song = Song (track, ctx.Message)
                    do! this.Players.AddTrack(conn, song)
            else
                let track = searchResult.Tracks |> Seq.head
                let song = Song (track, ctx.Message)
                do! this.Players.AddTrack(conn, song)
    }

    [<Command "pause">]
    [<Aliases ("stop")>]
    [<Description "Pause the current track">]
    member this.Pause (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.verifyConnection (ctx)
        if this.Players.GetCurrentSong(conn).IsSome then
            do! conn.PauseAsync()
            do! ctx.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("⏸️"))
    }

    [<Command "resume">]
    [<Description "Resume the current track">]
    member this.Resume (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.verifyConnection (ctx)
        if this.Players.GetCurrentSong(conn).IsSome then
            do! conn.ResumeAsync()
            do! ctx.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("▶️"))
    }

    [<Command "skip">]
    [<Aliases ("remove")>]
    [<Description "Skip the current track, or a specific track in the queue">]
    member this.Skip (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.verifyConnection (ctx)
        if this.Players.GetCurrentSong(conn).IsSome then
            do! conn.StopAsync()
            do! ctx.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("⏩"))
    }
    member this.Remove (ctx: CommandContext, loc: int, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.verifyConnection (ctx)
        if loc = 1 then
            do! this.Skip(ctx, txt)
        else
            this.Players.RemoveAt(conn, loc)
    }
    [<Command "clear">]
    [<Aliases ("clr", "skipall")>]
    [<Description "Clears out the queue">]
    member this.Clear (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.verifyConnection (ctx)
        this.Players.ClearQueue(conn)
    }

    [<Command "current">]
    [<Aliases ("curr", "now", "playing")>]
    [<Description "Get the currently playing track">]
    member this.Current (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.findConnection ctx
        let embed = DiscordEmbedBuilder()
        embed.Color <- DiscordColor.Purple

        match this.Players.GetCurrentSong(conn) with
        | Some song ->
            embed.Title <- "Currently Playing"
            embed.Description <- $"[{song.Track.Title}]({song.Track.Uri}) [{song.Message.Author.Mention}] | `{conn.CurrentState.PlaybackPosition |> this.StringTime} / {song.Track.Length |> this.StringTime}`"
        | None -> 
            embed.Description <- "Not playing anything..."
        do! this.Players.UpdatePlayMessage(conn, ctx.Message, embed)
    }

    [<Command "queue">]
    [<Aliases ("q", "list", "songs", "tracks")>]
    [<Description "Lists out the queue">]
    member this.GetQueue (ctx: CommandContext, [<RemainingText>] txt: string) : Task = task {
        let! conn = this.findConnection ctx
        let embed = this.Players.StringQueue(conn)
        ctx.RespondAsync(embed) |> ignore
    }