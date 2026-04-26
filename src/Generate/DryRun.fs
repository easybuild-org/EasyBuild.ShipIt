module EasyBuild.ShipIt.Generate.DryRun

open Spectre.Console
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Generate.Types

type STable = Spectre.Console.Table
type STableColumn = Spectre.Console.TableColumn

type TableColumn private () =

    static member column(name: string) = STableColumn(name)

    static member centered(col: STableColumn) =
        col.Centered() |> ignore
        col

type Table private () =

    static member empty = STable()

    static member withTitle (title: string) (table: STable) =
        table.Title <- TableTitle(title)
        table

    static member withColumn (input: obj) (table: STable) =
        match input with
        | :? string as name -> table.AddColumn(STableColumn(name)) |> ignore
        | :? STableColumn as col -> table.AddColumn(col) |> ignore
        | _ -> invalidArg "input" "Expected string or TableColumn"

        table

    static member withEmptyColumn(table: STable) =
        table.AddColumn(STableColumn("")) |> ignore
        table

    static member withRow (cells: string list) (table: STable) =
        table.AddRow(cells |> List.toArray) |> ignore
        table

    static member withRowSeparators(table: STable) =
        table.ShowRowSeparators() |> ignore
        table

    static member withHiddenHeaders(table: STable) =
        table.HideHeaders() |> ignore
        table

let private renderSummaryTable (releaseContexts: ReleaseContext list) (gitRepositoryRoot: string) =
    let addRows (table: STable) =
        releaseContexts
        |> List.iter (fun releaseContext ->
            let rows =
                match releaseContext with
                | NoVersionBumpRequired changelogInfo ->
                    let name = changelogInfo.NameOrDirectoryPath gitRepositoryRoot

                    [
                        name
                        "[green]✅[/]"
                        "-"
                    ]

                | BumpRequired bumpInfo ->
                    let name = bumpInfo.Changelog.NameOrDirectoryPath gitRepositoryRoot

                    [
                        name
                        "[green]🚀[/]"
                        bumpInfo.NewVersion.ToString()
                    ]

            Table.withRow rows table |> ignore
        )

        table

    Table.empty
    |> Table.withTitle "Summary of changes"
    |> Table.withColumn "Project"
    |> Table.withColumn (TableColumn.column "Status" |> TableColumn.centered)
    |> Table.withColumn (TableColumn.column "New Version" |> TableColumn.centered)
    |> Table.withRowSeparators
    |> addRows
    |> Log.output.Write

let private renderPullRequestSummary
    (remoteConfig: RemoteConfig)
    (releaseContexts: ReleaseContext list)
    (settings: Settings.SharedSettings)
    =

    let currentBranch = Git.getHeadBranchName ()

    let prContext =
        PullRequest.PullRequestContext(
            releaseContexts,
            settings.GitRepositoryRoot,
            remoteConfig,
            currentBranch
        )

    if prContext.ShouldCreate() then
        Table.empty
        |> Table.withTitle "Pull Request summary"
        |> Table.withHiddenHeaders
        |> Table.withRowSeparators
        |> Table.withColumn ""
        |> Table.withColumn ""
        |> Table.withRow (
            match prContext.BranchName with
            | Ok branchName ->
                [
                    "[bold]Branch[/]"
                    branchName
                ]
            | Error error ->
                [
                    "[bold]Branch[/]"
                    "[red]" + error + "[/]"
                ]
        )
        |> Table.withRow (
            match prContext.Title with
            | Ok title ->
                [
                    "[bold]Title[/]"
                    title
                ]
            | Error error ->
                [
                    "[bold]Title[/]"
                    "[red]" + error + "[/]"
                ]
        )
        |> Log.output.Write

let preview
    (remoteConfig: RemoteConfig)
    (releaseContexts: ReleaseContext list)
    (settings: Settings.SharedSettings)
    =
    let hasBumps =
        releaseContexts
        |> List.exists (
            function
            | BumpRequired _ -> true
            | _ -> false
        )

    Log.newLine ()

    if not hasBumps then
        Log.info "No changelog would be bumped, nothing to ship."
    else
        renderSummaryTable releaseContexts settings.GitRepositoryRoot

        renderPullRequestSummary remoteConfig releaseContexts settings
