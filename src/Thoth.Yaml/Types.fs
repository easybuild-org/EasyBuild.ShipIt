namespace Thoth.Yaml

open YamlDotNet.RepresentationModel

type ErrorReason =
    | BadPrimitive of string * YamlNode
    | BadPrimitiveExtra of string * YamlNode * string
    | BadType of string * YamlNode
    | BadField of string * YamlNode
    | BadPath of string * YamlNode * string
    | TooSmallArray of string * YamlNode
    | FailMessage of string
    | BadOneOf of DecoderError list

and DecoderError = string * ErrorReason

type Decoder<'T> = YamlNode -> Result<'T, DecoderError>

type Encoder<'T> = 'T -> YamlNode
