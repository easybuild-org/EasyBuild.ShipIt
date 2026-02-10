module EasyBuild.Commands.Release

open Spectre.Console.Cli
open SimpleExec
open EasyBuild.Workspace
open System
open System.IO
open BlackFox.CommandLine
open EasyBuild.Tools.DotNet
open EasyBuild.Tools.Git
open EasyBuild.Commands.Test

type ReleaseSettings() =
    inherit CommandSettings()

type ReleaseCommand() =
    inherit Command<ReleaseSettings>()
    interface ICommandLimiter<ReleaseSettings>

    override __.Execute(context, _settings, ct) =
        TestCommand().Execute(context, TestSettings(), ct) |> ignore

        // Clean up the src/bin folder
        if Directory.Exists VirtualWorkspace.src.bin.``.`` then
            Directory.Delete(VirtualWorkspace.src.bin.``.``, true)

        let nupkgPath = DotNet.pack Workspace.src.``.``

        DotNet.nugetPush nupkgPath

        0
