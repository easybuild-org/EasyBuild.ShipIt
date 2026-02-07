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

    [<CommandOption("--allow-branch <VALUES>")>]
    [<Description("List of branches that are allowed to be used to generate the changelog")>]
    [<DefaultValue([| "main" |])>]
    member val AllowBranch: string array = [| "main" |] with get, set

    [<CommandOption("--mode <MODE>")>]
    [<DefaultValue("pull-request")>]
    [<Description("Mode of operation. Possible values are 'local', 'pull-request' and 'push'")>]
    member val Mode: string option = None with get, set

    [<CommandOption("--pre-release [PREFIX]")>]
    [<DefaultValue("beta")>]
    [<Description("Indicate that the generated version is a pre-release version. Optionally, you can provide a prefix for the beta version. Default is 'beta'")>]
    member val PreRelease: FlagValue<string> = FlagValue() with get, set

    [<CommandOption("--remote-hostname <HOSTNAME>")>]
    [<Description("Git remote hostname, e.g. github.com, gitlab.com")>]
    member val RemoteHostname: string option = None with get, set

    [<CommandOption("--remote-owner <OWNER>")>]
    [<Description("Git remote owner or organization name")>]
    member val RemoteOwner: string option = None with get, set

    [<CommandOption("--remote-repo <REPO>")>]
    [<Description("Git remote repository name")>]
    member val RemoteRepo: string option = None with get, set

    [<CommandOption("--skip-invalid-commit")>]
    [<Description("Skip invalid commits instead of failing")>]
    [<DefaultValue(false)>]
    member val SkipInvalidCommit: FlagValue<bool> = FlagValue() with get, set

    [<CommandOption("--skip-merge-commit")>]
    [<Description("Skip merge commits when generating the changelog (commit messages starting with 'Merge ')")>]
    [<DefaultValue(false)>]
    member val SkipMergeCommit: FlagValue<bool> = FlagValue() with get, set

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
