module EasyBuild.ShipIt.Tests.Dependencies

open System.IO
open TUnit.Core
open Tests.Utils
open Semver
open EasyBuild.CommitParser.Types
open EasyBuild.ShipIt.Generate
open EasyBuild.ShipIt.Generate.Types
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Types.Settings
open Workspace

let private makeChangelog (directory: string) (version: SemVersion) (metadata: ChangelogMetadata) =
    {
        File = FileInfo(Path.Combine(Workspace.``.``, directory, "CHANGELOG.md"))
        Content = ""
        Versions = [ version ]
        Metadata = metadata
    }

let private dependsOn (dependencies: string list) =
    { ChangelogMetadata.Empty with
        DependsOn = dependencies
    }

let private directoryNames (changelogs: ChangelogInfo list) =
    changelogs |> List.map _.File.Directory.Name

let private settings () =
    DefaultCommandSettings(GitRepositoryRoot = Workspace.``.``)

let private computeAll (commits: Git.Commit list) (changelogs: ChangelogInfo list) =
    changelogs
    |> ReleaseContext.computeAll (settings ()) (fun _ -> commits) CommitParserConfig.Default
    |> Result.defaultWith failwith

let private bumpInfo (releaseContext: ReleaseContext) =
    match releaseContext with
    | BumpRequired bumpInfo -> bumpInfo
    | NoVersionBumpRequired changelog ->
        failwith $"Expected a version bump for '%s{changelog.File.FullName}'"

[<Category("Dependencies.resolve")>]
type ResolveTests() =

    [<Test>]
    member _.``Accepts a directory or a changelog file``() =
        let plugin = makeChangelog "plugin" (SemVersion(1, 0, 0)) ChangelogMetadata.Empty
        let core = makeChangelog "core" (SemVersion(1, 0, 0)) ChangelogMetadata.Empty

        let dom =
            makeChangelog
                "dom"
                (SemVersion(1, 0, 0))
                (dependsOn
                    [
                        "../plugin/"
                        "../core/CHANGELOG.md"
                    ])

        let graph =
            Dependencies.resolve
                [
                    plugin
                    core
                    dom
                ]
            |> Result.defaultWith failwith

        let _, domDependencies = graph |> List.find (fun (changelog, _) -> changelog = dom)

        Expect.equal
            (directoryNames domDependencies)
            [
                "plugin"
                "core"
            ]

    [<Test>]
    member _.``Fails if a dependency does not point to a known changelog``() =
        let dom = makeChangelog "dom" (SemVersion(1, 0, 0)) (dependsOn [ "../unknown/" ])

        match Dependencies.resolve [ dom ] with
        | Ok _ -> failwith "Expected an error"
        | Error error -> Expect.stringContains error "'../unknown/'"

[<Category("Dependencies.sort")>]
type SortTests() =

    [<Test>]
    member _.``Dependencies come before their dependents regardless of priority``() =
        let core =
            makeChangelog
                "core"
                (SemVersion(1, 0, 0))
                { ChangelogMetadata.Empty with
                    Priority = Some 2
                }

        let dom =
            makeChangelog
                "dom"
                (SemVersion(1, 0, 0))
                { dependsOn [ "../core/" ] with
                    Priority = Some 1
                }

        let docs =
            makeChangelog
                "docs"
                (SemVersion(1, 0, 0))
                { ChangelogMetadata.Empty with
                    Priority = Some 0
                }

        let actual =
            Dependencies.resolve
                [
                    dom
                    core
                    docs
                ]
            |> Result.bind Dependencies.sort
            |> Result.defaultWith failwith

        Expect.equal
            (directoryNames actual)
            [
                "docs"
                "core"
                "dom"
            ]

    [<Test>]
    member _.``Fails on circular dependencies``() =
        let a = makeChangelog "a" (SemVersion(1, 0, 0)) (dependsOn [ "../b/" ])
        let b = makeChangelog "b" (SemVersion(1, 0, 0)) (dependsOn [ "../a/" ])

        match
            Dependencies.resolve
                [
                    a
                    b
                ]
            |> Result.bind Dependencies.sort
        with
        | Ok _ -> failwith "Expected an error"
        | Error error -> Expect.stringContains error "Circular"

