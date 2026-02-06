module EasyBuild.ShipIt.Generate.Types

open Spectre.Console.Cli
open System.ComponentModel
open System
open System.IO
open Semver
open EasyBuild.CommitParser.Types
open EasyBuild.ShipIt.Types
open System.Text.RegularExpressions

module private Line =

    let isNotVersion (line: string) = not (line.StartsWith("##"))

    let isFrontMatterDelimiter (line: string) = line.Trim() = "---"

module Literals =

    [<Literal>]
    let UNNAMED_CHANGELOG = "Unnamed"

type ChangelogInfo =
    {
        File: FileInfo
        Content: string
        Versions: SemVersion list
        Metadata: ChangelogMetadata
    }

    /// <summary>
    /// Get a user-friendly name for the changelog, used for display purposes and to generate branch names.
    /// </summary>
    /// <param name="rootDir">The root directory of the git repository, used to compute a relative path if no name is provided in the metadata.</param>
    /// <returns>
    /// The name of the changelog if specified in the metadata, otherwise the relative path from the repository root to the changelog file.
    /// </returns>
    member this.NameOrDirectoryPath(rootDir: string) =
        match this.Metadata.Name with
        | Some name -> name
        | None ->
            let resolvedDir =
                Path.GetRelativePath(rootDir, this.File.FullName) |> Path.GetDirectoryName

            if String.IsNullOrEmpty(resolvedDir) then
                Literals.UNNAMED_CHANGELOG
            else
                resolvedDir.Replace(Path.DirectorySeparatorChar, '/')

    member this.NameWithVersion (version: SemVersion) (rootDir: string) =
        let baseName = this.NameOrDirectoryPath(rootDir)
        let versionText = version.ToString()

        if baseName = Literals.UNNAMED_CHANGELOG then
            versionText
        else
            $"%s{baseName}@%s{versionText}"

    member this.LastVersion =
        match List.tryHead this.Versions with
        | Some version -> version
        | None -> SemVersion(0, 0, 0)

    member this.Lines = this.Content |> String.normalizeNewLines |> String.splitBy '\n'

    member this.Description =
        let hasEasyBuildMetadata =
            match this.Lines with
            | firstLine :: _ when Line.isFrontMatterDelimiter firstLine -> true
            | _ -> false

        let filteredLines =
            if hasEasyBuildMetadata then
                this.Lines
                |> Seq.skip 1 // Skip first '---'
                |> Seq.skipWhile (Line.isFrontMatterDelimiter >> not)
                |> Seq.skip 1 // Skip second '---'
                |> Seq.takeWhile Line.isNotVersion
            else
                this.Lines |> Seq.takeWhile Line.isNotVersion

        filteredLines |> String.concat "\n"

    member this.VersionsText =
        this.Lines |> Seq.skipWhile Line.isNotVersion |> String.concat "\n"

type CommitForRelease =
    {
        OriginalCommit: Git.Commit
        SemanticCommit: CommitMessage
    }

type BumpInfo =
    {
        NewVersion: SemVersion
        CommitsForRelease: CommitForRelease list
        LastCommitSha: string
        Changelog: ChangelogInfo
    }

type ReleaseContext =
    | NoVersionBumpRequired of ChangelogInfo
    | BumpRequired of BumpInfo
