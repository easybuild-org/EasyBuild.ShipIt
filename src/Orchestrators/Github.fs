module EasyBuild.ShipIt.Orchestrators.Github

open EasyBuild.ShipIt.Types
open Thoth.Json.Core
open Thoth.Json.System.Text.Json
open FsToolkit.ErrorHandling

module OpenedReleasePR =

    let decoder: Decoder<Orchestrator.OpenedReleasePR> =
        Decode.object (fun get ->
            {
                Url = get.Required.Field "url" Decode.string
                HeadRefName = get.Required.Field "headRefName" Decode.string
                Number = get.Required.Field "number" Decode.int
            }
        )

type GitHubOrchestrator() =

    let ghCli = Gh.CLI()

    interface Orchestrator.IOrchestrator with

        member _.GetOpenedPullRequests(branchName: string) =
            // By default, GitHub CLI returns 30 items, which should be enough for our use case
            // It would fails if we have more than 30 open PRs with the pending label (unlikely)
            ghCli.pr.List(
                Orchestrator.Label.PENDING,
                Gh.PR.List.State.Open,
                [
                    Gh.PR.List.Fields.HeadRefName
                    Gh.PR.List.Fields.Url
                    Gh.PR.List.Fields.Number
                ]
            )
            |> Decode.unsafeFromString (Decode.list OpenedReleasePR.decoder)
            // Filter PRs opened for the current release branch
            // This allows working in parallel on multiple version of the project
            |> List.filter (fun pr -> pr.HeadRefName = branchName)

        member _.EnsureLabelsExist() =
            let references = [ Orchestrator.Label.PENDING ]

            let existingLabels =
                let decoder = Decode.list (Decode.field "name" Decode.string)

                ghCli.label.List(jsonFields = [ Gh.Label.List.Fields.Name ])
                |> Decode.unsafeFromString decoder

            references
            |> List.iter (fun labelRef ->
                if not (List.contains labelRef existingLabels) then
                    ghCli.label.Create(labelRef, color = "#EDEDED")
            )

        member _.CreateReleasePullRequest (head: string) (title: string) (body: string) =
            ghCli.pr.Create(
                head = head,
                title = title,
                body = body,
                labels = [ Orchestrator.Label.PENDING ]
            )

        member _.UpdateReleasePullRequest (prNumber: int) (title: string) (body: string) =
            ghCli.pr.Update(prNumber, title = title, body = body)

        member _.VerifyAndSetupRequirements(mode: ReleaseMode) : Result<unit, string> =
            let cliAvailable () =
                if not (ghCli.IsAvailable()) then
                    Error
                        "GitHub CLI is not available. Please install it from https://cli.github.com/ and ensure it's in your PATH."
                else
                    // We can do additional checks here if needed, for example checking if the user is authenticated with GitHub CLI
                    Ok()

            let verifyGithubActionsEvent () =
                match Environment.tryGet "GITHUB_GITHUB_EVENT_NAME" with
                // If we are in a GitHub Actions environment, we want to ensure that the event is "push"
                //
                // This is because if we are in another event like "pull_request"
                // there is a risk of not having the right permissions with the default tokens
                // but more importantly, we would have an additional "Merge" commit in the history
                //
                // Example:
                // ecea0cc Merge 11e06e4e7c1e9087f8f3fa0f897c32c830ddb6b4 into 0fc1d5229402c8c1f64213c5e902e01036c2e10a
                // 11e06e4 ci: try to debug why there is a Merge commit
                // 68cacfa chore: try to set an initial commit because Git history seems broken on Github
                //
                // For now, we are restricting the usage to "push" events, as it should
                // cover the need for when running PullRequest or Push mode from Github Actions
                | Some eventName when eventName = "push" -> Ok()
                | Some _ ->
                    Error
                        "GitHub CLI cannot be used in a GitHub Actions workflow triggered by an event different than 'push'."
                // We are not in a GitHub Actions environment, we can proceed with the normal checks
                | None -> Ok()

            result {
                do! cliAvailable ()
                do! verifyGithubActionsEvent ()

                match mode with
                | ReleaseMode.Local -> ()
                | ReleaseMode.PullRequest
                | ReleaseMode.Push ->
                    if Environment.isCI () then
                        Git.setLocalConfig "user.name" "🤖 easybuild-shipit"

                        Git.setLocalConfig
                            "user.email"
                            "github-actions[bot]@users.noreply.github.com"

                return ()
            }
