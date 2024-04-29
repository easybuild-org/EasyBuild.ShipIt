[<RequireQualifiedAccess>]
module EasyBuild.ShipIt.Types.Orchestrator

open EasyBuild.ShipIt.Types

module Label =

    [<Literal>]
    let PENDING = "easybuild-release:pending"

    [<Literal>]
    let PUBLISHED = "easybuild-release:published"

type OpenedReleasePR =
    {
        Url: string
        HeadRefName: string
        Number: int
    }

type IOrchestrator =
    abstract IsAvailable: unit -> bool

    abstract GetOpenedPullRequests: branchName: string -> OpenedReleasePR list
    abstract EnsureLabelsExist: unit -> unit
    abstract CreateReleasePullRequest: head: string -> title: string -> body: string -> unit
    abstract UpdateReleasePullRequest: prNumber: int -> title: string -> body: string -> unit

type IResolver =
    abstract GetOrchestrator: remoteConfig: RemoteConfig -> Result<IOrchestrator, string>
