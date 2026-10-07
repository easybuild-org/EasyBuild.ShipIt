module EasyBuild.ShipIt.Generate.Dependencies

open System.IO
open FsToolkit.ErrorHandling
open EasyBuild.ShipIt.Generate.Types

type DependencyGraph = (ChangelogInfo * ChangelogInfo list) list

let private resolveChangelogPath (changelog: ChangelogInfo) (dependency: string) =
    let fullPath =
        Path.GetFullPath(Path.Combine(changelog.File.DirectoryName, dependency))

    if Path.GetExtension(fullPath) = ".md" then
        fullPath
    else
        Path.Combine(fullPath, "CHANGELOG.md")

let resolve (changelogs: ChangelogInfo list) : Result<DependencyGraph, string> =
    let changelogsByPath =
        changelogs
        |> List.map (fun changelog -> changelog.File.FullName, changelog)
        |> Map.ofList

    changelogs
    |> List.traverseResultM (fun changelog ->
        changelog.Metadata.DependsOn
        |> List.traverseResultM (fun dependency ->
            let dependencyPath = resolveChangelogPath changelog dependency

            match Map.tryFind dependencyPath changelogsByPath with
            | Some dependencyChangelog -> Ok dependencyChangelog
            | None ->
                Error
                    $"Dependency '%s{dependency}' declared in '%s{changelog.File.FullName}' does not point to a known changelog. Expected to find '%s{dependencyPath}'."
        )
        |> Result.map (fun dependencies -> changelog, dependencies)
    )

let private comparePriority (a: ChangelogInfo) (b: ChangelogInfo) =
    // Lower number means higher priority, changelogs without priority go to the end
    match a.Metadata.Priority, b.Metadata.Priority with
    | Some a', Some b' -> compare a' b'
    | Some _, None -> -1
    | None, Some _ -> 1
    | None, None -> 0

/// Dependencies always come first, priority only orders changelogs whose dependencies are already placed.
let sort (graph: DependencyGraph) : Result<ChangelogInfo list, string> =
    let isSame (a: ChangelogInfo) (b: ChangelogInfo) = a.File.FullName = b.File.FullName

    let rec loop (sorted: ChangelogInfo list) (remaining: DependencyGraph) =
        match remaining with
        | [] -> Ok(List.rev sorted)
        | _ ->
            let isSorted (dependency: ChangelogInfo) =
                sorted |> List.exists (isSame dependency)

            match remaining |> List.tryFind (snd >> List.forall isSorted) with
            | Some(next, _) ->
                loop (next :: sorted) (remaining |> List.filter (fst >> isSame next >> not))
            | None ->
                let blockedChangelogs =
                    remaining
                    |> List.map (fun (changelog, _) -> $"- %s{changelog.File.FullName}")
                    |> String.concat "\n"

                Error
                    $"Circular 'depends_on' detected. The following changelogs are part of a cycle or depend on one:\n\n%s{blockedChangelogs}"

    graph |> List.sortWith (fun (a, _) (b, _) -> comparePriority a b) |> loop []
