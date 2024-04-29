module EasyBuild.ShipIt.Generate.Changelog

open System
open System.IO
open Semver
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Generate.Types
open System.Text.RegularExpressions
open Microsoft.Extensions.FileSystemGlobbing
open Microsoft.Extensions.FileSystemGlobbing.Abstractions
open Thoth.Yaml
open FsToolkit.ErrorHandling

[<Literal>]
let EMPTY_CHANGELOG =
    """# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This changelog is generated using [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

⚠ Only edit the front matter metadata at the top of this file. All other changes will be overwritten when a new release is created.
"""

let findVersions (content: string) =

    let matches =
        Regex.Matches(
            content,
            "^##\\s\\[?v?(?<version>[\\w\\d.-]+\\.[\\w\\d.-]+[a-zA-Z0-9])\\]?(\\s-\\s(?<date>\\d{4}-\\d{2}-\\d{2}))?$",
            RegexOptions.Multiline
        )

    matches
    |> Seq.map (fun m ->
        let version = m.Groups.["version"].Value

        match SemVersion.TryParse(version, SemVersionStyles.Strict) with
        | true, version -> version
        | false, _ -> failwith "Invalid version"
    )
    |> Seq.toList

let find (settings: Settings.SharedSettings) =
    let matcher = Matcher()
    matcher.AddInclude("**/CHANGELOG.md") |> ignore

    let directoryInfo = DirectoryInfo(settings.Cwd)
    let dirWrapper = DirectoryInfoWrapper(directoryInfo)

    matcher.Execute(dirWrapper).Files
    |> Seq.map (fun file -> Path.Combine(settings.Cwd, file.Path) |> FileInfo)

let tryLoad (changelogFile: FileInfo) =
    result {
        if not changelogFile.Exists then
            Log.info ($"File '{changelogFile.FullName}' does not exist, creating a new one.")

            return
                {
                    File = changelogFile
                    Content = EMPTY_CHANGELOG
                    Versions = []
                    Metadata = ChangelogMetadata.Empty
                }

        else
            Log.info ($"Changelog file '{changelogFile.FullName}' found.")

            let changelogContent = File.ReadAllText(changelogFile.FullName)

            let frontMatterRegex =
                Regex(
                    "^(---\n(?'metadata'.*?)\n---\n)?(?'content'.*)",
                    RegexOptions.Singleline ||| RegexOptions.Multiline
                )

            let m = frontMatterRegex.Match(changelogContent)

            if not m.Success then
                return! Error "Failed to parse changelog."

            else
                let metadataText = m.Groups.["metadata"].Value.Trim()

                let! changelogMetadata =
                    if String.IsNullOrWhiteSpace(metadataText) then
                        Ok ChangelogMetadata.Empty
                    else
                        match Decode.fromString ChangelogMetadata.Decoder metadataText with
                        | Ok changelogMetadata -> Ok changelogMetadata
                        | Error error -> Error $"Failed to parse changelog metadata:\n\n{error}"

                return
                    {
                        File = changelogFile
                        Content = changelogContent
                        Versions = findVersions changelogContent
                        Metadata = changelogMetadata
                    }
    }

let tryFindAdditionalChangelogContent (text: string) : string list list =
    let lines = text.Replace("\r\n", "\n").Split('\n') |> Seq.toList

    let rec apply
        (acc: string list list)
        (lines: string list)
        (currentBlock: string list)
        (isInsideChangelogBlock: bool)
        =
        match lines with
        | [] -> acc
        | line :: rest ->
            if isInsideChangelogBlock then
                if line = "=== changelog ===" then
                    apply (acc @ [ currentBlock ]) rest [] false
                else
                    apply acc rest (currentBlock @ [ line ]) true
            else if line = "=== changelog ===" then
                apply acc rest currentBlock true
            else
                apply acc rest currentBlock false

    apply [] lines [] false

module Literals =

    module Type =

        [<Literal>]
        let FEAT = "feat"

        [<Literal>]
        let PERF = "perf"

        [<Literal>]
        let FIX = "fix"

let (|BreakingChange|Feat|Perf|Fix|Other|) (commit: EasyBuild.CommitParser.Types.CommitMessage) =
    if commit.BreakingChange then
        BreakingChange
    elif commit.Type = Literals.Type.FEAT then
        Feat
    elif commit.Type = Literals.Type.PERF then
        Perf
    elif commit.Type = Literals.Type.FIX then
        Fix
    else
        Other

type GroupedCommits =
    {
        BreakingChanges: CommitForRelease list
        Feats: CommitForRelease list
        Perfs: CommitForRelease list
        Fixes: CommitForRelease list
    }

type Writer() =
    let lines = ResizeArray<string>()

    member _.AppendLine(line: string) = lines.Add(line)

    member _.NewLine() = lines.Add("")

    member _.ToText() = lines |> String.concat "\n"

