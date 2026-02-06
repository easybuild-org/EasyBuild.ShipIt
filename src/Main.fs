module EasyBuild.ShipIt.Main

open Spectre.Console.Cli
open EasyBuild.ShipIt.Commands.Version
open EasyBuild.ShipIt.Commands.Default
open EasyBuild.ShipIt.Commands.Github

let mutable private helpWasCalled = false

type CustomHelperProvider(settings: ICommandAppSettings) =
    inherit Help.HelpProvider(settings)

    override _.GetUsage
        (model: Help.ICommandModel, command: Help.ICommandInfo)
        : System.Collections.Generic.IEnumerable<Spectre.Console.Rendering.IRenderable>
        =
        helpWasCalled <- true
        base.GetUsage(model, command)

[<EntryPoint>]
let main args =

    // Default command is for GitHub
    let app = CommandApp<DefaultCommand>()

    app
        .WithDescription(
            "Automate changelog generation based on conventional commit messages and create pull requests for releases.

The tool will do its best to automatically detect the Git provider based on the git remote URL.
You can force it to use a specific provider using sub-commands, e.g. 'shipit github' to force using GitHub.

Learn more at https://github.com/easybuild-org/EasyBuild.ShipIt"
        )
        .Configure(fun config ->
            config.Settings.ApplicationName <- "shipit"
            config.SetHelpProvider(CustomHelperProvider(config.Settings)) |> ignore
            config.AddCommand<VersionCommand>("version") |> ignore

            config.SetExceptionHandler(fun ex _ ->
                Log.error "An error occured, please review the logs above and details below."
                Log.exn ex
                1
            )
            |> ignore

            config.AddCommand<GithubCommand>("github").WithDescription("Publish to GitHub")
            |> ignore
        )

    let exitCode = app.Run(args)

    if helpWasCalled then
        // Make it easy for caller to know when help was called
        100
    else
        exitCode
