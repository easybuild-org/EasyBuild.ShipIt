module EasyBuild.ShipIt.Generate.Updater

open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Encodings.Web
open System.IO
open Semver

module Regex =
    open System.Text.RegularExpressions

    let replaceVersion (newVersion: SemVersion) (regexPattern: string) (filePath: string) =
        let rawText = File.ReadAllText(filePath)
        let regex = Regex(regexPattern, RegexOptions.Multiline)

        if regex.IsMatch(rawText) then
            let newContent = regex.Replace(rawText, newVersion.ToString())

            Ok newContent

        else
            Error
                $"""Pattern not found in file.
File: %s{filePath}
Regex: %s{regexPattern}"""

module Json =

    open JsonPatch
    open JsonPointer

    let private ensureFieldsExist (pointer: JsonPointer.JsonPointer) =

        let rec apply
            (pathSegments: string list)
            (currentPath: string)
            (addOperations: JsonPatch.PatchOperation list)
            =

            match pathSegments with
            | [] -> addOperations
            | segment :: remaining ->
                let newPath =
                    if currentPath = "" then
                        "/" + segment
                    else
                        currentPath + "/" + segment

                // Determine the default value to add at this path
                // It can either be an object or an array depending on the next segment
                let defaultValue =
                    match remaining with
                    | [] -> JsonObject() :> JsonNode
                    | segment :: _ ->
                        match JsonPointer.Advanced.tryParseArrayIndex segment with
                        | Some _ -> JsonArray() :> JsonNode
                        | None -> JsonObject() :> JsonNode

                let newAddOp = PatchOperation.Add(JsonPointer.create newPath, defaultValue)

                apply remaining newPath (addOperations @ [ newAddOp ])

        let segments = JsonPointer.split pointer
        apply segments "" []

    let replaceVersion (newVersion: SemVersion) (filePath: string) (jsonPath: string) =
        let rawText = File.ReadAllText(filePath)

        let json =
            JsonNode.Parse(
                rawText,
                // Make the parser more lenient
                documentOptions =
                    JsonDocumentOptions(
                        AllowTrailingCommas = true,
                        AllowDuplicateProperties = true,
                        // Can't allow comments ¯\_(ツ)_/¯
                        CommentHandling = JsonCommentHandling.Skip
                    )
            )

        let rec takeFirstIndentedLine (lines: string list) =
            match lines with
            | [] -> None
            | line :: rest ->
                let trimmed = line.TrimStart()

                if trimmed.StartsWith("{") then
                    takeFirstIndentedLine rest
                else
                    let indentation = line.Length - trimmed.Length

                    if indentation > 0 then
                        Some(indentation)
                    else
                        takeFirstIndentedLine rest

        // We try to preserve the original indentation of the file
        let indentSize =
            rawText
            |> String.normalizeNewLines
            |> String.splitBy '\n'
            |> takeFirstIndentedLine
            |> Option.defaultValue 4

        let versionPointer = JsonPointer.create jsonPath

        let patch =
            [
                yield! ensureFieldsExist versionPointer
                PatchOperation.Replace(
                    JsonPointer.create jsonPath,
                    JsonValue.Create(newVersion.ToString())
                )
            ]

        let serializerOptions =
            JsonSerializerOptions(
                WriteIndented = true,
                NewLine = "\n",
                IndentSize = indentSize,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            )

        match JsonPatch.patchWithSerializer json patch serializerOptions with
        | Ok pathResult -> Ok pathResult
        | Error error ->
            Error
                $"""Error when upading JSON file.

File: $s{filePath}
JsonPath: {jsonPath}
Error: {error}"""

module PackageJson =

    let replaceVersion (newVersion: SemVersion) (filePath: string) =
        Json.replaceVersion newVersion filePath "/version"

module Xml =

    open System.Xml

    let replaceVersion (newVersion: SemVersion) (filePath: string) (xpath: string) =
        let rawText = File.ReadAllText(filePath)
        let xmlDoc = XmlDocument()
        xmlDoc.LoadXml(rawText)

        let namespaceManager = XmlNamespaceManager(xmlDoc.NameTable)
        // Add any necessary namespaces to the manager here if needed

        let node = xmlDoc.SelectSingleNode(xpath, namespaceManager)

        if isNull node then
            Error
                $"""XPath not found in file.
File: %s{filePath}
XPath: %s{xpath}"""
        else
            node.InnerText <- newVersion.ToString()
            use stringWriter = new StringWriter()
            use xmlTextWriter = new XmlTextWriter(stringWriter)
            xmlTextWriter.Formatting <- Formatting.Indented
            xmlDoc.WriteTo(xmlTextWriter)
            xmlTextWriter.Flush()
            let updatedContent = stringWriter.GetStringBuilder().ToString()
            Ok updatedContent

module Command =

    open SimpleExec

    let run (cwd: string) (command: string) (newVersion: SemVersion) =
        let programName = command.Split(' ') |> Array.head

        let args =
            command.Split(' ')
            |> Array.skip 1
            |> String.concat " "
            |> fun s -> s.Replace("{version}", newVersion.ToString())

        try
            Command.Run(programName, args, workingDirectory = cwd)
            Ok()
        with ex ->
            Error
                $"""Error when running command.

Command: {command}
Error: {ex.Message}"""