[<Category("ReleaseContext.computeAll")>]
type ComputeAllTests() =

    [<Test>]
    member _.``A release cascades to direct and transitive dependents``() =
        let plugin = makeChangelog "plugin" (SemVersion(1, 0, 0)) ChangelogMetadata.Empty
        let dom = makeChangelog "dom" (SemVersion(1, 0, 0)) (dependsOn [ "../plugin/" ])
        let form = makeChangelog "form" (SemVersion(1, 0, 0)) (dependsOn [ "../dom/" ])

        let commits =
            [
                Git.Commit.Create(
                    "49c0699af98a67f1e8efcac8b1467b283a244aa8",
                    "fix: fix the plugin",
                    files = [ "plugin/somefile.txt" ]
                )
            ]

        let actual =
            computeAll
                commits
                [
                    form
                    dom
                    plugin
                ]
            |> List.map bumpInfo

        Expect.equal
            (actual
             |> List.map (fun info -> info.Changelog.File.Directory.Name, info.NewVersion))
            [
                "plugin", SemVersion(1, 0, 1)
                "dom", SemVersion(1, 0, 1)
                "form", SemVersion(1, 0, 1)
            ]

        Expect.equal
            (actual |> List.map _.DependencyUpdates)
            [
                []
                [
                    {
                        Name = "plugin"
                        NewVersion = SemVersion(1, 0, 1)
                    }
                ]
                [
                    {
                        Name = "dom"
                        NewVersion = SemVersion(1, 0, 1)
                    }
                ]
            ]

        Expect.isEmpty actual[1].CommitsForRelease

    [<Test>]
    member _.``A dependency change that does not release does not trigger its dependents``() =
        let plugin = makeChangelog "plugin" (SemVersion(1, 0, 0)) ChangelogMetadata.Empty
        let dom = makeChangelog "dom" (SemVersion(1, 0, 0)) (dependsOn [ "../plugin/" ])

        let commits =
            [
                Git.Commit.Create(
                    "49c0699af98a67f1e8efcac8b1467b283a244aa8",
                    "chore: tidy the plugin",
                    files = [ "plugin/somefile.txt" ]
                )
            ]

        let actual =
            computeAll
                commits
                [
                    plugin
                    dom
                ]

        Expect.equal
            actual
            [
                NoVersionBumpRequired plugin
                NoVersionBumpRequired dom
            ]

    [<Test>]
    member _.``A dependency release increments the pre-release number``() =
        let plugin = makeChangelog "plugin" (SemVersion(1, 0, 0)) ChangelogMetadata.Empty

        let dom =
            makeChangelog
                "dom"
                (SemVersion.Parse("1.0.0-beta.7", SemVersionStyles.Strict))
                { dependsOn [ "../plugin/" ] with
                    PreRelease = Some "beta"
                }

        let commits =
            [
                Git.Commit.Create(
                    "49c0699af98a67f1e8efcac8b1467b283a244aa8",
                    "fix: fix the plugin",
                    files = [ "plugin/somefile.txt" ]
                )
            ]

        let actual =
            computeAll
                commits
                [
                    plugin
                    dom
                ]

        Expect.equal
            (bumpInfo actual[1]).NewVersion
            (SemVersion.Parse("1.0.0-beta.8", SemVersionStyles.Strict))

    [<Test>]
    member _.``Own commits and dependency releases combine, the highest bump wins``() =
        let plugin = makeChangelog "plugin" (SemVersion(1, 0, 0)) ChangelogMetadata.Empty
        let dom = makeChangelog "dom" (SemVersion(1, 0, 0)) (dependsOn [ "../plugin/" ])

        let commits =
            [
                Git.Commit.Create(
                    "49c0699af98a67f1e8efcac8b1467b283a244aa8",
                    "feat: add a feature to dom",
                    files = [ "dom/somefile.txt" ]
                )
                Git.Commit.Create(
                    "2a6f3b3403aaa629de6e65558448b37f126f8e86",
                    "fix: fix the plugin",
                    files = [ "plugin/somefile.txt" ]
                )
            ]

        let domBumpInfo =
            computeAll
                commits
                [
                    plugin
                    dom
                ]
            |> List.item 1
            |> bumpInfo

        Expect.equal domBumpInfo.NewVersion (SemVersion(1, 1, 0))
        Expect.equal (domBumpInfo.CommitsForRelease |> List.map _.OriginalCommit) [ commits[0] ]
        Expect.equal (domBumpInfo.DependencyUpdates |> List.map _.Name) [ "plugin" ]
