open System.IO

let args = System.Environment.GetCommandLineArgs()

let version =
    let rec findVersionArg (args: string list) =
        match args with
        | [] -> None
        | "--version" :: version :: _ -> Some version
        | _ :: tail -> findVersionArg tail

    findVersionArg (List.ofArray args)

match version with
| None ->
    failwith "Could not find --version argument. Please provide the new version as an argument, e.g. --version 1.2.3"
    1
| Some version ->
    let cwd = Directory.GetCurrentDirectory()

    let versionFile = Path.Combine(cwd, "files", "some_file.version")

    File.WriteAllText(versionFile, version)
    0
