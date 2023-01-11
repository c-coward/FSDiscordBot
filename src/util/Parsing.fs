namespace MusicBot.Util

open System
open FParsec

module Parsing =

    let private ptoken p = spaces >>. p .>> spaces
    let private str_token s = ptoken (pstring s)

    let dice: Parser<list<int>, unit> =
        let rand = Random()
        pipe3 (ptoken pint64 |>> int)
            (str_token "d" <|> str_token "D")
            (ptoken pint64 |>> int)
            (fun n _ d -> [for _ = 1 to n do yield rand.Next()%d + 1])
    
    let modif: Parser<int, unit> =
        str_token "+" >>. ptoken pint64 |>> int <|>% 0

    let roll = dice .>>. modif