module EasyBuild.ShipIt.Tests.GlobalSetup

open System
open TUnit.Core
open VerifyTests
open VerifyTUnit
open Workspace
open Argon

type GlobalHooks() =
    [<Before(HookType.TestSession)>]
    static member SetUp() =
        Verifier.UseProjectRelativeDirectory(Workspace.``..``.VerifyTests.``.``)

        VerifierSettings.AddExtraSettings(fun settings ->
            settings.AddFSharpConverters()
            settings.DefaultValueHandling <- DefaultValueHandling.Include
        )

type VerifyCheckTests() =

    [<Test>]
    member _.``check that Verify conventions are setup correctly``() =
        // https://github.com/VerifyTests/Verify#conventions-check
        VerifyChecks.Run()
