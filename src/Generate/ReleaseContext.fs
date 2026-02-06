module EasyBuild.ShipIt.Generate.ReleaseContext

open Semver
open EasyBuild.CommitParser
open EasyBuild.CommitParser.Types
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Generate.Types
open Microsoft.Extensions.FileSystemGlobbing
open System.IO
open FsToolkit.ErrorHandling

let getCommits (changelog: ChangelogInfo) =
    let commitFilter =
        match changelog.Metadata.LastCommitReleased with
        | Some lastReleasedCommit -> Git.GetCommitsFilter.From lastReleasedCommit
        | None -> Git.GetCommitsFilter.All

    Git.getCommits commitFilter

let computeVersion
    (settings: Settings.SharedSettings)
    (changelog: ChangelogInfo)
    (commitsForRelease: CommitForRelease list)
    (refVersion: SemVersion)
    =

    // Detect if we are doing a pre-release based on the settings or the changelog metadata
    // CLI settings take precedence over the changelog metadata
    let preRelease =
        if settings.PreRelease.IsSet then
            Some settings.PreRelease.Value
        else
            changelog.Metadata.PreRelease

    if refVersion.IsPrerelease then
        match preRelease with
        | Some preReleasePrefix ->
            // If the pre-release identifier is the same, then increment the pre-release number
            // Before: 2.0.0-beta.1 -> After: 2.0.0-beta.2
            if refVersion.Prerelease.StartsWith(preReleasePrefix) then
                let index = refVersion.Prerelease.IndexOf(preReleasePrefix + ".")

                if index >= 0 then
                    let preReleaseNumber =
                        refVersion.Prerelease.Substring(index + preReleasePrefix.Length + 1) |> int

                    refVersion.WithPrereleaseParsedFrom(
                        preReleasePrefix + "." + (preReleaseNumber + 1).ToString()
                    )
                    |> Some
                else
                    // This should never happen
                    // If the pre-release identifier is present, then the pre-release number should also be present
                    // If the pre-release identifier is not present, then the version should be a release
                    // So, this should be a safe assumption
                    failwith "Invalid pre-release identifier"
            // Otherwise, start a new pre-release from 1
            // This can happens when moving from alpha to beta, for example
            // Before: 2.0.0-alpha.1 -> After: 2.0.0-beta.1
            else
                refVersion.WithPrereleaseParsedFrom(preReleasePrefix + ".1") |> Some
        // If the last version is a release, and user requested a stable release
        // Then, remove the pre-release identifier
        // Example: 2.0.0-beta.1 -> 2.0.0
        | None -> refVersion.WithoutPrereleaseOrMetadata() |> Some

    else
        let shouldBumpMajor =
            commitsForRelease
            |> List.exists (fun commit -> commit.SemanticCommit.BreakingChange)

        let shouldBumpMinor =
            commitsForRelease
            |> List.exists (fun commit ->
                commit.SemanticCommit.Type = "feat" || commit.SemanticCommit.Type = "perf"
            )

        let shouldBumpPatch =
            commitsForRelease
            |> List.exists (fun commit -> commit.SemanticCommit.Type = "fix")

        let bumpMajor () =
            refVersion
                .WithMajor(refVersion.Major + 1I)
                .WithMinor(0)
                .WithPatch(0)
                .WithoutPrereleaseOrMetadata()

        let bumpMinor () =
            refVersion
                .WithMinor(refVersion.Minor + 1I)
                .WithPatch(0)
                .WithoutPrereleaseOrMetadata()

        let bumpPatch () =
            refVersion.WithPatch(refVersion.Patch + 1I).WithoutPrereleaseOrMetadata()

        // If the last version is a release, and user requested a pre-release
        // Then we compute the standard release version and add the pre-release identifier starting from 1
        // Example:
        // - Major bump needed: 2.0.0 -> 3.0.0-beta.1
        // - Minor bump needed: 2.0.0 -> 2.1.0-beta.1
        // - Patch bump needed: 2.0.0 -> 2.0.1-beta.1
        match preRelease with
        | Some preReleasePrefix ->
            let applyPreReleaseIdentifier (version: SemVersion) =
                version.WithPrereleaseParsedFrom(preReleasePrefix + ".1")

            if shouldBumpMajor then
                bumpMajor () |> applyPreReleaseIdentifier |> Some
            elif shouldBumpMinor then
                bumpMinor () |> applyPreReleaseIdentifier |> Some
            elif shouldBumpPatch then
                bumpPatch () |> applyPreReleaseIdentifier |> Some
            else
                None

        // If the last version is a release, and user requested a stable release
        // Then we compute the standard release version
        // Example:
        // - Major bump needed: 2.0.0 -> 3.0.0
        // - Minor bump needed: 2.0.0 -> 2.1.0
        // - Patch bump needed: 2.0.0 -> 2.0.1
        | None ->
            let removePreReleaseIdentifier (version: SemVersion) =
                version.WithoutPrereleaseOrMetadata()

            if shouldBumpMajor then
                bumpMajor () |> removePreReleaseIdentifier |> Some
            elif shouldBumpMinor then
                bumpMinor () |> removePreReleaseIdentifier |> Some
            elif shouldBumpPatch then
                bumpPatch () |> removePreReleaseIdentifier |> Some
            else
                None

[<RequireQualifiedAccess>]
module Matcher =

    let inline addIncludePatterns (matcher: Matcher) (patterns: string list) =
        matcher.AddIncludePatterns(patterns)

    let inline addExcludePatterns (matcher: Matcher) (patterns: string list) =
        matcher.AddExcludePatterns(patterns)

