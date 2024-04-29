module EasyBuild.ShipIt.Types.Settings

open Spectre.Console.Cli
open System.ComponentModel

type VersionSettings() =
    inherit CommandSettings()

type SharedSettings() =

    inherit CommandSettings()

    [<CommandOption("-c|--config")>]
    [<Description("Path to the configuration file")>]
    member val Config: string option = None with get, set

    [<CommandOption("--remote-hostname <HOSTNAME>")>]
    [<Description("Git remote hostname, e.g. github.com, gitlab.com")>]
    member val RemoteHostname: string option = None with get, set

    [<CommandOption("--remote-owner <OWNER>")>]
    [<Description("Git remote owner or organization name")>]
    member val RemoteOwner: string option = None with get, set

    [<CommandOption("--remote-repo <REPO>")>]
    [<Description("Git remote repository name")>]
    member val RemoteRepo: string option = None with get, set

    [<CommandOption("--allow-branch <VALUES>")>]
    [<Description("List of branches that are allowed to be used to generate the changelog. Default is 'main'")>]
    member val AllowBranch: string array = [| "main" |] with get, set

    [<CommandOption("--skip-invalid-commit")>]
    [<Description("Skip invalid commits instead of failing")>]
    [<DefaultValue(true)>]
    member val SkipInvalidCommit: bool = true with get, set

    [<CommandOption("--skip-merge-commit")>]
    [<Description("Skip merge commits when generating the changelog (commit messages starting with 'Merge ')")>]
    [<DefaultValue(true)>]
    member val SkipMergeCommit: bool = true with get, set

    [<CommandOption("--skip-pull-request")>]
    [<Description("Skip creating a pull request updating the changelogs")>]
    [<DefaultValue(false)>]
    member val SkipPullRequest: bool = false with get, set

    member val Cwd = System.Environment.CurrentDirectory with get, set

    /// <summary>
    /// This is an internal setting used for testing purposes to force GitRepositoryRoot to a specific value.It is not intended to be set by users.
    ///
    /// It is not exposed as a command line option and should only be set programmatically in tests.
    /// </summary>
    /// <returns>
    /// Absolute path to the root of the git repository.
    /// </returns>
    member val GitRepositoryRoot: string = Git.getTopLevelDirectory () with get, set

type DefaultCommandSettings() =
    inherit SharedSettings()

    // When using a default command Spectre.Console.Cli does not support --version out of the box
    // We capture it in our own command and will handle it ourselves
    [<CommandOption("-v|--version")>]
    [<Description("Show version information")>]
    [<DefaultValue(false)>]
    member val ShowVersion: bool = false with get, set

type GithubCommandSettings() =
    inherit SharedSettings()

    [<CommandOption("--token <TOKEN>")>]
    [<Description("GitHub token to use for authentication. If not provided, it will use the GITHUB_TOKEN environment variable or the gh CLI authentication")>]
    member val Token: string option = None with get, set
