module EasyBuild.Commands.Test

open Spectre.Console.Cli
open SimpleExec
open EasyBuild.Workspace
open BlackFox.CommandLine

type TestSettings() =
    inherit CommandSettings()

    [<CommandOption("-w|--watch")>]
    member val IsWatch = false with get, set

type TestCommand() =
    inherit Command<TestSettings>()
    interface ICommandLimiter<TestSettings>

    override __.Execute(_, settings, _) =
        if settings.IsWatch then
            Command.Run(
                "dotnet",
                CmdLine.empty
                |> CmdLine.appendRaw "watch"
                |> CmdLine.appendRaw "test"
                // Can't use the type provider here because of:
                // The type provider 'EasyBuild.FileSystemProvider.FileSystemProviders+RelativeFileSystemProvider' reported an error: Capacity exceeds maximum capacity. (Parameter 'capacity')
                // ¯\_(ツ)_/¯
                |> CmdLine.appendPrefix "--project" "tests/EasyBuild.ShipIt.Tests.fsproj"
                |> CmdLine.toString
            )
        else
            Command.Run("dotnet", "test")

        0