let compute
    (settings: Settings.SharedSettings)
    (changelog: ChangelogInfo)
    (commitsCandidates: Git.Commit list)
    (commitParserConfig: CommitParserConfig)
    =

    let commitsForRelease =
        commitsCandidates
        // Parse the commit message
        |> List.choose (fun commit ->
            match Parser.tryParseCommitMessage commitParserConfig commit.RawBody with
            | Ok semanticCommit ->
                Some
                    {
                        OriginalCommit = commit
                        SemanticCommit = semanticCommit
                    }
            | Error error ->
                if settings.SkipInvalidCommit then
                    Log.warning $"Failed to parse commit message: {error}"
                    None
                else if settings.SkipMergeCommit && commit.RawBody.StartsWith("Merge ") then
                    // Skip merge commits
                    None
                else
                    let error =
                        $"Failed to parse commit message:

==============
    Commit
==============

%s{commit.RawBody}

==============
    Error
==============

%s{error}"

                    failwith error
        )
        // Only include commits that have the type feat, fix or is marked as a breaking change
        |> List.filter (fun commit ->
            commit.SemanticCommit.Type = "feat"
            || commit.SemanticCommit.Type = "perf"
            || commit.SemanticCommit.Type = "fix"
            || commit.SemanticCommit.BreakingChange
        )
        // Only keep the commits that have the tags we are looking for
        // or all commits if no tags are provided
        |> List.filter (fun commit ->
            // We need to modify the "context" of the matcher to be relative to the git
            // repository root. This is because when doing `../XXXX` patterns we need to
            // escape from the matcher.
            // By using, Git repository root as reference, we can modify the include patterns
            // to be relative to that root and allows to include files from outside the current
            // CHANGELOG directory.
            let relativeSourcePath =
                Path.GetRelativePath(settings.GitRepositoryRoot, changelog.File.DirectoryName)

            let matcher = Matcher()
            // By default, we include all files under the current CHANGELOG directory
            matcher.AddInclude(relativeSourcePath + "/**/*") |> ignore

            let resolveRelativePattern (pattern: string) =
                let absolutePath =
                    Path.GetFullPath(Path.Combine(changelog.File.DirectoryName, pattern))

                Path.GetRelativePath(settings.GitRepositoryRoot, absolutePath)

            changelog.Metadata.Include
            |> List.map resolveRelativePattern
            |> Matcher.addIncludePatterns matcher

            changelog.Metadata.Exclude
            |> List.map resolveRelativePattern
            |> Matcher.addExcludePatterns matcher

            let gitFiles =
                commit.OriginalCommit.Files
                |> List.map (fun filePath -> settings.GitRepositoryRoot + "/" + filePath)

            matcher.Match(settings.GitRepositoryRoot, gitFiles).HasMatches
        )

    let refVersion = changelog.LastVersion

    let makeBumpInfo newVersion =
        {
            NewVersion = newVersion
            CommitsForRelease = commitsForRelease
            LastCommitSha = commitsCandidates[0].Hash
            Changelog = changelog
        }

    // If the user forced a version, then use that version
    match changelog.Metadata.ForceVersion with
    | Some version -> version |> makeBumpInfo |> BumpRequired

    | None ->
        match computeVersion settings changelog commitsForRelease refVersion with
        | Some newVersion -> makeBumpInfo newVersion |> BumpRequired
        | None -> NoVersionBumpRequired changelog

let apply (remoteConfig: RemoteConfig) (releaseContext: ReleaseContext) : Result<unit, string> =
    // Notify user about the status of each changelog in order
    match releaseContext with
    | NoVersionBumpRequired changelogInfo ->
        Log.info $"No version bump required for '{changelogInfo.File.FullName}'."
        Ok()

    | BumpRequired bumpInfo ->
        Log.info $"Bumping '{bumpInfo.Changelog.File.FullName}' to {bumpInfo.NewVersion}."

        // Update the changelog file with the new version
        File.WriteAllText(
            bumpInfo.Changelog.File.FullName,
            Changelog.updateWithNewVersion remoteConfig bumpInfo
        )

        bumpInfo.Changelog.Metadata.Updaters
        |> List.traverseResultM (fun updater ->
            let resolveAbsolutePath (path: string) =
                Path.Combine(bumpInfo.Changelog.File.DirectoryName, path)

            let updateFileContent (filePath: string) (newContent: string) =
                File.WriteAllText(filePath, newContent)

            match updater with
            | ChangelogMetadata.Updater.ReplaceRegex info ->
                let absolutePath = resolveAbsolutePath info.File

                Updater.Regex.replaceVersion bumpInfo.NewVersion info.Pattern absolutePath
                |> Result.map (updateFileContent absolutePath)

            | ChangelogMetadata.Updater.PackageJson packageJsonFile ->
                let absolutePath = resolveAbsolutePath packageJsonFile

                Updater.PackageJson.replaceVersion bumpInfo.NewVersion absolutePath
                |> Result.map (updateFileContent absolutePath)

            | ChangelogMetadata.Updater.JsonPatch info ->
                let absolutePath = resolveAbsolutePath info.File

                Updater.Json.replaceVersion bumpInfo.NewVersion absolutePath info.Pointer
                |> Result.map (updateFileContent absolutePath)

            | ChangelogMetadata.Updater.Xml info ->
                let absolutePath = resolveAbsolutePath info.File

                Updater.Xml.replaceVersion bumpInfo.NewVersion absolutePath info.Selector
                |> Result.map (updateFileContent absolutePath)

            | ChangelogMetadata.Updater.Command command ->
                Updater.Command.run
                    bumpInfo.Changelog.File.DirectoryName
                    command
                    bumpInfo.NewVersion

        )
        // Unwrap the success case as returning unit or list of unit is equivalent
        |> function
            | Ok _ -> Ok()
            | Error error ->
                Error
                    $"Failed to apply updaters for changelog '{bumpInfo.Changelog.File.FullName}':\n\n%s{error}"
