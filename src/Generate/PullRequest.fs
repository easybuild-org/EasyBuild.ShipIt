module EasyBuild.ShipIt.Generate.PullRequest

open System
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Generate.Types
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling

type PullRequestContext
    (
        items: ReleaseContext list,
        gitRepositoryRoot: string,
        remoteConfig: RemoteConfig,
        targetBranch: string
    )
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

    member _.BranchName = Ok $"release/{targetBranch}"

    member _.Title =
        // If the repo only have one project, we can use the specific version in the PR title
        if items.Length = 1 then
            match Seq.tryHead updatedItems with
            | None ->
                Error "Tried to generate a PR title with no updated items, this should not happen"
            | Some project ->
                let name = project.Changelog.NameWithVersion project.NewVersion gitRepositoryRoot

                Ok $"chore: release %s{name}"
        else
            Ok "chore: release multiple projects"

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

        // Store the current branch to restore it later
        // We also use it as part of the PR branch name to ensure we have only one PR per target branch
        let currentBranch = Git.getHeadBranchName ()

        let prContext =
            PullRequestContext(
                releaseContexts,
                settings.GitRepositoryRoot,
                remoteConfig,
                currentBranch
            )

        if prContext.ShouldCreate() |> not then
            return ()
        else

            do! orchestrator.VerifyAndSetupRequirements ReleaseMode.PullRequest

            let! remoteName = Git.getRemoteName ()

            // Git operations:
            // 1. Create a new branch and move to it
            // 2. Commit all changes and push the branch
            // 3. Restore the previous branch and delete the temporary branch

            let! branchName = prContext.BranchName
            let! prTitle = prContext.Title

            Git.switchAndMove branchName
            Git.commitAll prTitle
            Git.setUpstreamAndForcePush remoteName branchName
            Git.switch currentBranch
            Git.deleteBranch branchName

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

            let openedReleasePRs = orchestrator.GetOpenedPullRequests(branchName)

            // No PR were found, we can create a new one from scratch
            if openedReleasePRs.Length = 0 then
                orchestrator.CreateReleasePullRequest branchName prTitle prBody

                return ()
            // A PR already exists, we need to update it
            else if openedReleasePRs.Length = 1 then
                // Update the existing PR
                orchestrator.UpdateReleasePullRequest openedReleasePRs.Head.Number prTitle prBody

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
