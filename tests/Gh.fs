module EasyBuild.ShipIt.Tests.Gh

open Thoth.Yaml
open TUnit.Core
open Tests.Utils

type GhTests() =

    // Disabled as people could have gh installed or not, making the test unreliable
    // Keeping the test in case we need to debug in the future
    [<Test>]
    member _.``isAvailable returns true when gh is installed``() =
        let cli = Gh.CLI()
        let available = cli.IsAvailable()

        Expect.isTrue available

    [<Test>]
    member _.``scopes returns non-empty list when gh is authenticated``() =
        let gh = Gh.CLI()
        let scopes = gh.auth.Scopes()

        Expect.isNonEmpty scopes
