module EasyBuild.ShipIt.Commands.InitGithub

open Spectre.Console.Cli
open Thoth.Json.Core
open Thoth.Json.Newtonsoft
open FsToolkit.ErrorHandling
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Types.Settings

[<Literal>]
let SQUASH_MERGE_COMMIT_TITLE = "PR_TITLE"

[<Literal>]
let ACTIONS_PULL_REQUEST_LABEL =
    "Allow GitHub Actions to create and approve pull requests"

type RepositorySettings =
    {
        OwnerType: string
        AllowMergeCommit: bool
        AllowSquashMerge: bool
        AllowRebaseMerge: bool
        SquashMergeCommitTitle: string
    }

    member this.IsOwnedByOrganization = this.OwnerType = "Organization"

    static member Decoder: Decoder<RepositorySettings> =
        Decode.object (fun get ->
            {
                OwnerType =
                    get.Required.At
                        [
                            "owner"
                            "type"
                        ]
                        Decode.string
                AllowMergeCommit = get.Required.Field "allow_merge_commit" Decode.bool
                AllowSquashMerge = get.Required.Field "allow_squash_merge" Decode.bool
                AllowRebaseMerge = get.Required.Field "allow_rebase_merge" Decode.bool
                SquashMergeCommitTitle =
                    get.Required.Field "squash_merge_commit_title" Decode.string
            }
        )

type WorkflowPermissions =
    {
        DefaultWorkflowPermissions: string
        CanApprovePullRequestReviews: bool
    }

    static member Decoder: Decoder<WorkflowPermissions> =
        Decode.object (fun get ->
            {
                DefaultWorkflowPermissions =
                    get.Required.Field "default_workflow_permissions" Decode.string
                CanApprovePullRequestReviews =
                    get.Required.Field "can_approve_pull_request_reviews" Decode.bool
            }
        )

type SettingCheck =
    {
        Label: string
        Current: string
        Desired: string
        Field: Gh.Api.Field
    }

    member this.IsUpToDate = this.Current = this.Desired

module SettingCheck =

    let private toggle (value: bool) =
        if value then
            "enabled"
        else
            "disabled"

    let mergeStrategy (current: RepositorySettings) =
        [
            {
                Label = "Allow merge commits"
                Current = toggle current.AllowMergeCommit
                Desired = toggle false
                Field = Gh.Api.Field.Typed("allow_merge_commit", "false")
            }
            {
                Label = "Allow squash merging"
                Current = toggle current.AllowSquashMerge
                Desired = toggle true
                Field = Gh.Api.Field.Typed("allow_squash_merge", "true")
            }
            {
                Label = "Allow rebase merging"
                Current = toggle current.AllowRebaseMerge
                Desired = toggle true
                Field = Gh.Api.Field.Typed("allow_rebase_merge", "true")
            }
            {
                Label = "Default squash merge commit title"
                Current = current.SquashMergeCommitTitle
                Desired = SQUASH_MERGE_COMMIT_TITLE
                Field = Gh.Api.Field.Raw("squash_merge_commit_title", SQUASH_MERGE_COMMIT_TITLE)
            }
        ]

    let actionsPullRequestPermission (current: WorkflowPermissions) =
        [
            {
                Label = ACTIONS_PULL_REQUEST_LABEL
                Current = toggle current.CanApprovePullRequestReviews
                Desired = toggle true
                // GitHub replaces the whole workflow permissions object, so the default
                // permissions have to be sent back untouched.
                Field =
                    Gh.Api.Field.Raw(
                        "default_workflow_permissions",
                        current.DefaultWorkflowPermissions
                    )
            }
        ]

