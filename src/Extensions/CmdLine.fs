namespace BlackFox.CommandLine

module CmdLine =

    open BlackFox.CommandLine

    let appendPrefixSeqIfSome (prefix: string) (values: string list option) (cmdLine: CmdLine) =
        match values with
        | Some vals -> CmdLine.appendPrefixSeq prefix vals cmdLine
        | None -> cmdLine
