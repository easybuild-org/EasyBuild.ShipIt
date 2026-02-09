[<RequireQualifiedAccess>]
module Environment

open System

let tryGet (name: string) =
    let var = Environment.GetEnvironmentVariable(name)

    if String.IsNullOrEmpty var then
        None
    else
        Some var

let isCI () = tryGet "CI" |> Option.isSome
