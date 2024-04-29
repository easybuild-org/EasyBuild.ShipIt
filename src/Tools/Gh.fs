[<RequireQualifiedAccess>]
module Gh

open SimpleExec
open BlackFox.CommandLine
open System.Threading.Tasks

type AuthCommand() =

    member _.Scopes() =
        let struct (standtardOutput, _) =
            Command.ReadAsync(
                "gh",
                CmdLine.empty
                |> CmdLine.appendRaw "auth"
                |> CmdLine.appendRaw "status"
                |> CmdLine.appendPrefix "--json" "hosts"
                |> CmdLine.appendPrefix "--jq" ".hosts | add | add | .scopes"
                |> CmdLine.toString
            )
            |> Task.RunSynchronously

        standtardOutput |> String.splitBy ','

module PR =

    module List =

        [<RequireQualifiedAccess>]
        type State =
            | Open
            | Closed
            | Merged
            | All

            member this.AsText =
                match this with
                | Open -> "open"
                | Closed -> "closed"
                | Merged -> "merged"
                | All -> "all"

        [<RequireQualifiedAccess>]
        type Fields =
            | Additions
            | Assignees
            | Author
            | AutoMergeRequest
            | BaseRefName
            | BaseRefOid
            | Body
            | ChangedFiles
            | Closed
            | ClosedAt
            | ClosingIssuesReferences
            | Comments
            | Commits
            | CreatedAt
            | Deletions
            | Files
            | FullDatabaseId
            | HeadRefName
            | HeadRefOid
            | HeadRepository
            | HeadRepositoryOwner
            | Id
            | IsCrossRepository
            | IsDraft
            | Labels
            | LatestReviews
            | MaintainerCanModify
            | MergeCommit
            | MergeStateStatus
            | Mergeable
            | MergedAt
            | MergedBy
            | Milestone
            | Number
            | PotentialMergeCommit
            | ProjectCards
            | ProjectItems
            | ReactionGroups
            | ReviewDecision
            | ReviewRequests
            | Reviews
            | State
            | StatusCheckRollup
            | Title
            | UpdatedAt
            | Url

            member this.AsText =
                match this with
                | Additions -> "additions"
                | Assignees -> "assignees"
                | Author -> "author"
                | AutoMergeRequest -> "autoMergeRequest"
                | BaseRefName -> "baseRefName"
                | BaseRefOid -> "baseRefOid"
                | Body -> "body"
                | ChangedFiles -> "changedFiles"
                | Closed -> "closed"
                | ClosedAt -> "closedAt"
                | ClosingIssuesReferences -> "closingIssuesReferences"
                | Comments -> "comments"
                | Commits -> "commits"
                | CreatedAt -> "createdAt"
                | Deletions -> "deletions"
                | Files -> "files"
                | FullDatabaseId -> "fullDatabaseId"
                | HeadRefName -> "headRefName"
                | HeadRefOid -> "headRefOid"
                | HeadRepository -> "headRepository"
                | HeadRepositoryOwner -> "headRepositoryOwner"
                | Id -> "id"
                | IsCrossRepository -> "isCrossRepository"
                | IsDraft -> "isDraft"
                | Labels -> "labels"
                | LatestReviews -> "latestReviews"
                | MaintainerCanModify -> "maintainerCanModify"
                | MergeCommit -> "mergeCommit"
                | MergeStateStatus -> "mergeStateStatus"
                | Mergeable -> "mergeable"
                | MergedAt -> "mergedAt"
                | MergedBy -> "mergedBy"
                | Milestone -> "milestone"
                | Number -> "number"
                | PotentialMergeCommit -> "potentialMergeCommit"
                | ProjectCards -> "projectCards"
                | ProjectItems -> "projectItems"
                | ReactionGroups -> "reactionGroups"
                | ReviewDecision -> "reviewDecision"
                | ReviewRequests -> "reviewRequests"
                | Reviews -> "reviews"
                | State -> "state"
                | StatusCheckRollup -> "statusCheckRollup"
                | Title -> "title"
                | UpdatedAt -> "updatedAt"
                | Url -> "url"

