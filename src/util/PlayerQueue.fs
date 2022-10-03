namespace MusicBot

open DSharpPlus.CommandsNext
open DSharpPlus.Entities
open DSharpPlus.Lavalink
open System.Collections.Generic
open System.Threading.Tasks


type Song (track: LavalinkTrack, msg: DiscordMessage) = 
    member val Track = track
    member val Message = msg

type PlayerContext () =
    member val Playlist = Queue<Song> ()
    // member val Channel = channel
    member val PlayMessage : option<DiscordMessage> = None with get, set
    member val QueueMessage : option<DiscordMessage> = None with get, set

type PlayerMap = Dictionary<uint64, PlayerContext>

type PlayerQueue () = 
    member val Players = new PlayerMap ()
    
    member this.AddConn (conn: LavalinkGuildConnection) =
        let guild = conn.Guild.Id
        this.Players.Add(guild, new PlayerContext())
    
    member this.AddTrack (conn: LavalinkGuildConnection, song: Song) =
        let guild = conn.Guild.Id
        if not (this.Players.ContainsKey(guild)) then
            this.AddConn(conn)
        this.Players.Item(guild).Playlist.Enqueue(song)

        let embed = DiscordEmbedBuilder()
        embed.Description <- $"Queued [{song.Track.Title}]({song.Track.Uri})"
        embed.Color <- DiscordColor.Purple
        
        if conn.CurrentState.CurrentTrack = null
            then this.PlayFrom(conn)  
            else this.UpdateQueueMessage(conn, song.Message, embed)
    
    member this.PlayFrom (conn: LavalinkGuildConnection) = task {
        let guild = conn.Guild.Id

        if this.Players.ContainsKey(guild) then
            let queue = this.Players.Item(guild).Playlist
            if queue.Count <> 0 then
                let song = queue.Peek()
                do! conn.PlayAsync(song.Track)
    }

    member this.UpdatePlayMessage (conn: LavalinkGuildConnection, msg: DiscordMessage, embed: DiscordEmbed) = task {
        let player = this.Players.Item(conn.Guild.Id)
        let oldMsg = player.PlayMessage

        match oldMsg with
        | Some x -> do! x.Channel.DeleteMessageAsync(x)
        | None -> ()

        let! newMsg = msg.RespondAsync(embed)
        player.PlayMessage <- Some newMsg
    }

    member this.UpdateQueueMessage (conn: LavalinkGuildConnection, msg: DiscordMessage, embed: DiscordEmbed) = task {
        let player = this.Players.Item(conn.Guild.Id)
        let oldMsg = player.QueueMessage

        match oldMsg with
        | Some x -> do! x.Channel.DeleteMessageAsync(x)
        | None -> ()

        let! newMsg = msg.RespondAsync(embed)
        player.QueueMessage <- Some newMsg
    }

    member this.AdvanceQueue (conn: LavalinkGuildConnection) =
        this.Players.Item(conn.Guild.Id).Playlist.Dequeue() |> ignore
    
    member this.ClearQueue (conn: LavalinkGuildConnection) =
        let guild = conn.Guild.Id
        this.Players.Item(guild).Playlist.Clear()
    
    member this.DropConnection (conn: LavalinkGuildConnection) = task {
        this.Players.Remove(conn.Guild.Id) |> ignore
        do! conn.DisconnectAsync()
    }
    
    member this.PlaybackStartedEvent : LavalinkGuildConnection -> EventArgs.TrackStartEventArgs -> Task = 
        fun (conn: LavalinkGuildConnection) (args: EventArgs.TrackStartEventArgs) ->
            task {
                let player = this.Players.Item(conn.Guild.Id)
                let song = player.Playlist.Peek()

                let embed = DiscordEmbedBuilder()
                embed.Title <- "Now Playing"
                embed.Description <- $"[{song.Track.Title}]({song.Track.Uri}) [{song.Message.Author.Mention}]"
                embed.Color <- DiscordColor.Purple

                do! this.UpdatePlayMessage(conn, song.Message, embed)
            }
    
    member this.PlaybackFinishedEvent : LavalinkGuildConnection -> EventArgs.TrackFinishEventArgs -> Task =
        fun (conn: LavalinkGuildConnection) (args: EventArgs.TrackFinishEventArgs) ->
            task {
                this.AdvanceQueue(conn)
                do! this.PlayFrom(conn)
            }