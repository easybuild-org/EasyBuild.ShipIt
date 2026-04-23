module EasyBuild.ShipIt.Commands.InitWorkflows

open System.IO
open System.Reflection
open Spectre.Console.Cli
open EasyBuild.ShipIt.Types.Settings

module WorkflowTemplates =

    let private getResourceName (fileName: string) =
        let assembly = Assembly.GetExecutingAssembly()

        assembly.GetManifestResourceNames()
        |> Array.find (fun name -> name.EndsWith(fileName))

    let read (fileName: string) =
        let assembly = Assembly.GetExecutingAssembly()
        let resourceName = getResourceName fileName
        use stream = assembly.GetManifestResourceStream(resourceName)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

type InitWorkflowsCommand() =
    inherit Command<InitWorkflowsSettings>()

    interface ICommandLimiter<CommandSettings>

    override _.Execute(_context, settings, _ct) =
        let dirPath = Path.GetFullPath(".github/workflows/", settings.Cwd)
        Directory.CreateDirectory(dirPath) |> ignore

        let files =
            [
                "conventional-pr-title.yml"
                "easybuild-shipit.yml"
            ]

        // Check for existing files first
        let existingFiles =
            files
            |> List.map (fun fileName -> Path.Combine(dirPath, fileName))
            |> List.filter File.Exists

        if not (List.isEmpty existingFiles) then
            let fileList = existingFiles |> List.map (fun f -> $"  - {f}") |> String.concat "\n"

            Log.error
                $"The following workflow files already exist:\n%s{fileList}\n\nPlease remove them before running this command."

            1
        else
            for fileName in files do
                let content = WorkflowTemplates.read fileName
                let filePath = Path.Combine(dirPath, fileName)
                File.WriteAllText(filePath, content)
                Log.success $"Workflow created at '{filePath}'."

            0
