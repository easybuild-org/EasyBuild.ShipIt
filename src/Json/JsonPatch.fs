module JsonPatch

(*
    Simple implementation of JSON Patch (RFC 6902) for our specific use case.

    See https://jsonpatch.com/

    Only a subset of the operations are implemented:
        - Add
        - Replace

    This is sufficient for our needs in updating JSON files like package.json.
*)

open System.Text.Json
open System.Text.Json.Nodes
open JsonPointer

/// Represents a single JSON Patch operation
[<RequireQualifiedAccess>]
type PatchOperation =
    | Add of path: JsonPointer * value: JsonNode
    | Replace of path: JsonPointer * value: JsonNode

/// Parsed result of a JSON path: The Parent Node and the Key/Index to modify on it
type private PathTarget =
    | ObjectKey of parent: JsonObject * key: string
    | ArrayIndex of parent: JsonArray * index: int
    | ArrayAppend of parent: JsonArray // specific for path ending in "/-"

module JsonPatch =

    let private navigateToTarget (root: JsonNode) (path: JsonPointer) : Result<PathTarget, string> =
        // 1. Split path (handling the empty root case)
        let segments = JsonPointer.split path

        if List.isEmpty segments then
            Error "Cannot patch the root node directly with this helper."
        else
            let rec findParent (currentNode: JsonNode) (currentPath: string list) =
                match currentPath with
                | [] -> Error "Path traversal error."

                // We have reached the final segment (the target key/index)
                | [ lastSegment ] ->
                    if currentNode :? JsonObject then
                        Ok(ObjectKey(currentNode.AsObject(), lastSegment))
                    elif currentNode :? JsonArray then
                        let arr = currentNode.AsArray()

                        if lastSegment = "-" then
                            Ok(ArrayAppend arr)
                        else
                            match JsonPointer.Advanced.tryParseArrayIndex lastSegment with
                            | Some idx -> Ok(ArrayIndex(arr, idx))
                            | None -> Error $"Invalid array index: '{lastSegment}'"
                    else
                        Error $"Cannot patch primitive value at this depth."

                // We are still navigating down (intermediate segments)
                | nextSegment :: remaining ->
                    if currentNode :? JsonObject then
                        let obj = currentNode.AsObject()

                        if obj.ContainsKey(nextSegment) then
                            findParent obj[nextSegment] remaining
                        else
                            Error $"Target path `{path}` could not be reached"
                    elif currentNode :? JsonArray then
                        let arr = currentNode.AsArray()

                        match JsonPointer.Advanced.tryParseArrayIndex nextSegment with
                        | Some idx ->
                            if idx >= 0 && idx < arr.Count then
                                findParent arr[idx] remaining
                            else
                                Error $"Array index '{idx}' out of bounds."
                        | None -> Error $"Invalid array index: '{nextSegment}'"
                    else
                        Error "Path traverses through a primitive value."

            findParent root segments

    let applyOp (root: JsonNode) (operation: PatchOperation) : Result<JsonNode, string> =
        match operation with
        | PatchOperation.Add(path, value) ->
            match navigateToTarget root path with
            | Ok target ->
                match target with
                | ObjectKey(parent, key) ->
                    parent[key] <- value
                    Ok root
                | ArrayIndex(parent, index) ->
                    if index >= 0 && index <= parent.Count then
                        parent.Insert(index, value)
                        Ok root
                    else
                        Error $"Array index '{index}' out of bounds for Add operation."
                | ArrayAppend parent ->
                    parent.Add(value)
                    Ok root
            | Error e -> Error e

        | PatchOperation.Replace(path, value) ->
            match navigateToTarget root path with
            | Ok target ->
                match target with
                | ObjectKey(parent, key) ->
                    if parent.ContainsKey(key) then
                        parent[key] <- value
                        Ok root
                    else
                        Error $"Key '{key}' does not exist for Replace operation."
                | ArrayIndex(parent, index) ->
                    if index >= 0 && index < parent.Count then
                        parent[index] <- value
                        Ok root
                    else
                        Error $"Array index '{index}' out of bounds for Replace operation."
                | ArrayAppend _ -> Error "Cannot use Replace operation with array append path."
            | Error e -> Error e

    let patch (json: JsonNode) (operations: PatchOperation list) : Result<JsonNode, string> =
        let folderFunc (acc: Result<JsonNode, string>) (op: PatchOperation) =
            match acc with
            | Ok currentJson -> applyOp currentJson op
            | Error e -> Error e

        List.fold folderFunc (Ok json) operations

    let patchWithDefaultSerializer
        (json: JsonNode)
        (operations: PatchOperation list)
        : Result<string, string>
        =
        let patchedResult = patch json operations

        match patchedResult with
        | Ok updatedJson -> JsonSerializer.Serialize(updatedJson) |> Ok
        | Error e -> Error e

    let patchWithSerializer
        (json: JsonNode)
        (operations: PatchOperation list)
        (options: JsonSerializerOptions)
        : Result<string, string>
        =
        let patchedResult = patch json operations

        match patchedResult with
        | Ok updatedJson -> JsonSerializer.Serialize(updatedJson, options) |> Ok
        | Error e -> Error e
