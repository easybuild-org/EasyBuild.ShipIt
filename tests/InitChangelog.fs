module EasyBuild.ShipIt.Tests.InitChangelog

open System
open System.IO
open TUnit.Core
open Tests.Utils
open EasyBuild.ShipIt.Commands.InitChangelog
open EasyBuild.ShipIt.Types.Settings

let runInTempDir (action: string -> 'a) : 'a =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    try
        action tempDir
    finally
        try
            Directory.Delete(tempDir, true)
        with _ ->
            ()

[<Category("InitChangelog")>]
type InitChangelogTests() =

    [<Test>]
    member _.``InitChangelog creates changelog at default path``() =
        runInTempDir (fun tempDir ->
            let settings = InitChangelogSettings(Cwd = tempDir)
            let command = InitChangelogCommand()

            let exitCode =
                command.Execute(null, settings, System.Threading.CancellationToken.None)

            Expect.equal exitCode 0

            let changelogPath = Path.Combine(tempDir, "CHANGELOG.md")
            Expect.isTrue (File.Exists(changelogPath))

            let content = File.ReadAllText(changelogPath)
            Expect.equal content EasyBuild.ShipIt.Generate.Changelog.EMPTY_CHANGELOG
        )

    [<Test>]
    member _.``InitChangelog creates changelog at custom path``() =
        runInTempDir (fun tempDir ->
            let settings = InitChangelogSettings(Path = Some "docs/CHANGELOG.md", Cwd = tempDir)
            let command = InitChangelogCommand()

            let exitCode =
                command.Execute(null, settings, System.Threading.CancellationToken.None)

            Expect.equal exitCode 0

            let changelogPath = Path.Combine(tempDir, "docs", "CHANGELOG.md")
            Expect.isTrue (File.Exists(changelogPath))

            let content = File.ReadAllText(changelogPath)
            Expect.equal content EasyBuild.ShipIt.Generate.Changelog.EMPTY_CHANGELOG
        )

    [<Test>]
    member _.``InitChangelog fails if changelog already exists``() =
        runInTempDir (fun tempDir ->
            let changelogPath = Path.Combine(tempDir, "CHANGELOG.md")
            File.WriteAllText(changelogPath, "existing content")

            let settings = InitChangelogSettings(Cwd = tempDir)
            let command = InitChangelogCommand()

            let exitCode =
                command.Execute(null, settings, System.Threading.CancellationToken.None)

            Expect.equal exitCode 1

            let content = File.ReadAllText(changelogPath)
            Expect.equal content "existing content"
        )
