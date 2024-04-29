namespace Thoth.Yaml

open System.IO
open YamlDotNet.RepresentationModel
open YamlDotNet.Core
open YamlDotNet.Serialization

[<RequireQualifiedAccess>]
module Encode =

    let string (value: string) : YamlNode = YamlScalarNode(value)

    let int (value: int) : YamlNode = YamlScalarNode(value.ToString())

    let bool (value: bool) : YamlNode =
        YamlScalarNode(
            if value then
                "true"
            else
                "false"
        )

    let object (values: seq<string * YamlNode>) : YamlNode =
        let mapping = YamlMappingNode()

        for (key, value) in values do
            mapping.Add(YamlScalarNode(key), value)

        mapping

    let array (values: YamlNode seq) : YamlNode =
        let sequence = YamlSequenceNode()

        for value in values do
            sequence.Add(value)

        sequence

    let inline list (values: YamlNode list) : YamlNode = array (Seq.ofList values)

    let toString (node: YamlNode) : string =
        let serializer =
            SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .WithIndentedSequences()
                .Build()

        serializer.Serialize(node)

    let lossyOption (encoder: Encoder<'T>) (value: 'T option) : YamlNode =
        match value with
        | Some v -> encoder v
        | None -> YamlScalarNode("")

    module Object =

        let empty = []

        let addField
            (fieldName: string)
            (optionValue: 'T)
            (encoder: Encoder<'T>)
            (fields: (string * YamlNode) list)
            =
            fields @ [ (fieldName, encoder optionValue) ]

        let addFieldIfSome
            (fieldName: string)
            (optionValue: 'T option)
            (encoder: Encoder<'T>)
            (fields: (string * YamlNode) list)
            =
            match optionValue with
            | Some v -> fields @ [ (fieldName, encoder v) ]
            | None -> fields

        let addFieldIfNotEmpty
            (fieldName: string)
            (values: YamlNode list)
            (fields: (string * YamlNode) list)
            =
            if List.isEmpty values then
                fields
            else
                fields @ [ (fieldName, list values) ]
