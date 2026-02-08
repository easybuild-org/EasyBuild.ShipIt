module EasyBuild.ShipIt.Generate.PullRequest

open System
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Generate.Types
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling

type PullRequestContext
    (items: ReleaseContext list, gitRepositoryRoot: string, remoteConfig: RemoteConfig)
    =
    // Track only the items that require a version bump
    // This simplifies the logic to determine if a PR should be created,
    // and is also used to generate additional information like the branch name, changelog summary, etc.
    let updatedItems = ResizeArray<BumpInfo>()

    do
        items
        |> List.iter (fun item ->
            match item with
            | BumpRequired info -> updatedItems.Add(info)
            | NoVersionBumpRequired _ -> ()
        )

    member _.ShouldCreate() = updatedItems.Count > 0

    member _.BranchName =
        // If we only have on bump, we can use the specific version in the branch name
        if updatedItems.Count = 1 then
            $"release/{updatedItems[0].NewVersion}"
        else
            // If we have multiple bumps, we try to make a deterministic branch name
            // We make a concatenation of all the changelog names and their new versions
            // and then we hash it to produce a short unique identifier

            let concatenatedInfo =
                updatedItems
                |> Seq.map (fun bumpInfo ->
                    let projectName = bumpInfo.Changelog.NameOrDirectoryPath gitRepositoryRoot

                    $"{projectName}:{bumpInfo.NewVersion}"
                )
                |> String.concat "|"

            let hash =
                use sha1 = Security.Cryptography.SHA1.Create()

                let hashBytes =
                    sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(concatenatedInfo))

                BitConverter
                    .ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant()
                    .Substring(0, 16)

            $"release/multiple-{hash}"

    member _.Title =
        if updatedItems.Count = 1 then
            let project = updatedItems[0]
            let name = project.Changelog.NameWithVersion project.NewVersion gitRepositoryRoot

            $"chore: release %s{name}"
        else
            "chore: release multiple projects"

    member _.PullRequestSummaryTable =
        let rows =
            items
            |> List.map (fun item ->
                match item with
                | NoVersionBumpRequired changelogInfo ->
                    let changelogName = changelogInfo.NameOrDirectoryPath gitRepositoryRoot

                    $"| %s{changelogName} | ✅ | |"
                | BumpRequired bumpInfo ->
                    let changelogName = bumpInfo.Changelog.NameOrDirectoryPath gitRepositoryRoot

                    $"| %s{changelogName} | 🚀 | %s{bumpInfo.NewVersion.ToString()} |"
            )

        let hasUnnamedProject =
            items
            |> List.exists (
                function
                | NoVersionBumpRequired info
                | BumpRequired {
                                   Changelog = info
                               } ->
                    info.NameOrDirectoryPath gitRepositoryRoot = Literals.UNNAMED_CHANGELOG
            )

        [
            "| Project | Status | New Version |"
            "| --- | :---: | :---: |"
            yield! rows
            ""
            "**Legend:**"
            "- ✅ No version bump required"
            "- 🚀 New version"
            if hasUnnamedProject then
                "> [!TIP]"
                "> 🤖 I was not able to find a meaningful name for all the projects"
                ">"
                "> You can help me by setting [`name`](https://github.com/easybuild-org/EasyBuild.ShipIt#name) in your `CHANGELOG.md` configuration"
        ]
        |> String.concat "\n"

    member _.ChangelogSummary =
        updatedItems
        |> Seq.map (fun bumpInfo ->
            // Increase header rank by one level to fit in the PR body
            // This is because the project name will be using a H2 header (##)
            // which is the same level used in the changelog for the version headers
            let increaseHeaderRank (text: string) =
                let regex = Regex(@"^(#+)( .+)$", RegexOptions.Multiline)

                regex.Replace(
                    text,
                    fun (m: Match) ->
                        let hashes = m.Groups.[1].Value
                        let rest = m.Groups.[2].Value
                        let newHashes = "#" + hashes
                        newHashes + rest
                )

            let newVersionContent =
                Changelog.generateNewVersionSection
                    remoteConfig
                    bumpInfo.Changelog.Metadata.LastCommitReleased
                    bumpInfo

            [
                $"## %s{bumpInfo.Changelog.NameOrDirectoryPath gitRepositoryRoot}"
                ""
                increaseHeaderRank newVersionContent
            ]
            |> String.concat "\n"
        )
        |> String.concat ""

let createOrUpdatePullRequest
    (orchestratorResolver: Orchestrator.IResolver)
    (remoteConfig: RemoteConfig)
    (releaseContexts: ReleaseContext list)
    (settings: Settings.SharedSettings)
    =
    result {
        let! orchestrator = orchestratorResolver.GetOrchestrator remoteConfig

        let prContext =
            PullRequestContext(releaseContexts, settings.GitRepositoryRoot, remoteConfig)

        if prContext.ShouldCreate() |> not then
            return ()
        else

            // Store the current branch to restore it later
            let currentBranch = Git.getHeadBranchName ()

            // Git operations:
            // 1. Create a new branch and move to it
            // 2. Commit all changes and push the branch
            // 3. Restore the previous branch and delete the temporary branch

            Git.switchAndMove prContext.BranchName

            if Environment.isGithubActions () then
                match Environment.tryGet "GITHUB_EVENT_NAME" with
                | Some eventName ->
                    match eventName with
                    | "pull_request" ->
                        Git.createEasyBuildStash ()
                        Git.dropLastCommit ()
                        Git.applyEasyBuildStash ()
                    | _ -> ()
                | None -> ()

            Git.commitAll prContext.Title
            let! remoteName = Git.getRemoteName ()
            Git.setUpstreamAndForcePush remoteName prContext.BranchName

            // If we were in a real branch before, we can switch back to it and delete the temporary branch
            // Detached HEAD happens on Github Actions
            if currentBranch <> "HEAD" then
                Git.switch currentBranch
                Git.deleteBranch prContext.BranchName

            let prBody =
                [
                    "## 🤖 New versions available"
                    ""
                    prContext.PullRequestSummaryTable
                    ""
                    prContext.ChangelogSummary
                    ""
                    "---"
                    "This PR was created automatically by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt)"
                ]
                |> String.concat "\n"

            orchestrator.EnsureLabelsExist()

            let openedReleasePRs = orchestrator.GetOpenedPullRequests(prContext.BranchName)

            // No PR were found, we can create a new one from scratch
            if openedReleasePRs.Length = 0 then
                orchestrator.CreateReleasePullRequest prContext.BranchName prContext.Title prBody

                return ()
            // A PR already exists, we need to update it
            else if openedReleasePRs.Length = 1 then
                // Update the existing PR
                orchestrator.UpdateReleasePullRequest
                    openedReleasePRs.Head.Number
                    prContext.Title
                    prBody

                return ()
            // Multiple PRs were found, we cannot proceed further
            // I think this is an invalid state for Github, but I am not sure
            else
                let openedReleasePRsList =
                    openedReleasePRs
                    |> List.map _.Url
                    |> String.concat "\n- "
                    |> String.prepend "- "

                return!
                    Error
                        $"""Multiple pull requests for release are already opened, please close the following PR(s) before trying again:

%s{openedReleasePRsList}"""
    }
