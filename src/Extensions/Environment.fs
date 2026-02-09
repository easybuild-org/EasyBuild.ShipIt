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

let isGithubActions () =
    tryGet "GITHUB_ACTIONS" |> Option.isSome
