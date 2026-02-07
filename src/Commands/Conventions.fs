module EasyBuild.ShipIt.Commands.Conventions

open Spectre.Console.Cli
open EasyBuild.CommitParser.Types
open EasyBuild.ShipIt.Types.Settings
open Spectre.Console

type ConventionsCommand() =
    inherit Command<ConventionsSettings>()

    interface ICommandLimiter<CommandSettings>

    override this.Execute(_context, settings, _ct) =
        let typeList =
            CommitParserConfig.Default.Types
            |> List.map (fun typeInfo ->
                let description =
                    match typeInfo.Description with
                    | Some text -> $": %s{text}"
                    | None -> ""

                $"- [bold]%s{typeInfo.Name}[/] %s{description}"
            )
            |> String.concat "\n"

        let commitDescription =
            new Panel(
                Markup.Escape
                    """<type>[optional scope][optional !]: <description>

[optional body]

[optional footer]"""
            )

        Log.output.WriteLine "EasyBuild.Shipit follows the Conventional Commits specification."
        Log.output.WriteLine()
        Log.output.Write commitDescription
        Log.output.WriteLine()

        Log.output.MarkupLine
            $"""The following commit types are supported:

%s{typeList}

Learn more:
    - https://www.conventionalcommits.org/en/v1.0.0/
    - https://github.com/easybuild-org/EasyBuild.ShipIt"""

        0
