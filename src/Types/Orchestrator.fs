[<RequireQualifiedAccess>]
module EasyBuild.ShipIt.Types.Orchestrator

open EasyBuild.ShipIt.Types

module Label =

    [<Literal>]
    let PENDING = "easybuild-release:pending"

type OpenedReleasePR =
    {
        Url: string
        HeadRefName: string
        Number: int
    }

type IOrchestrator =
    abstract GetOpenedPullRequests: branchName: string -> OpenedReleasePR list
    abstract EnsureLabelsExist: unit -> unit
    abstract CreateReleasePullRequest: head: string -> title: string -> body: string -> unit
    abstract UpdateReleasePullRequest: prNumber: int -> title: string -> body: string -> unit
    /// <summary>
    /// Verifies that all requirements for the orchestrator are met.
    ///
    /// This is also used to setup any required configuration specific to the orchestrator,
    /// tokens, commit authors, etc...
    /// </summary>
    abstract VerifyAndSetupRequirements: mode: ReleaseMode -> Result<unit, string>

type IResolver =
    abstract GetOrchestrator: remoteConfig: RemoteConfig -> Result<IOrchestrator, string>
