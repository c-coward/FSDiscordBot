namespace MusicBot.Util

open System

module Parsing =
    type ParseResult<'A> =
        | Success of 'A
        | Failure of string
    
    type Parser<'S, 'T> = Parser of ('S -> ParseResult<'T * 'S>)

    let run (Parser f) = fun s -> f s

    let bind f p = Parser (fun s ->
        match run p s with
        | Success (r, s') -> run (f r) s'
        | Failure err -> Failure err)
    let (>>=) p f = bind f p
    let ret x = Parser (fun s -> Success (x, s))
    let empty = Parser (fun s -> Failure s)

    let map f = bind (f >> ret)
    let (<!>) = map
    let (|>>) p f = map f p
    
    let apply pf px =
        pf >>= (fun f ->
        px >>= (fun x -> ret (f x)))
    let (<*>) = apply

    let andThen pa pb = 
        pa >>= (fun r1 ->
        pb >>= (fun r2 -> ret (r1, r2)))
    let (.>>.) = andThen

    let orElse pa pb = Parser (fun s ->
        match run pa s with
        | Failure _ -> run pb s
        | success -> success)
    let (<|>) = orElse

    let choice plist = List.reduce (<|>) plist

    let (.>>) pa pb = pa .>>. pb |>> fst
    let (>>.) pa pb = pa .>>. pb |>> snd
    let between pa pb pc = pa >>. pb .>> pc
    let outsides pa pb pc = pa .>> pb .>>. pc

    let lift f p = (<*>) (f <!> p)

    let rec sequence pList =
        let cons h t = h :: t
        match pList with
        | [] -> ret []
        | (h::t) -> cons <!> h <*> sequence t
    
    let rec zeroPlus p s =
        match (run p s) with
        | Failure _ -> ([], s)
        | Success (first, s') ->
            let rest, s'' = zeroPlus p s'
            (first::rest, s'')
    let many p = Parser (fun s -> Success (zeroPlus p s))
    let some p =
        p >>= (fun first ->
        many p >>= (fun rest -> ret (first::rest)))

    // String parsing
    let sat b =
        Parser (function
        | "" -> Failure "empty string"
        | s  -> if b s[0] then Success (s[0], s[1..]) else Failure "")

    let pchar c = sat ((=) c)

    let charListToStr s = String(List.toArray s)

    let pstring (s:string) =
        List.ofSeq s
        |> List.map pchar |> sequence
        |>> charListToStr
    
    let spaces = many (sat Char.IsWhiteSpace) |>> ignore

    let digit = sat (Char.IsDigit)
    let num = many digit |>> (charListToStr >> int)

    let token p = between spaces p spaces
    let stoken = token << pstring

    let modifier = stoken "+" >>. num
        

    // Dice parsing
    let roll =
        let rand = Random()
        outsides (token num)
            (token (choice [pstring "d"; pstring "D"]))
            (token num) >>=
        (fun (count, sides) ->
        choice [modifier; ret 0] >>=
        (fun (modif) ->
            ret ([for _ = 1 to count do
                    yield rand.Next()%sides + 1], modif)))