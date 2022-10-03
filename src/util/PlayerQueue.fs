namespace MusicBot

open DSharpPlus.CommandsNext
open DSharpPlus.Entities
open DSharpPlus.Lavalink
open System.Collections.Generic
open System.Threading.Tasks


type Song (track: LavalinkTrack, msg: DiscordMessage) = 
    member val Track = track
    member val Message = msg

type PlayerContext (channel: DiscordChannel) =
    member val Playlist = Queue<Song> ()
    // member val Channel = channel
    member val PlayMessage : option<DiscordMessage> = None with get, set
    member val QueueMessage : option<DiscordMessage> = None with get, set

type PlayerMap = Dictionary<uint64, PlayerContext>

type PlayerQueue () = 
    member val Players = new PlayerMap ()
    
    member this.AddConn (conn: LavalinkGuildConnection, channel: DiscordChannel) =
        let guild = conn.Guild.Id
        this.Players.Add(guild, new PlayerContext(channel))
    
    member this.AddTrack (conn: LavalinkGuildConnection, song: Song, channel: DiscordChannel) =
        let guild = conn.Guild.Id
        if not (this.Players.ContainsKey(guild)) then
            this.AddConn(conn, channel)
        this.Players.Item(guild).Playlist.Enqueue(song)
        printfn $"Added track {song.Track.Title}"
    
    member this.PlayFrom (conn: LavalinkGuildConnection) = task {
        let guild = conn.Guild.Id

        if this.Players.ContainsKey(guild) then
            let queue = this.Players.Item(guild).Playlist
            if queue.Count <> 0 then
                let song = queue.Peek()
                do! conn.PlayAsync(song.Track)
    }

    member this.AdvanceQueue (conn: LavalinkGuildConnection) =
        this.Players.Item(conn.Guild.Id).Playlist.Dequeue() |> ignore
    
    member this.ClearQueue (conn: LavalinkGuildConnection) =
        let guild = conn.Guild.Id
        this.Players.Item(guild).Playlist.Clear()
    
    member this.DropConnection (conn: LavalinkGuildConnection) =
        let guild = conn.Guild.Id
        this.Players.Remove(guild)
    
    member this.PlaybackStartedEvent : LavalinkGuildConnection -> EventArgs.TrackStartEventArgs -> Task = 
        fun (conn: LavalinkGuildConnection) (args: EventArgs.TrackStartEventArgs) ->
            task {
                let guild = conn.Guild.Id
                let player = this.Players.Item(guild)
                let invoker = player.Playlist.Peek().Message
                
                let! newMsg = invoker.RespondAsync($"Playing {args.Track.Title}")
                player.PlayMessage <- Some newMsg

                // ctx.RespondAsync($"Playing {args.Track.Title}") |> ignore
            }
    
    member this.PlaybackFinishedEvent : LavalinkGuildConnection -> EventArgs.TrackFinishEventArgs -> Task =
        fun (conn: LavalinkGuildConnection) (args: EventArgs.TrackFinishEventArgs) ->
            task {
                let guild = conn.Guild.Id
                let player = this.Players.Item(guild)
                let oldMsg = player.PlayMessage
                match oldMsg with
                | Some x -> do!
                    printfn "Deleted message"
                    x.Channel.DeleteMessageAsync(x)
                | None -> printfn "Nothing to delete..."
                this.AdvanceQueue(conn)
                do! this.PlayFrom(conn)
            }