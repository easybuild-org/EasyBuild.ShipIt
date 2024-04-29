module EasyBuild.ShipIt.Tests.Updater

open TUnit.Core
open Tests.Utils
open EasyBuild.ShipIt.Generate
open EasyBuild.ShipIt.Generate.Types
open System.IO
open Semver
open EasyBuild.CommitParser
open EasyBuild.CommitParser.Types
open FsToolkit.ErrorHandling
open Spectre.Console.Cli
open Workspace
open EasyBuild.ShipIt.Types
open EasyBuild.ShipIt.Types.Settings
open Workspace
open EasyBuild.ShipIt.Generate
open VerifyTUnit

[<Category "Updater">]
type PackageJsonUpdaterTests() =

    [<Test>]
    member _.``Works even if version is missing``() =
        task {
            let actual =
                Updater.PackageJson.replaceVersion
                    (SemVersion.Parse "1.2.3")
                    Workspace.``package.json-updater``.``empty.json``

            match actual with
            | Ok updatedContent -> return! verifyJson updatedContent
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Updates version correctly``() =
        task {
            let actual =
                Updater.PackageJson.replaceVersion
                    (SemVersion.Parse "2.3.4")
                    Workspace.``package.json-updater``.``with-version.json``

            match actual with
            | Ok updatedContent -> return! verifyJson updatedContent
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Try to preserve formatting``() =
        task {
            let actual =
                Updater.PackageJson.replaceVersion
                    (SemVersion.Parse "3.4.5")
                    Workspace.``package.json-updater``.``with-version-2-spaces-indented.json``

            match actual with
            | Ok updatedContent -> return! verifyJson updatedContent
            | Error e -> return failwith e
        }

[<Category "Updater">]
type RegexUpdaterTests() =

    [<Test>]
    member _.``Updates version correctly when negative lookbehind only is used``() =
        task {
            let actual =
                Updater.Regex.replaceVersion
                    (SemVersion.Parse "4.5.6")
                    "(?<=version: )(\"[0-9]+\.[0-9]+\.[0-9]+\")"
                    Workspace.``regex-updater``.``sample.txt``

            match actual with
            | Ok updatedContent -> return! Verifier.Verify updatedContent
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Updates version correctly when lookbehind and lookahead are used``() =
        task {
            let actual =
                Updater.Regex.replaceVersion
                    (SemVersion.Parse "5.6.7")
                    "(?<=<Version>).*(?=</Version>)"
                    Workspace.``regex-updater``.``example.xml``

            match actual with
            | Ok updatedContent -> return! Verifier.Verify updatedContent
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Returns error when pattern not found``() =
        let actual =
            Updater.Regex.replaceVersion
                (SemVersion.Parse "6.7.8")
                "NON_EXISTENT_PATTERN"
                Workspace.``regex-updater``.``sample.txt``

        Expect.equal
            actual
            (Error
                $"""Pattern not found in file.
File: %s{Workspace.``regex-updater``.``sample.txt``}
Regex: NON_EXISTENT_PATTERN""")

[<Category "Updater">]
type JsonUpdaterTests() =

    [<Test>]
    member _.``Updates version correctly``() =
        task {
            let actual =
                Updater.Json.replaceVersion
                    (SemVersion.Parse "7.8.9")
                    Workspace.``json-updater``.``missing-version.json``
                    "/metadata/version"

            match actual with
            | Ok updatedContent -> return! Verifier.Verify updatedContent
            | Error e -> return failwith e
        }

[<Category "Updater">]
type XmlUpdaterTests() =

    [<Test>]
    member _.``Updates version correctly``() =
        task {
            let actual =
                Updater.Xml.replaceVersion
                    (SemVersion.Parse "8.9.10")
                    Workspace.``xml-updater``.``example.xml``
                    "/Project/ItemGroup/Version"

            match actual with
            | Ok updatedContent -> return! Verifier.Verify updatedContent
            | Error e -> return failwith e
        }
