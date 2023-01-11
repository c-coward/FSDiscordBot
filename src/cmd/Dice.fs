namespace MusicBot.Cmd

open System
open System.Threading.Tasks
open DSharpPlus.CommandsNext
open DSharpPlus.CommandsNext.Attributes
open DSharpPlus.Entities

open MusicBot.Util.Parsing

type Dice () =
    inherit BaseCommandModule ()

    [<Command "roll">]
    [<Aliases ("r")>]
    [<Description "Roll the dice">]
    member this.Roll(ctx: CommandContext, [<RemainingText>] txt) : Task = task {
        match run roll txt with
        | Success ((nums, m), _) ->
            let result = Seq.sum nums + m
            let combined = String.Join(" + ", Seq.map string nums)
            let embed = DiscordEmbedBuilder()
            embed.Color <- DiscordColor.Purple
            let modString = if m = 0 then "" else $" + {m}"
            embed.Description <- $"( {combined} ){modString} -> {result}"
            ctx.RespondAsync(embed) |> ignore
        | _ -> ()
    }