type InitGithubCommand() =
    inherit Command<InitGithubSettings>()

    let ghCli = Gh.CLI()

    let readWorkflowPermissions (endpoint: string) =
        ghCli.api.Request(Gh.Api.Method.Get, endpoint)
        |> Result.bind (Decode.fromString WorkflowPermissions.Decoder)

    let report (title: string) (checks: SettingCheck list) =
        Log.newLine ()
        Log.log title

        for check in checks do
            if check.IsUpToDate then
                Log.log $"  {check.Label}: {check.Current}"
            else
                Log.info $"  {check.Label}: {check.Current} -> {check.Desired}"

    let enablePullRequestPermission (endpoint: string) (checks: SettingCheck list) =
        if checks |> List.forall _.IsUpToDate then
            Ok()
        else
            ghCli.api.Request(
                Gh.Api.Method.Put,
                endpoint,
                (checks |> List.map _.Field)
                @ [ Gh.Api.Field.Typed("can_approve_pull_request_reviews", "true") ]
            )
            |> Result.ignore

    interface ICommandLimiter<CommandSettings>

    override _.Execute(_context, settings, _ct) =
        let run =
            result {
                do!
                    if ghCli.IsAvailable() then
                        Ok()
                    else
                        Error
                            "GitHub CLI is not available. Please install it from https://cli.github.com/ and ensure it's in your PATH."

                let! gitRemote = Git.tryFindRemote ()

                let remote: RemoteConfig =
                    {
                        Hostname = gitRemote.Hostname
                        Owner = gitRemote.Owner
                        Repository = gitRemote.Repository
                    }

                do!
                    match remote.Host with
                    | RemoteHost.GitHub -> Ok()
                    | RemoteHost.Unknown ->
                        Error
                            $"'%s{remote.Hostname}' is not a GitHub remote. 'init github' can only configure repositories hosted on github.com."

                let repositoryEndpoint = $"repos/%s{remote.Owner}/%s{remote.Repository}"

                let repositoryWorkflowEndpoint =
                    $"%s{repositoryEndpoint}/actions/permissions/workflow"

                let organizationWorkflowEndpoint =
                    $"orgs/%s{remote.Owner}/actions/permissions/workflow"

                let! repository =
                    ghCli.api.Request(Gh.Api.Method.Get, repositoryEndpoint)
                    |> Result.bind (Decode.fromString RepositorySettings.Decoder)

                let! repositoryWorkflow = readWorkflowPermissions repositoryWorkflowEndpoint

                do!
                    if settings.Org && not repository.IsOwnedByOrganization then
                        Error
                            $"'%s{remote.Owner}' is a user account, --org only applies to repositories owned by an organization."
                    else
                        Ok()

                let! organizationWorkflow =
                    if settings.Org then
                        readWorkflowPermissions organizationWorkflowEndpoint
                        |> Result.map Some
                        |> Result.mapError (fun error ->
                            $"""Failed to read the GitHub Actions permissions of organization '%s{remote.Owner}'.

%s{error}"""
                        )
                    else
                        Ok None

                let mergeStrategyChecks = SettingCheck.mergeStrategy repository

                let repositoryActionsChecks =
                    SettingCheck.actionsPullRequestPermission repositoryWorkflow

                let organizationActionsChecks =
                    organizationWorkflow
                    |> Option.map SettingCheck.actionsPullRequestPermission
                    |> Option.defaultValue []

                Log.log $"Repository: %s{remote.Owner}/%s{remote.Repository}"

                report "Pull request merge strategy" mergeStrategyChecks

                if settings.Org then
                    report
                        $"GitHub Actions permissions (organization '%s{remote.Owner}')"
                        organizationActionsChecks

                report "GitHub Actions permissions (repository)" repositoryActionsChecks

                Log.newLine ()

                let allChecks =
                    mergeStrategyChecks @ organizationActionsChecks @ repositoryActionsChecks

                if allChecks |> List.forall _.IsUpToDate then
                    Log.success "Nothing to do, everything is already configured."
                    return 0
                elif settings.DryRun then
                    Log.info "Dry run: no changes have been applied."
                    return 0
                else

                    // The organization policy is applied first, because a restrictive
                    // organization policy prevents the repository from enabling the same setting.
                    do!
                        enablePullRequestPermission
                            organizationWorkflowEndpoint
                            organizationActionsChecks

                    do!
                        enablePullRequestPermission
                            repositoryWorkflowEndpoint
                            repositoryActionsChecks

                    let mergeStrategyChanges =
                        mergeStrategyChecks |> List.filter (_.IsUpToDate >> not)

                    if not (List.isEmpty mergeStrategyChanges) then
                        do!
                            ghCli.api.Request(
                                Gh.Api.Method.Patch,
                                repositoryEndpoint,
                                mergeStrategyChanges |> List.map _.Field
                            )
                            |> Result.ignore

                    let! appliedWorkflow = readWorkflowPermissions repositoryWorkflowEndpoint

                    if appliedWorkflow.CanApprovePullRequestReviews then
                        Log.success "GitHub settings updated."
                        return 0
                    else
                        let guidance =
                            if repository.IsOwnedByOrganization && not settings.Org then
                                $"""An organization owner can lift it by running:

    dotnet shipit init github --org

or by enabling '%s{ACTIONS_PULL_REQUEST_LABEL}' at:

    https://github.com/organizations/%s{remote.Owner}/settings/actions"""
                            else
                                $"""Enable '%s{ACTIONS_PULL_REQUEST_LABEL}' manually at:

    %s{remote.BaseUrl}/settings/actions"""

                        Log.error
                            $"""'%s{ACTIONS_PULL_REQUEST_LABEL}' was applied but GitHub still reports it as disabled.

This means it is blocked by a policy above the repository.

%s{guidance}"""

                        return 1
            }

        match run with
        | Ok exitCode -> exitCode
        | Error error ->
            Log.error error
            1
