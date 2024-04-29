namespace Thoth.Yaml

open System.IO
open YamlDotNet.RepresentationModel
open YamlDotNet.Core
open YamlDotNet.Serialization

[<RequireQualifiedAccess>]
module Decode =

    module Helpers =

        let anyToString (node: YamlNode) =
            let serializer = SerializerBuilder().Build()
            // Pass your root node (or object) directly
            serializer.Serialize(node)

        let prependPath (path: string) (err: DecoderError) : DecoderError =
            let (oldPath, reason) = err
            (path + oldPath, reason)

        let inline prependPathToResult<'T, 'JsonValue>
            (path: string)
            (res: Result<'T, DecoderError>)
            =
            res |> Result.mapError (prependPath path)

        let isNullValue (node: YamlNode) =
            match node.NodeType with
            | YamlNodeType.Scalar ->
                let scalar = node :?> YamlScalarNode

                scalar.Value = "null"
                || scalar.Value = "Null"
                || scalar.Value = "NULL"
                || scalar.Value = "~"
                || scalar.Value = ""
            | _ -> false

        let inline isObject (node: YamlNode) = node.NodeType = YamlNodeType.Mapping

        let tryGetProperty (fieldName: string) (mappingNode: YamlMappingNode) =
            let keyNode = YamlScalarNode(fieldName)

            match mappingNode.Children.TryGetValue(keyNode) with
            | true, valueNode -> Some valueNode
            | false, _ -> None

    let private genericMsg msg value newLine =
        try
            "Expecting "
            + msg
            + " but instead got:"
            + (if newLine then
                   "\n"
               else
                   " ")
            + (Helpers.anyToString value)
        with _ ->
            "Expecting "
            + msg
            + " but decoder failed. Couldn't report given value due to circular structure."
            + (if newLine then
                   "\n"
               else
                   " ")

    let rec private errorToString (path: string, error) =
        let reason =
            match error with
            | BadPrimitive(msg, value) -> genericMsg msg value true
            | BadType(msg, value) -> genericMsg msg value true
            | BadPrimitiveExtra(msg, value, reason) ->
                genericMsg msg value true + "\nReason: " + reason
            | BadField(msg, value) -> genericMsg $"a field '%s{msg}'" value true
            | BadPath(msg, value, fieldName) ->
                genericMsg msg value true + ("\nNode `" + fieldName + "` is unkown.")
            | TooSmallArray(msg, value) -> "Expecting " + msg + ".\n" + (Helpers.anyToString value)
            | BadOneOf errors ->
                let messages =
                    errors
                    |> List.map (fun error -> Helpers.prependPath path error |> errorToString)

                "The following errors were found:\n\n" + String.concat "\n" messages
            | FailMessage msg -> "The following `failure` occurred with the decoder: " + msg

        match error with
        | BadOneOf _ ->
            // Don't need to show the path here because each error case will show it's own path
            reason
        | _ -> "Error at: `" + path + "`\n" + reason

    let fromvalue (decoder: Decoder<'T>) =
        fun value ->
            match decoder value with
            | Ok success -> Ok success
            | Error error -> Error(errorToString error)

    let fromString (decoder: Decoder<_>) =
        fun value ->
            use reader = new StringReader(value)
            let stream = YamlStream()

            try
                stream.Load(reader)

                match decoder (stream.Documents.[0].RootNode) with
                | Ok success -> Ok success
                | Error error ->
                    let finalError = error |> Helpers.prependPath "$"
                    Error(errorToString finalError)

            with :? YamlException as ex ->
                Error("Given an invalid YAML: " + ex.Message)

    let unsafeFromString (decoder: Decoder<_>) =
        fun value ->
            match fromString decoder value with
            | Ok value -> value
            | Error errorMsg -> failwith errorMsg

    let string: Decoder<string> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Scalar ->
                let scalar = node :?> YamlScalarNode
                Ok scalar.Value
            | _ -> Error("", BadPrimitive("string", node))

    let int: Decoder<int> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Scalar ->
                let scalar = node :?> YamlScalarNode

                match System.Int32.TryParse(scalar.Value) with
                | true, value -> Ok value
                | false, _ -> Error("", BadPrimitiveExtra("int", node, "Could not parse integer"))
            | _ -> Error("", BadPrimitive("int", node))

    let float: Decoder<float> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Scalar ->
                let scalar = node :?> YamlScalarNode

                match System.Double.TryParse(scalar.Value) with
                | true, value -> Ok value
                | false, _ -> Error("", BadPrimitiveExtra("float", node, "Could not parse float"))
            | _ -> Error("", BadPrimitive("float", node))

    let bool: Decoder<bool> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Scalar ->
                let scalar = node :?> YamlScalarNode

                if scalar.Value = "true" || scalar.Value = "True" || scalar.Value = "TRUE" then
                    Ok true
                else if
                    scalar.Value = "false" || scalar.Value = "False" || scalar.Value = "FALSE"
                then
                    Ok false
                else
                    Error("", BadPrimitiveExtra("bool", node, "Could not parse boolean"))
            | _ -> Error("", BadPrimitive("bool", node))

    let oneOf (decoders: Decoder<'T> list) : Decoder<'T> =
        fun node ->
            let rec runner (decoders: Decoder<'value> list) (errors: DecoderError list) =
                match decoders with
                | headDecoder :: tail ->
                    match headDecoder node with
                    | Ok v -> Ok v
                    | Error error -> runner tail (List.append errors [ error ])
                | [] -> ("", BadOneOf errors) |> Error

            runner decoders []

    let field (fieldName: string) (decoder: Decoder<'T>) : Decoder<'T> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Mapping ->
                let mapping = node :?> YamlMappingNode
                let keyNode = YamlScalarNode(fieldName)

                match mapping.Children.TryGetValue(keyNode) with
                | true, valueNode ->
                    (decoder valueNode) |> Helpers.prependPathToResult ("." + fieldName)
                | false, _ -> Error("", BadField(fieldName, node))
            | _ -> Error("", BadType("an object", node))

    let private decodeMaybeNull (path: string) (decoder: Decoder<'value>) (value: YamlNode) =
        // The decoder may be an option decoder so give it an opportunity to check null values
        // We catch the null value case first to avoid executing the decoder logic
        // Indeed, if the decoder logic try to access the value to do something with it,
        // it can throw an exception about the value being null
        if Helpers.isNullValue value then
            Ok None
        else
            match decoder value with
            | Ok v -> Ok(Some v)
            | Error er -> er |> Helpers.prependPath path |> Error

    let optional (fieldName: string) (decoder: Decoder<'value>) : Decoder<'value option> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Mapping ->
                let mappingNode = node :?> YamlMappingNode

                match Helpers.tryGetProperty fieldName mappingNode with
                | Some fieldValue -> decodeMaybeNull ("." + fieldName) decoder fieldValue
                | None -> Ok None
            | _ -> Error("", BadType("an object", node))

    let optionalAt (fieldNames: string list) (decoder: Decoder<'value>) : Decoder<'value option> =
        fun firstNode ->
            (("", firstNode, None), fieldNames)
            ||> List.fold (fun (curPath, curValue, res) field ->
                match res with
                | Some _ -> curPath, curValue, res
                | None ->
                    if Helpers.isNullValue curValue then
                        curPath, curValue, Some(Ok None)
                    elif Helpers.isObject curValue then
                        match Helpers.tryGetProperty field (curValue :?> YamlMappingNode) with
                        | Some valueNode -> curPath + "." + field, valueNode, None

                        | None -> curPath, curValue, Some(Ok None)
                    else
                        let res = Error(curPath, BadType("an object", curValue))

                        curPath, curValue, Some res
            )
            |> function
                | _, _, Some res -> res
                | lastPath, lastValue, None ->
                    if Helpers.isNullValue lastValue then
                        Ok None
                    else
                        decodeMaybeNull lastPath decoder lastValue

    let private badPathError fieldNames currentPath value =
        let currentPath = defaultArg currentPath (fieldNames |> String.concat ".")

        let msg = "an object with path `" + (String.concat "." fieldNames) + "`"

        Error(
            "." + currentPath,
            BadPath(msg, value, List.tryLast fieldNames |> Option.defaultValue "")
        )

    let at (fieldNames: string list) (decoder: Decoder<'value>) : Decoder<'value> =
        fun firstNode ->
            (("", firstNode, None), fieldNames)
            ||> List.fold (fun (curPath, curValue, res) field ->
                match res with
                | Some _ -> curPath, curValue, res
                | None ->
                    if Helpers.isNullValue curValue then
                        let res = badPathError fieldNames (Some curPath) firstNode

                        curPath, curValue, Some res
                    elif curValue.NodeType = YamlNodeType.Mapping then
                        let mappingNode = curValue :?> YamlMappingNode

                        match Helpers.tryGetProperty field mappingNode with
                        | Some valueNode -> curPath + "." + field, valueNode, None
                        | None ->
                            let res = badPathError fieldNames None firstNode

                            curPath, curValue, Some res
                    else
                        let res = Error(curPath, BadType("an object", curValue))

                        curPath, curValue, Some res
            )
            |> function
                | _, _, Some res -> res
                | lastPath, lastValue, None ->
                    decoder lastValue |> Helpers.prependPathToResult lastPath

    let list (decoder: Decoder<'T>) : Decoder<'T list> =
        fun node ->
            match node.NodeType with
            | YamlNodeType.Sequence ->
                let sequenceNode = node :?> YamlSequenceNode
                let mutable i = 0
                let mutable result = []
                let mutable error: DecoderError option = None

                while i < sequenceNode.Children.Count && error.IsNone do
                    let value = sequenceNode.Children.[i]

                    match decoder value with
                    | Ok value -> result <- result @ [ value ]
                    | Error er ->
                        let x = Some(er |> Helpers.prependPath (".[" + (i.ToString()) + "]"))

                        error <- x

                    i <- i + 1

                if error.IsNone then
                    Ok result
                else
                    Error error.Value
            | _ -> Error("", BadPrimitive("a list", node))

    let map (ctor: 'A -> 'B) (decoder: Decoder<'A>) : Decoder<'B> =
        fun node ->
            match decoder node with
            | Ok value -> Ok(ctor value)
            | Error error -> Error error

    let map2 (ctor: 'A -> 'B -> 'C) (decoderA: Decoder<'A>) (decoderB: Decoder<'B>) : Decoder<'C> =
        fun node ->
            match decoderA node, decoderB node with
            | Ok v1, Ok v2 -> Ok(ctor v1 v2)
            | Error er, _ -> Error er
            | _, Error er -> Error er

    let map3
        (ctor: 'A -> 'B -> 'C -> 'D)
        (decoderA: Decoder<'A>)
        (decoderB: Decoder<'B>)
        (decoderC: Decoder<'C>)
        : Decoder<'D>
        =
        fun node ->
            match decoderA node, decoderB node, decoderC node with
            | Ok v1, Ok v2, Ok v3 -> Ok(ctor v1 v2 v3)
            | Error er, _, _ -> Error er
            | _, Error er, _ -> Error er
            | _, _, Error er -> Error er

    let map4
        (ctor: 'A -> 'B -> 'C -> 'D -> 'E)
        (decoderA: Decoder<'A>)
        (decoderB: Decoder<'B>)
        (decoderC: Decoder<'C>)
        (decoderD: Decoder<'D>)
        : Decoder<'E>
        =
        fun node ->
            match decoderA node, decoderB node, decoderC node, decoderD node with
            | Ok v1, Ok v2, Ok v3, Ok v4 -> Ok(ctor v1 v2 v3 v4)
            | Error er, _, _, _ -> Error er
            | _, Error er, _, _ -> Error er
            | _, _, Error er, _ -> Error er
            | _, _, _, Error er -> Error er

    let map5
        (ctor: 'A -> 'B -> 'C -> 'D -> 'E -> 'F)
        (decoderA: Decoder<'A>)
        (decoderB: Decoder<'B>)
        (decoderC: Decoder<'C>)
        (decoderD: Decoder<'D>)
        (decoderE: Decoder<'E>)
        : Decoder<'F>
        =
        fun node ->
            match decoderA node, decoderB node, decoderC node, decoderD node, decoderE node with
            | Ok v1, Ok v2, Ok v3, Ok v4, Ok v5 -> Ok(ctor v1 v2 v3 v4 v5)
            | Error er, _, _, _, _ -> Error er
            | _, Error er, _, _, _ -> Error er
            | _, _, Error er, _, _ -> Error er
            | _, _, _, Error er, _ -> Error er
            | _, _, _, _, Error er -> Error er

    let map6
        (ctor: 'A -> 'B -> 'C -> 'D -> 'E -> 'F -> 'G)
        (decoderA: Decoder<'A>)
        (decoderB: Decoder<'B>)
        (decoderC: Decoder<'C>)
        (decoderD: Decoder<'D>)
        (decoderE: Decoder<'E>)
        (decoderF: Decoder<'F>)
        : Decoder<'G>
        =
        fun node ->
            match
                decoderA node,
                decoderB node,
                decoderC node,
                decoderD node,
                decoderE node,
                decoderF node
            with
            | Ok v1, Ok v2, Ok v3, Ok v4, Ok v5, Ok v6 -> Ok(ctor v1 v2 v3 v4 v5 v6)
            | Error er, _, _, _, _, _ -> Error er
            | _, Error er, _, _, _, _ -> Error er
            | _, _, Error er, _, _, _ -> Error er
            | _, _, _, Error er, _, _ -> Error er
            | _, _, _, _, Error er, _ -> Error er
            | _, _, _, _, _, Error er -> Error er

    let map7
        (ctor: 'A -> 'B -> 'C -> 'D -> 'E -> 'F -> 'G -> 'H)
        (decoderA: Decoder<'A>)
        (decoderB: Decoder<'B>)
        (decoderC: Decoder<'C>)
        (decoderD: Decoder<'D>)
        (decoderE: Decoder<'E>)
        (decoderF: Decoder<'F>)
        (decoderG: Decoder<'G>)
        : Decoder<'H>
        =
        fun node ->
            match
                decoderA node,
                decoderB node,
                decoderC node,
                decoderD node,
                decoderE node,
                decoderF node,
                decoderG node
            with
            | Ok v1, Ok v2, Ok v3, Ok v4, Ok v5, Ok v6, Ok v7 -> Ok(ctor v1 v2 v3 v4 v5 v6 v7)
            | Error er, _, _, _, _, _, _ -> Error er
            | _, Error er, _, _, _, _, _ -> Error er
            | _, _, Error er, _, _, _, _ -> Error er
            | _, _, _, Error er, _, _, _ -> Error er
            | _, _, _, _, Error er, _, _ -> Error er
            | _, _, _, _, _, Error er, _ -> Error er
            | _, _, _, _, _, _, Error er -> Error er

    let map8
        (ctor: 'A -> 'B -> 'C -> 'D -> 'E -> 'F -> 'G -> 'H -> 'I)
        (decoderA: Decoder<'A>)
        (decoderB: Decoder<'B>)
        (decoderC: Decoder<'C>)
        (decoderD: Decoder<'D>)
        (decoderE: Decoder<'E>)
        (decoderF: Decoder<'F>)
        (decoderG: Decoder<'G>)
        (decoderH: Decoder<'H>)
        : Decoder<'I>
        =
        fun node ->
            match
                decoderA node,
                decoderB node,
                decoderC node,
                decoderD node,
                decoderE node,
                decoderF node,
                decoderG node,
                decoderH node
            with
            | Ok v1, Ok v2, Ok v3, Ok v4, Ok v5, Ok v6, Ok v7, Ok v8 ->
                Ok(ctor v1 v2 v3 v4 v5 v6 v7 v8)
            | Error er, _, _, _, _, _, _, _ -> Error er
            | _, Error er, _, _, _, _, _, _ -> Error er
            | _, _, Error er, _, _, _, _, _ -> Error er
            | _, _, _, Error er, _, _, _, _ -> Error er
            | _, _, _, _, Error er, _, _, _ -> Error er
            | _, _, _, _, _, Error er, _, _ -> Error er
            | _, _, _, _, _, _, Error er, _ -> Error er
            | _, _, _, _, _, _, _, Error er -> Error er

    let nil (value: 'T) : Decoder<'T> =
        fun node ->
            if Helpers.isNullValue node then
                Ok value
            else
                Error("", BadPrimitive("null", node))

    /// <summary>
    /// Decode a JSON null value into an F# option.
    ///
    /// Attention, this decoder is lossy, it will not be able to distinguish between `'T option` and `'T option option`.
    ///
    /// If you need to distinguish between `'T option` and `'T option option`, use `losslessOption`.
    /// </summary>
    /// <param name="decoder">
    /// The decoder to apply to the value if it is not null.
    /// </param>
    /// <typeparam name="'value">The type of the value to decode.</typeparam>
    /// <returns>
    /// <c>None</c> if the value is null, otherwise <c>Some value</c> where <c>value</c> is the result of the decoder.
    /// </returns>
    let lossyOption (decoder: Decoder<'T>) : Decoder<'T option> =
        fun node ->
            if Helpers.isNullValue node then
                Ok None
            else
                match decoder node with
                | Ok value -> Ok(Some value)
                | Error error -> Error error

    let value _ v = Ok v

    let succeed output = fun _ -> Ok output

    let fail msg : Decoder<'T> = fun _ -> Error("", FailMessage msg)

    let andThen (cb: 'A -> Decoder<'B>) (decoder: Decoder<'A>) : Decoder<'B> =
        fun node ->
            match decoder node with
            | Ok result -> (cb result) node
            | Error err -> Error err

    let all (decoders: Decoder<'T> list) : Decoder<'T list> =
        fun node ->
            let rec runner decoders values =
                match decoders with
                | decoder :: tail ->
                    match decoder node with
                    | Ok value -> runner tail (List.append values [ value ])
                    | Error error -> Error error
                | [] -> Ok values

            runner decoders []

    type IRequiredGetter =
        abstract Field: string -> Decoder<'a> -> 'a
        abstract At: List<string> -> Decoder<'a> -> 'a
        abstract Raw: Decoder<'a> -> 'a

    type IOptionalGetter =
        abstract Field: string -> Decoder<'a> -> 'a option
        abstract At: List<string> -> Decoder<'a> -> 'a option
        abstract Raw: Decoder<'a> -> 'a option

    type IGetters =
        abstract Required: IRequiredGetter
        abstract Optional: IOptionalGetter

    let private unwrapWith
        (errors: ResizeArray<DecoderError>)
        (decoder: Decoder<'T>)
        (value: 'JsonValue)
        : 'T
        =
        match decoder value with
        | Ok v -> v
        | Error er ->
            errors.Add(er)
            Unchecked.defaultof<'T>

    type Getters<'T>(value: YamlNode) =
        let mutable errors = ResizeArray<DecoderError>()

        let required =
            { new IRequiredGetter with
                member __.Field (fieldName: string) (decoder: Decoder<_>) =
                    unwrapWith errors (field fieldName decoder) value

                member __.At (fieldNames: string list) (decoder: Decoder<_>) =
                    unwrapWith errors (at fieldNames decoder) value

                member __.Raw(decoder: Decoder<_>) = unwrapWith errors decoder value
            }

        let optional =
            { new IOptionalGetter with

                member __.Field (fieldName: string) (decoder: Decoder<_>) =
                    unwrapWith errors (optional fieldName decoder) value

                member __.At (fieldNames: string list) (decoder: Decoder<_>) =
                    unwrapWith errors (optionalAt fieldNames decoder) value

                member __.Raw(decoder: Decoder<_>) =
                    match decoder value with
                    | Ok v -> Some v
                    | Error((_, reason) as error) ->
                        match reason with
                        | BadPrimitive(_, v)
                        | BadPrimitiveExtra(_, v, _)
                        | BadType(_, v) ->
                            if Helpers.isNullValue v then
                                None
                            else
                                errors.Add(error)
                                Unchecked.defaultof<_>
                        | BadField _
                        | BadPath _ -> None
                        | TooSmallArray _
                        | FailMessage _
                        | BadOneOf _ ->
                            errors.Add(error)
                            Unchecked.defaultof<_>
            }

        member __.Errors: DecoderError list = Seq.toList errors

        interface IGetters with
            member __.Required = required
            member __.Optional = optional

    let object (builder: IGetters -> 'value) : Decoder<'value> =
        fun node ->
            let getters = Getters(node)
            let result = builder getters

            match getters.Errors with
            | [] -> Ok result
            | fst :: _ as errors ->
                if errors.Length > 1 then
                    ("", BadOneOf errors) |> Error
                else
                    Error fst
