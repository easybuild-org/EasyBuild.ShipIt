module EasyBuild.ShipIt.Commands.Github

open Spectre.Console.Cli
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Types.Settings
open EasyBuild.ShipIt.Orchestrators.Github

type GithubCommand() =
    inherit Command<GithubCommandSettings>()

    interface ICommandLimiter<GithubCommandSettings>

    interface Orchestrator.IResolver with
        member __.GetOrchestrator(_remoteConfig: RemoteConfig) =
            GitHubOrchestrator() :> Orchestrator.IOrchestrator |> Ok

    override this.Execute(_context, settings, _ct) = Default.execute settings this
