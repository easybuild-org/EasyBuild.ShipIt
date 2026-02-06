module EasyBuild.ShipIt.Generate.Verify

open EasyBuild.ShipIt.Types

let branch (settings: Settings.SharedSettings) =
    let currentBranchName = Git.getHeadBranchName ()

    if Array.contains currentBranchName settings.AllowBranch then
        Ok()
    else
        let allowedBranch =
            settings.AllowBranch |> Array.map (String.prepend "- ") |> String.concat "\n"

        Error
            $"""Branch '%s{currentBranchName}' is not allowed to generate the changelog.

Allowed branches are:
%s{allowedBranch}

You can use the --allow-branch option to allow other branches."""

let dirty () =
    if Git.isDirty () then
        Error
            "Repository is dirty. Please commit or stash your changes before generating the changelog."
    else
        Ok()

let resolveRemoteConfig (settings: Settings.SharedSettings) =
    match settings.RemoteHostname, settings.RemoteOwner, settings.RemoteRepo with
    | Some hostname, Some owner, Some repo ->
        {
            Hostname = hostname
            Owner = owner
            Repository = repo
        }
        |> Ok

    | None, None, None ->
        match Git.tryFindRemote () with
        | Ok remote ->
            {
                Hostname = remote.Hostname
                Owner = remote.Owner
                Repository = remote.Repository
            }
            |> Ok
        | Error error -> Error error

    | _ ->
        Error
            """When using --remote-hostname, --remote-owner and --remote-repo they must be all provided."""

let releaseMode (mode: string option) =
    match mode with
    | None -> Ok ReleaseMode.PullRequest
    | Some "local" -> Ok ReleaseMode.Local
    | Some "pull-request" -> Ok ReleaseMode.PullRequest
    | Some "push" -> Ok ReleaseMode.Push
    | _ ->
        Error
            "Invalid mode passed to `--mode`. Allowed values are 'local', 'pull-request' and 'push'."