type PRCommand() =

    member _.Create
        (?base_: string, ?head: string, ?title: string, ?body: string, ?labels: string list)
        =
        Command.RunAsync(
            "gh",
            CmdLine.empty
            |> CmdLine.appendRaw "pr"
            |> CmdLine.appendRaw "create"
            |> CmdLine.appendPrefixIfSome "--base" base_
            |> CmdLine.appendPrefixIfSome "--head" head
            |> CmdLine.appendPrefixIfSome "--title" title
            |> CmdLine.appendPrefixIfSome "--body" body
            |> CmdLine.appendPrefixSeqIfSome "--label" labels
            |> CmdLine.toString,
            noEcho = true
        )
        |> Task.RunSynchronously

    member _.Update(prNumber: int, ?title: string, ?body: string) =
        Command.RunAsync(
            "gh",
            CmdLine.empty
            |> CmdLine.appendRaw "pr"
            |> CmdLine.appendRaw "edit"
            |> CmdLine.appendRaw (prNumber.ToString())
            |> CmdLine.appendPrefixIfSome "--title" title
            |> CmdLine.appendPrefixIfSome "--body" body
            |> CmdLine.toString,
            noEcho = true
        )
        |> Task.RunSynchronously

    member _.List(?label: string, ?state: PR.List.State, ?jsonFields: PR.List.Fields list) =
        let state = state |> Option.map _.AsText
        let jsonFields = jsonFields |> Option.map (List.map _.AsText >> String.concat ",")

        let struct (standardOutput, _) =
            Command.ReadAsync(
                "gh",
                CmdLine.empty
                |> CmdLine.appendRaw "pr"
                |> CmdLine.appendRaw "list"
                |> CmdLine.appendPrefixIfSome "--label" label
                |> CmdLine.appendPrefixIfSome "--state" state
                |> CmdLine.appendPrefixIfSome "--json" jsonFields
                |> CmdLine.toString
            )
            |> Task.RunSynchronously

        standardOutput

module Label =

    module List =

        [<RequireQualifiedAccess>]
        type Fields =
            | Color
            | CreatedAt
            | Description
            | Id
            | IsDefault
            | Name
            | UpdatedAt
            | Url

            member this.AsText =
                match this with
                | Color -> "color"
                | CreatedAt -> "createdAt"
                | Description -> "description"
                | Id -> "id"
                | IsDefault -> "isDefault"
                | Name -> "name"
                | UpdatedAt -> "updatedAt"
                | Url -> "url"

type LabelCommand() =

    member _.List(?jsonFields: Label.List.Fields list) =
        let jsonFields = jsonFields |> Option.map (List.map _.AsText >> String.concat ",")

        let struct (standardOutput, _) =
            Command.ReadAsync(
                "gh",
                CmdLine.empty
                |> CmdLine.appendRaw "label"
                |> CmdLine.appendRaw "list"
                |> CmdLine.appendPrefixIfSome "--json" jsonFields
                |> CmdLine.toString
            )
            |> Task.RunSynchronously

        standardOutput

    member _.Create(name: string, ?color: string, ?description: string) =
        Command.RunAsync(
            "gh",
            CmdLine.empty
            |> CmdLine.appendRaw "label"
            |> CmdLine.appendPrefix "create" name
            |> CmdLine.appendPrefixIfSome "--color" color
            |> CmdLine.appendPrefixIfSome "--description" description
            |> CmdLine.toString
        )
        |> Task.RunSynchronously

type CLI() =

    member _.IsAvailable() =
        try
            let _ = Command.ReadAsync("gh", "--version") |> Task.RunSynchronously

            true
        with _ ->
            false

    member _.auth = AuthCommand()
    member _.pr = PRCommand()
    member _.label = LabelCommand()

// 1. git push origin HEAD:release/1.0.0 --force
// 2. gh create pr --base main --head release/1.0.0 --title "Release 1.0.0" --body "Automated release PR"
