module Tests.Utils

open VerifyTUnit
open System.IO
open EasyBuild.CommitParser
open EasyBuild.CommitParser.Types
open EasyBuild.ShipIt.Generate.Types

let inline verifyYaml (yaml: string) = Verifier.Verify(yaml, "yml")

let inline verifyMarkdown (markdown: string) = Verifier.Verify(markdown, "md")

let inline verifyJson (json: string) = Verifier.Verify(json, "json")

let inline verifyXml (xml: string) = Verifier.Verify(xml, "xml")

/// <summary>
/// A utility class that restores the original content of a file upon disposal.
/// </summary>
type RestorableFile(filePath: string) =

    let originalContent = File.ReadAllText(filePath)

    interface System.IDisposable with
        member _.Dispose() =
            File.WriteAllText(filePath, originalContent)

open Expecto

module Expect =

    let equal actual expected = Expect.equal actual expected ""

    let notEqual actual expected = Expect.notEqual actual expected ""

    let isNotEmpty actual = Expect.isNotEmpty actual ""

    let isNonEmpty actual = Expect.isNonEmpty actual ""

    let isOk actual = Expect.isOk actual ""

    let isError actual = Expect.isError actual ""

    let isTrue actual = Expect.isTrue actual ""

    let isFalse actual = Expect.isFalse actual ""

    let throws f = Expect.throws f ""

    let isNone actual = Expect.isNone actual ""

    let isSome actual = Expect.isSome actual ""

type Result<'T, 'TError> with
    member this.UnsafeOkValue =
        match this with
        | Ok v -> v
        | Error e -> failwithf "Expected Ok but got Error: %A" e

type Git.Commit with

    /// <summary>
    /// Creates a Git.Commit instance for testing purposes.
    ///
    /// <remarks>
    /// By default, a <c>src/Main.fs</c> file is included in the commit,
    /// the author is set to "Kaladin Stormblessed", and the long message
    /// is set to the short message followed by a newline.
    /// </remarks>
    /// </summary>
    /// <param name="hash">Hash of the commit.</param>
    /// <param name="shortMessage">Short message of the commit.</param>
    /// <param name="longMessage">Optional long message of the commit.</param>
    /// <param name="author">Optional author of the commit.</param>
    /// <param name="files">Optional list of files changed in the commit.</param>
    /// <returns>
    /// A Git.Commit instance with the specified properties.
    /// </returns>
    static member Create
        (
            hash: string,
            shortMessage: string,
            ?longMessage: string,
            ?author: string,
            ?files: string list
        )
        : Git.Commit
        =
        let author = defaultArg author "Kaladin Stormblessed"
        let longMessage = defaultArg longMessage (shortMessage + "\n")
        let files = defaultArg files [ "src/Main.fs" ]

        {
            Hash = hash
            AbbrevHash = hash.Substring(0, 7)
            Author = author
            ShortMessage = shortMessage
            RawBody = longMessage
            Files = files
        }

let gitCommitToCommitForRelease (commit: Git.Commit) : CommitForRelease =
    {
        OriginalCommit = commit
        SemanticCommit =
            Parser.tryParseCommitMessage CommitParserConfig.Default commit.RawBody
            |> function
                | Ok semanticCommit -> semanticCommit
                | Error error -> failwith error
    }

// open VerifyTests
// open VerifyExpecto
// open DiffEngine
// open Workspace
// open System.Runtime.CompilerServices
// open System.Runtime.InteropServices

// let mutable diffToolRegistered = false

// let registerDiffTool () =
//     if not diffToolRegistered then
//         let launchArguments =
//             LaunchArguments(
//                 Left = fun temp target -> $"-n --diff \"{target}\" \"{temp}\""
//                 , Right = fun temp target -> $"-n --diff \"{temp}\" \"{target}\""
//             )

//         DiffTools.AddToolBasedOn(
//             DiffTool.VisualStudioCode,
//             "VisualStudioCodeNewWindow",
//             launchArguments = launchArguments
//         )
//         |> ignore

//         diffToolRegistered <- true

// let inline verify (name: string) (value: string) =
//     // registerDiffTool()

//     let settings = VerifySettings()
//     settings.UseDirectory(Workspace.``..``.VerifyTests.``.``)
//     Verifier.Verify(name, value, settings).ToTask()

// type Verify =

//     static member Markdown
//         (
//             name: string,
//             value: string,
//             [<CallerFilePath; Optional; DefaultParameterValue("")>] callerFilePath: string
//         )

//         =
//         let settings = VerifySettings()
//         settings.UseDirectory(Workspace.``..``.VerifyTests.``.``)
//         registerDiffTool ()

//         Verifier.Verify(name, value, "md", settings, callerFilePath).ToTask()

// type TestHelper =
//     static member testMarkdown
//         (
//             name: string,
//             func: unit -> string,
//             [<CallerFilePath; Optional; DefaultParameterValue("")>] callerFilePath: string
//         )
//         =
//         testTask name { do! Verify.Markdown(name, func (), callerFilePath) }

//     static member ftestMarkdown
//         (
//             name: string,
//             func: unit -> string,
//             [<CallerFilePath; Optional; DefaultParameterValue("")>] callerFilePath: string
//         )
//         =
//         ftestTask name { do! Verify.Markdown(name, func (), callerFilePath) }
