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

type ChangelogInfo =
    {
        File: FileInfo
        Content: string
        Versions: SemVersion list
        Metadata: ChangelogMetadata
    }

    member this.NameOrFilePath(rootDir: string) =
        match this.Metadata.Name with
        | Some name -> name
        | None -> Path.GetRelativePath(rootDir, this.File.FullName)

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
