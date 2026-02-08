[<RequireQualifiedAccess>]
module Git

open System
open SimpleExec
open BlackFox.CommandLine

let getHeadBranchName () =
    let struct (standardOutput, _) =
        Command.ReadAsync(
            "git",
            CmdLine.empty
            |> CmdLine.appendRaw "rev-parse"
            |> CmdLine.appendPrefix "--abbrev-ref" "HEAD"
            |> CmdLine.toString
        )
        |> Async.AwaitTask
        |> Async.RunSynchronously

    standardOutput.Trim()

let getTopLevelDirectory () =
    let struct (standardOutput, _) =
        Command.ReadAsync(
            "git",
            CmdLine.empty
            |> CmdLine.appendRaw "rev-parse"
            |> CmdLine.appendRaw "--show-toplevel"
            |> CmdLine.toString
        )
        |> Async.AwaitTask
        |> Async.RunSynchronously

    standardOutput.Trim()

let isDirty () =
    let struct (standardOutput, _) =
        Command.ReadAsync(
            "git",
            CmdLine.empty
            |> CmdLine.appendRaw "status"
            |> CmdLine.appendRaw "--porcelain"
            |> CmdLine.toString
        )
        |> Async.AwaitTask
        |> Async.RunSynchronously

    standardOutput.Trim().Length > 0

type Commit =
    {
        Hash: string
        AbbrevHash: string
        Author: string
        ShortMessage: string
        RawBody: string
        Files: string list
    }

[<RequireQualifiedAccess>]
type GetCommitsFilter =
    | All
    | From of string

let readCommit (sha1: string) =
    let delimiter = "----PARSER_DELIMITER----"

    let gitFormat =
        [
            "%H" // commit hash
            "%h" // abbreviated commit hash
            "%an" // author name
            "%s" // subject
            "%B" // body / long message
            "END_METADATA" // We need an extra item otherwise, the body will have an additional new line at the end
        ]
        |> String.concat delimiter

    let args =
        CmdLine.empty
        |> CmdLine.appendRaw "--no-pager"
        |> CmdLine.appendRaw "show"
        |> CmdLine.appendRaw $"--format={gitFormat}"
        |> CmdLine.appendRaw "--name-only" // display the files changed in the commit
        |> CmdLine.appendRaw sha1
        |> CmdLine.toString

    let struct (commitStdout, _) =
        Command.ReadAsync("git", args) |> Async.AwaitTask |> Async.RunSynchronously

    let commitStdout = commitStdout.Split("END_METADATA", StringSplitOptions.None)

    let failToReadCommit context =
        failwith
            $"""Failed to read commit {sha1}, unexpected output from git show

Output was:
{commitStdout}

Context:
{context}
"""

    if commitStdout.Length <> 2 then
        failToReadCommit "Unexpected number of sections in git show output"

    let metadatas = commitStdout[0].Split(delimiter)

    let files =
        if commitStdout[1].Trim().Length = 0 then
            []
        else
            commitStdout[1].Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList

    if metadatas.Length <> 6 then
        failToReadCommit "Unexpected number of metadata fields in git show output"

    {
        Hash = metadatas[0]
        AbbrevHash = metadatas[1]
        Author = metadatas[2]
        ShortMessage = metadatas[3]
        RawBody = metadatas[4]
        Files = files
    }

let getCommits (filter: GetCommitsFilter) =
    let headRef =
        // When running on Github Actions, if the event is "pull_request"
        // we are getting an additional commit in the history of type "Merge"
        // This is because Github creates a merge commit between the base branch and the PR branch to run the workflow on it
        //
        // Example:
        // ecea0cc Merge 11e06e4e7c1e9087f8f3fa0f897c32c830ddb6b4 into 0fc1d5229402c8c1f64213c5e902e01036c2e10a
        // 11e06e4 ci: try to debug why there is a Merge commit
        // 68cacfa chore: try to set an initial commit because Git history seems broken on Github
        //
        // So we need to detect this case and use "HEAD~1" as the reference to get the "real" last commit instead of the merge commit.
        //
        // Otherwise, we default to HEAD.
        //
        // This logic could need to be updated in the future to be more strict based on the event or
        // perhaps to support other CI providers when adding support for them in the PullRequest orchestrator.
        match Environment.tryGet "GITHUB_EVENT_NAME" with
        | Some eventName ->
            printfn "Detected Github Actions event: %s" eventName

            match eventName with
            | "pull_request" -> "HEAD~1"
            | _ -> "HEAD"
        | None -> "HEAD"

    let commitFilter =
        match filter with
        | GetCommitsFilter.All -> headRef
        | GetCommitsFilter.From sha1 -> $"%s{sha1}..%s{headRef}"

    let struct (shaStdout, _) =
        Command.ReadAsync(
            "git",
            CmdLine.empty
            |> CmdLine.appendRaw "rev-list"
            |> CmdLine.appendRaw commitFilter
            |> CmdLine.toString
        )
        |> Async.AwaitTask
        |> Async.RunSynchronously

    shaStdout.Split('\n')
    |> Array.filter (fun x -> x.Length > 0)
    |> Array.map readCommit
    |> Array.toList

type Remote =
    {
        Hostname: string
        Owner: string
        Repository: string
    }

let private stripSuffix (suffix: string) (str: string) =
    if str.EndsWith(suffix) then
        str.Substring(0, str.Length - suffix.Length)
    else
        str

