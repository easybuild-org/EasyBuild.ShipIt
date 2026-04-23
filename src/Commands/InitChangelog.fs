module EasyBuild.ShipIt.Commands.InitChangelog

open System
open System.IO
open Spectre.Console.Cli
open EasyBuild.ShipIt.Types.Settings
open EasyBuild.ShipIt.Generate.Changelog

type InitChangelogCommand() =
    inherit Command<InitChangelogSettings>()

    interface ICommandLimiter<CommandSettings>

    override _.Execute(_context, settings, _ct) =
        let path =
            match settings.Path with
            | Some path -> path
            | None -> "CHANGELOG.md"

        let fullPath = Path.GetFullPath(path, settings.Cwd)

        if File.Exists(fullPath) then
            Log.error $"Changelog file already exists at '{fullPath}'."
            1
        else
            let directory = Path.GetDirectoryName(fullPath)

            if not (String.IsNullOrWhiteSpace(directory)) then
                Directory.CreateDirectory(directory) |> ignore

            File.WriteAllText(fullPath, EMPTY_CHANGELOG)
            Log.success $"Changelog created at '{fullPath}'."
            0
