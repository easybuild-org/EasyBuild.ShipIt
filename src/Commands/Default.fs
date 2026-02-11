module EasyBuild.ShipIt.Commands.Default

open Spectre.Console.Cli
open FsToolkit.ErrorHandling
open EasyBuild.ShipIt
open EasyBuild.ShipIt.Generate
open EasyBuild.ShipIt.Generate.Types
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Types.Settings
open EasyBuild.ShipIt.Commands.Version
open EasyBuild.ShipIt.Orchestrators.Github

let private releaseUsingPush
    (orchestratorResolver: Orchestrator.IResolver)
    (remoteConfig: RemoteConfig)
    (releaseContexts: ReleaseContext list)
    (settings: SharedSettings)
    =

    // Re-use the information from the PullRequestContext
    let prContext =
        PullRequest.PullRequestContext(releaseContexts, settings.GitRepositoryRoot, remoteConfig)

    if prContext.ShouldCreate() |> not then
        Ok()
    else
        result {
            let! orchestrator = orchestratorResolver.GetOrchestrator remoteConfig

            do! orchestrator.VerifyAndSetupRequirements ReleaseMode.Push

            let! prTitle = prContext.Title

            Git.commitAll prTitle
            Git.push ()
            return ()
        }

let execute (settings: SharedSettings) (orchestratorResolver: Orchestrator.IResolver) =
    let res =
        result {
            // Use the default config coming from EasyBuild.CommitParser
            let config = ConfigLoader.Config.Default
            // Apply automatic resolution of remote config if needed
            let! remoteConfig = Verify.resolveRemoteConfig settings
            let! releaseMode = Verify.releaseMode settings.Mode

            // We need to verify that the repository is clean before doing anything
            do! Verify.dirty ()
            do! Verify.branch settings

            let changelogFiles = Changelog.find settings

            // Apply the operations for each changelog file found
            let! changelogs =
                changelogFiles
                |> Seq.map Changelog.tryLoad
                |> Seq.toList
                |> List.sequenceResultM

            let releaseContexts =
                changelogs
                // Order by priority if available (lower number means higher priority)
                // Changelog without priority go to the end
                |> List.sortWith (fun a b ->
                    match a.Metadata.Priority, b.Metadata.Priority with
                    | Some a', Some b' -> compare a' b'
                    | Some _, None -> -1
                    | None, Some _ -> 1
                    | None, None -> 0
                )
                |> List.map (fun changelogInfo ->
                    let commits = ReleaseContext.getCommits changelogInfo

                    ReleaseContext.compute settings changelogInfo commits config.CommitParserConfig
                )

            let! _ = releaseContexts |> List.traverseResultM (ReleaseContext.apply remoteConfig)

            // Now we can generate the Pull Request with all the changelog updates
            if releaseContexts.Length = 0 then
                Log.success "No changelog was bumped, nothing to ship."
            else
                match releaseMode with
                | ReleaseMode.PullRequest ->
                    do!
                        PullRequest.createOrUpdatePullRequest
                            orchestratorResolver
                            remoteConfig
                            releaseContexts
                            settings

                | ReleaseMode.Local -> ()
                | ReleaseMode.Push ->
                    do! releaseUsingPush orchestratorResolver remoteConfig releaseContexts settings

                Log.success "Done 🚀"

            return 0
        }

    match res with
    | Ok exitCode -> exitCode
    | Error error ->
        Log.error error
        1

/// <summary>
/// Default command which tries to auto detect the remote using the git URL
/// </summary>
type DefaultCommand() =
    inherit Command<DefaultCommandSettings>()

    interface ICommandLimiter<DefaultCommandSettings>

    interface Orchestrator.IResolver with
        member __.GetOrchestrator(remoteConfig: RemoteConfig) =
            match remoteConfig.Host with
            | RemoteHost.GitHub -> GitHubOrchestrator() :> Orchestrator.IOrchestrator |> Ok
            | RemoteHost.Unknown ->
                Error
                    """The remote host is not supported for pull request operations.

Supported hosts are:

- GitHub

Please open an issue"""

    override this.Execute(context, settings, ct) =
        if settings.ShowVersion then
            VersionCommand().Execute(context, VersionSettings(), ct)
        else
            execute settings this
