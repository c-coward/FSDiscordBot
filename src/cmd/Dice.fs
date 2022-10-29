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
        | Failure _ -> ctx.RespondAsync("Something went wrong :P") |> ignore
        | Success (nums, _) ->
            let result = Seq.sum nums
            let combined = String.Join(" + ", Seq.map string nums)
            let embed = DiscordEmbedBuilder()
            embed.Color <- DiscordColor.Purple
            embed.Description <- $"( {combined} ) -> {result}"
            let! _ = ctx.RespondAsync(embed)
            ()
    }