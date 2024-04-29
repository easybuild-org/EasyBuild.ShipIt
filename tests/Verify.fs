module EasyBuild.ShipIt.Tests.Verify

open Thoth.Yaml
open TUnit.Core
open Tests.Utils
open EasyBuild.ShipIt.Generate
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Types.Settings

type VerifyTests() =

    [<Test>]
    member _.``Verify.resolveRemoteConfig priorize CLI arguments``() =
        let actual =
            Verify.resolveRemoteConfig (
                DefaultCommandSettings(
                    RemoteHostname = Some "gitlab.com",
                    RemoteOwner = Some "owner",
                    RemoteRepo = Some "repo"
                )
            )

        let expected =
            Ok(
                {
                    Hostname = "gitlab.com"
                    Owner = "owner"
                    Repository = "repo"
                }
                : RemoteConfig
            )

        Expect.equal expected actual

    [<Test>]
    member _.``returns an error if not all `--remote-XXX` CLI arguments are provided``() =
        // Test some combinations of missing arguments
        [
            Verify.resolveRemoteConfig (
                DefaultCommandSettings(
                    RemoteHostname = Some "gitlab.com",
                    RemoteOwner = Some "owner"
                )
            )

            Verify.resolveRemoteConfig (
                DefaultCommandSettings(RemoteHostname = Some "gitlab.com", RemoteRepo = Some "repo")
            )

            Verify.resolveRemoteConfig (DefaultCommandSettings(RemoteOwner = Some "owner"))
        ]
        |> List.iter (fun actual ->
            let expected =
                Error
                    """When using --remote-hostname, --remote-owner and --remote-repo they must be all provided."""

            Expect.equal expected actual
        )
