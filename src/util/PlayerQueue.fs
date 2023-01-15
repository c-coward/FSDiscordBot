namespace MusicBot.Util

open DSharpPlus.Entities
open DSharpPlus.Lavalink
open System
open System.Collections.Generic
open System.Threading.Tasks


module PlayerQueue =
    type Song (track: LavalinkTrack, msg: DiscordMessage) = 
        member val Track = track
        member val Message = msg

    type PlayerContext () =
        member val Playlist = Queue<Song> () with get, set
        member val PlayMessage : option<DiscordMessage> = None with get, set
        member val QueueMessage : option<DiscordMessage> = None with get, set
        member val isPaused : bool = false with get, set

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
            let currentSong = this.GetCurrentSong(conn)
            match currentSong with
            | Some (song : Song) -> do! conn.PlayAsync(song.Track)
            | None -> ()
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

        member this.GetQueue (conn: LavalinkGuildConnection) =
            this.Players.GetValueOrDefault(conn.Guild.Id).Playlist

        member this.GetCurrentSong (conn: LavalinkGuildConnection) =
            let queue = this.Players.GetValueOrDefault(conn.Guild.Id).Playlist
            if queue.Count <> 0 then
                Some (queue.Peek())
                else None
            
        member this.StringSong (conn: LavalinkGuildConnection, song: Song) =
            let current =
                if conn.CurrentState.CurrentTrack = song.Track
                    then this.TrackProgress(conn) else ""
            $"[{song.Track.Title}]({song.Track.Uri}) [{song.Message.Author.Mention}]{current}"
        
        member this.StringChunk(conn: LavalinkGuildConnection, songs: Song[]) =
            let strings = [for idx, song in songs |> Seq.zip [1 .. songs.Length]
                -> $"{idx}: {this.StringSong(conn, song)}"]
            String.Join("\n", strings)

        member this.StringQueue (conn: LavalinkGuildConnection) =
            let queue = this.Players.GetValueOrDefault(conn.Guild.Id).Playlist

            // queue |> Seq.chunkBySize 10 |> Seq.map (fun s -> this.StringChunk(conn, s))
            let embed = DiscordEmbedBuilder()
            embed.Color <- DiscordColor.Purple
            if queue.Count = 0 then
                embed.Description <- "Nothing queued."
            else
                embed.Title <- "Current Queue"
                let strings = [for idx, song in queue |> Seq.zip [1 .. Seq.length queue]
                    -> $"{idx}: {this.StringSong(conn, song)}"]
                            |> fun s -> String.Join("\n", s)
                embed.Description <- strings
            embed

        member this.StringTime (time: TimeSpan) =
            let hours = time.TotalHours |> int
            let minutes = time.Minutes
            let seconds = time.Seconds

            if hours > 0 then
                $"{hours}:%02i{minutes}:%02i{seconds}"
            else
                $"%02i{minutes}:%02i{seconds}"

        member this.TrackProgress (conn: LavalinkGuildConnection) =
            let position = conn.CurrentState.PlaybackPosition
            let duration = conn.CurrentState.CurrentTrack.Length

            $" [{this.StringTime position} / {this.StringTime duration}]"
    
        member this.AdvanceQueue (conn: LavalinkGuildConnection) =
            let queue = this.Players.GetValueOrDefault(conn.Guild.Id).Playlist
            if queue.Count <> 0 then
                queue.Dequeue() |> ignore
        
        member this.ClearQueue (conn: LavalinkGuildConnection) =
            this.Players.GetValueOrDefault(conn.Guild.Id).Playlist.Clear()
        
        member this.RemoveAt (conn: LavalinkGuildConnection, loc: int) =
            let queue = this.Players.GetValueOrDefault(conn.Guild.Id).Playlist
            if loc - 1 <= queue.Count && loc > 0 then
                let newQueue = queue |> Seq.removeAt (loc - 1) |> Queue<Song>
                this.Players.GetValueOrDefault(conn.Guild.Id).Playlist <- newQueue
                true
            else false
        
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