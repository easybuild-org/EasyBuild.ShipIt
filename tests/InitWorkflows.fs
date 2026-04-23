module EasyBuild.ShipIt.Tests.InitWorkflows

open System
open System.IO
open TUnit.Core
open Tests.Utils
open EasyBuild.ShipIt.Commands.InitWorkflows
open EasyBuild.ShipIt.Types.Settings

let runInTempDir (action: string -> unit) =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    try
        action tempDir
    finally
        try
            Directory.Delete(tempDir, true)
        with _ ->
            ()

[<Category("InitWorkflows")>]
type InitWorkflowsTests() =

    [<Test>]
    member _.``InitWorkflows creates workflows at default path``() =
        runInTempDir (fun tempDir ->
            let settings = InitWorkflowsSettings(Cwd = tempDir)
            let command = InitWorkflowsCommand()

            let exitCode =
                command.Execute(null, settings, System.Threading.CancellationToken.None)

            Expect.equal exitCode 0

            let workflowsDir = Path.Combine(tempDir, ".github", "workflows")
            Expect.isTrue (Directory.Exists(workflowsDir))
            Expect.isTrue (File.Exists(Path.Combine(workflowsDir, "conventional-pr-title.yml")))
            Expect.isTrue (File.Exists(Path.Combine(workflowsDir, "easybuild-shipit.yml")))
        )

    [<Test>]
    member _.``InitWorkflows fails if any workflow already exists``() =
        runInTempDir (fun tempDir ->
            let workflowsDir = Path.Combine(tempDir, ".github", "workflows")
            Directory.CreateDirectory(workflowsDir) |> ignore

            File.WriteAllText(
                Path.Combine(workflowsDir, "conventional-pr-title.yml"),
                "existing content"
            )

            let settings = InitWorkflowsSettings(Cwd = tempDir)
            let command = InitWorkflowsCommand()

            let exitCode =
                command.Execute(null, settings, System.Threading.CancellationToken.None)

            Expect.equal exitCode 1

            // The other file should not have been created
            Expect.isFalse (File.Exists(Path.Combine(workflowsDir, "easybuild-shipit.yml")))
        )