let private stripPrefix (prefix: string) (str: string) =
    if str.StartsWith(prefix) then
        str.Substring(prefix.Length)
    else
        str

// Url needs to be in the format:
// https://hostname/owner/repo.git
let tryGetRemoteFromUrl (url: string) =
    let normalizedUrl = url |> stripSuffix ".git"

    match Uri.TryCreate(normalizedUrl, UriKind.Absolute) with
    | true, uri ->
        let segments =
            uri.Segments
            |> Seq.map _.Trim('/')
            |> Seq.filter (String.IsNullOrEmpty >> not)
            |> Seq.toList

        if segments.Length < 2 then
            None
        else
            let owner = segments.[segments.Length - 2]
            let repo = segments.[segments.Length - 1]

            Some
                {
                    Hostname = uri.Host
                    Owner = owner.Trim('/')
                    Repository = repo.Trim('/')
                }
    | false, _ -> None

let tryGetRemoteFromSSH (url: string) =
    // Naive way to check the format
    if url.Contains("@") && url.Contains(":") && url.Contains("/") then
        let segments =
            // Remove the .git extension and split the url
            url |> stripPrefix "git@" |> stripSuffix ".git" |> _.Split(':') |> Seq.toList

        match segments with
        | hostname :: owner_repo :: _ ->
            let segments = owner_repo.Split('/') |> Array.rev |> Array.toList

            match segments with
            | repo :: owner :: _ ->
                Some
                    {
                        Hostname = hostname
                        Owner = owner
                        Repository = repo
                    }
            | _ -> None
        | _ -> None
    else
        None

let tryFindRemote () =
    let struct (remoteStdout, _) =
        try
            Command.ReadAsync(
                "git",
                CmdLine.empty
                |> CmdLine.appendRaw "config"
                |> CmdLine.appendPrefix "--get" "remote.origin.url"
                |> CmdLine.toString
            )
            |> Async.AwaitTask
            |> Async.RunSynchronously
        with
        // If we don't have a remote configured the Git command returns exitCode 1
        // making the invocation throw an exception
        | _ ->
            struct ("", "")

    let remoteUrl = remoteStdout.Trim()

    match tryGetRemoteFromUrl remoteUrl with
    | Some remote -> Ok remote
    | None ->
        match tryGetRemoteFromSSH remoteUrl with
        | Some remote -> Ok remote
        | None ->
            Error
                """Could not resolve the remote repository.

Automatic detection expects URL returned by `git config --get remote.origin.url` to be of the form 'https://hostname/owner/repo.git' or 'git@hostname:owner/repo.git'.

You can use the --github-repo option to specify the repository manually."""

let setUpstreamAndForcePush (remoteName: string) (branchName: string) =
    Command.Run(
        "git",
        CmdLine.empty
        |> CmdLine.appendRaw "push"
        |> CmdLine.appendRaw "--set-upstream"
        |> CmdLine.appendRaw remoteName
        |> CmdLine.appendRaw branchName
        |> CmdLine.appendRaw "--force"
        |> CmdLine.toString
    )

let push () = Command.Run("git", "push")

let switch (branchName: string) =
    Command.Run(
        "git",
        CmdLine.empty
        |> CmdLine.appendRaw "switch"
        |> CmdLine.appendRaw branchName
        |> CmdLine.toString
    )

let switchAndMove (branchName: string) =
    Command.Run(
        "git",
        CmdLine.empty
        |> CmdLine.appendRaw "switch"
        |> CmdLine.appendPrefix "-C" branchName
        |> CmdLine.toString
    )

let deleteBranch (branchName: string) =
    Command.Run(
        "git",
        CmdLine.empty
        |> CmdLine.appendRaw "branch"
        |> CmdLine.appendPrefix "-D" branchName
        |> CmdLine.toString
    )

let commitAll (message: string) =
    Command.Run(
        "git",
        CmdLine.empty
        |> CmdLine.appendRaw "commit"
        |> CmdLine.appendRaw "-a"
        |> CmdLine.appendPrefix "-m" message
        |> CmdLine.toString
    )

let tryGetConfig (key: string) =
    try
        let struct (stdout, _) =
            Command.ReadAsync(
                "git",
                CmdLine.empty
                |> CmdLine.appendRaw "config"
                |> CmdLine.appendPrefix "--get" key
                |> CmdLine.toString
            )
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Some(stdout.Trim())
    with _ ->
        None

let setLocalConfig (key: string) (value: string) =
    Command.Run(
        "git",
        CmdLine.empty
        |> CmdLine.appendRaw "config"
        |> CmdLine.appendRaw "--local"
        |> CmdLine.appendPrefix key value
        |> CmdLine.toString
    )

let getRemoteName () =
    if Environment.isGithubActions () then
        // On Github Actions the remote is always "origin"
        Ok "origin"
    else
        try
            let struct (remoteStdout, _) =
                Command.ReadAsync(
                    "git",
                    CmdLine.empty
                    |> CmdLine.appendRaw "rev-parse"
                    |> CmdLine.appendRaw "--abbrev-ref"
                    |> CmdLine.appendRaw "--symbolic-full-name"
                    |> CmdLine.appendRaw "@{upstream}"
                    |> CmdLine.toString
                )
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let remoteRef = remoteStdout.Trim()

            remoteRef.Split('/') |> Array.head |> Ok
        with

        | _ ->
            Error
                """Could not determine the name of the remote for the current branch.

Please open an issue we the result of running `git rev-parse --abbrev-ref --symbolic-full-name @{upstream}`"""