let private writeSection
    (writer: Writer)
    (label: string)
    (remote: RemoteConfig)
    (commits: CommitForRelease list)
    =
    if commits.Length > 0 then
        writer.AppendLine $"### %s{label}"
        writer.NewLine()

        let commits = commits |> List.sortBy (fun commit -> commit.SemanticCommit.Scope)

        for commit in commits do
            let githubCommitUrl sha = $"%s{remote.BaseUrl}/commit/%s{sha}"

            let commitUrl = githubCommitUrl commit.OriginalCommit.Hash

            let description = String.capitalizeFirstLetter commit.SemanticCommit.Description

            [
                "*"
                match commit.SemanticCommit.Scope with
                | Some scope -> $"*(%s{scope})*"
                | None -> ()
                $"%s{description.Trim()} ([%s{commit.OriginalCommit.AbbrevHash}](%s{commitUrl}))"
            ]
            |> String.concat " "
            |> writer.AppendLine

            let additionalChangelogContent =
                tryFindAdditionalChangelogContent commit.SemanticCommit.Body

            for blockLines in additionalChangelogContent do
                writer.NewLine()

                for line in blockLines do
                    $"    %s{line}" |> _.TrimEnd() |> writer.AppendLine

        writer.NewLine()

let generateNewVersionSection
    (remote: RemoteConfig)
    (previousReleasedSha: string option)
    (releaseContext: BumpInfo)
    =
    let writer = Writer()

    [
        "##"
        $"%s{releaseContext.NewVersion.ToString()}"
        "-"
        // If in debug mode, use a fixed date to make testing stable
#if DEBUG
        "2024-11-18"
#else
        DateTime.UtcNow.ToString("yyyy-MM-dd")
#endif
    ]
    |> String.concat " "
    |> writer.AppendLine

    writer.NewLine()

    let rec groupCommits (acc: GroupedCommits) (commits: CommitForRelease list) =

        match commits with
        | [] -> acc
        | commit :: rest ->
            match commit.SemanticCommit with
            | BreakingChange ->
                groupCommits
                    { acc with
                        BreakingChanges = commit :: acc.BreakingChanges
                    }
                    rest
            | Feat ->
                groupCommits
                    { acc with
                        Feats = commit :: acc.Feats
                    }
                    rest
            | Fix ->
                groupCommits
                    { acc with
                        Fixes = commit :: acc.Fixes
                    }
                    rest
            | Perf ->
                groupCommits
                    { acc with
                        Perfs = commit :: acc.Perfs
                    }
                    rest
            // This commit type is not to be emitted in the changelog
            | Other -> groupCommits acc rest

    let groupedCommits =
        groupCommits
            {
                BreakingChanges = []
                Feats = []
                Perfs = []
                Fixes = []
            }
            releaseContext.CommitsForRelease

    writeSection writer "🏗️ Breaking changes" remote groupedCommits.BreakingChanges
    writeSection writer "🚀 Features" remote groupedCommits.Feats
    writeSection writer "🐞 Bug Fixes" remote groupedCommits.Fixes
    writeSection writer "⚡ Performance Improvements" remote groupedCommits.Perfs

    match previousReleasedSha with
    | Some sha ->
        let compareUrl =
            $"%s{remote.BaseUrl}/compare/%s{sha}..%s{releaseContext.LastCommitSha}"

        let remoteName = remote.NameOnly |> String.capitalizeFirstLetter

        $"<strong><small>[View changes on %s{remoteName}](%s{compareUrl})</small></strong>"
        |> writer.AppendLine

        writer.NewLine()

    | None -> ()

    writer.ToText()

let updateWithNewVersion (githubRemote: RemoteConfig) (bumpInfo: BumpInfo) =
    let newVersionLines =
        generateNewVersionSection
            githubRemote
            bumpInfo.Changelog.Metadata.LastCommitReleased
            bumpInfo

    let rec removeConsecutiveEmptyLines
        (previousLineWasBlank: bool)
        (result: string list)
        (lines: string list)
        =
        match lines with
        | [] -> result
        | line :: rest ->
            if previousLineWasBlank && String.IsNullOrWhiteSpace(line) then
                removeConsecutiveEmptyLines true result rest
            else
                removeConsecutiveEmptyLines
                    (String.IsNullOrWhiteSpace(line))
                    (result @ [ line ])
                    rest

    // Update metadata with the new information
    let changelogInfo =
        { bumpInfo.Changelog with
            Metadata.LastCommitReleased = Some bumpInfo.LastCommitSha
        }

    let newChangelogContent =
        [
            // Add EasyBuild metadata
            changelogInfo.Metadata
            |> ChangelogMetadata.Encoder
            |> Encode.toString
            |> String.prepend "---\n"
            |> String.append "---"
            // Remove starting and ending new lines, from description
            // we are adding them ourselves manually to have better control
            // and keep formatting consistent
            ""
            changelogInfo.Description.Trim()
            ""
            // New version
            newVersionLines

            // Add the rest of the changelog
            changelogInfo.VersionsText
        ]
        |> removeConsecutiveEmptyLines false []
        |> String.concat "\n"

    newChangelogContent
