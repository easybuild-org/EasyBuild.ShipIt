module EasyBuild.ShipIt.Tests.JsonPatch

open TUnit.Core
open System.Text.Json
open System.Text.Json.Nodes
open Tests.Utils
open System.Text.Encodings.Web
open JsonPatch
open JsonPointer

let private serializerOptions =
    JsonSerializerOptions(
        WriteIndented = true,
        NewLine = "\n",
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    )

type JsonPatchTests() =

    [<Test>]
    member _.``Apply Add operation to object``() =
        task {
            let originalJson = """{ "name": "Sample", "version": "1.0.0" }""" |> JsonNode.Parse

            let patch =
                [ PatchOperation.Add(JsonPointer.create "/description", JsonValue.Create "42") ]

            let actual = JsonPatch.patchWithSerializer originalJson patch serializerOptions

            match actual with
            | Ok updatedJson -> return! verifyJson updatedJson
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Apply Replace operation to object``() =
        task {
            let originalJson = """{ "name": "Sample", "version": "1.0.0" }""" |> JsonNode.Parse

            let patch =
                [ PatchOperation.Replace(JsonPointer.create "/version", JsonValue.Create "2.0.0") ]

            let actual = JsonPatch.patchWithSerializer originalJson patch serializerOptions

            match actual with
            | Ok updatedJson -> return! verifyJson updatedJson
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Apply multiple operations``() =
        task {
            let originalJson = """{ "name": "Sample", "version": "1.0.0" }""" |> JsonNode.Parse

            let patch =
                [
                    PatchOperation.Add(
                        JsonPointer.create "/description",
                        JsonValue.Create "A sample project"
                    )
                    PatchOperation.Replace(JsonPointer.create "/version", JsonValue.Create "2.0.0")
                ]

            let actual = JsonPatch.patchWithSerializer originalJson patch serializerOptions

            match actual with
            | Ok updatedJson -> return! verifyJson updatedJson
            | Error e -> return failwith e
        }

    [<Test>]
    member _.``Operation can be applied to the same pointer``() =
        task {
            let originalJson = """{ "count": 1 }""" |> JsonNode.Parse

            let patch =
                [
                    PatchOperation.Replace(JsonPointer.create "/count", JsonValue.Create 2)
                    PatchOperation.Replace(JsonPointer.create "/count", JsonValue.Create 3)
                ]

            let actual = JsonPatch.patchWithSerializer originalJson patch serializerOptions

            match actual with
            | Ok updatedJson -> return! verifyJson updatedJson
            | Error e -> return failwith e
        }
