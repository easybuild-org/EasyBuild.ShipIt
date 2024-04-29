namespace EasyBuild.ShipIt.Types

open Thoth.Yaml
open Semver
[<RequireQualifiedAccess>]
type RemoteHost =
    | GitHub
    | Unknown

type RemoteConfig =
    {
        Hostname: string
        Owner: string
        Repository: string
    }

    /// <summary>
    /// Returns the baseUrl for the remote in the formet of <c>https://hostname/owner/repository</c>
    /// </summary>
    /// <returns>Base URL</returns>
    member this.BaseUrl = $"https://%s{this.Hostname}/%s{this.Owner}/%s{this.Repository}"

    /// <summary>
    /// Returns the name of the remote based on the hostname.
    ///
    /// E.g. for <c>github.com</c> it returns <c>github</c>
    /// </summary>
    /// <returns></returns>
    member this.NameOnly =
        this.Hostname
        |> Seq.toList
        |> List.takeWhile (fun c -> c <> '.')
        |> List.map string
        |> String.concat ""

    member this.Host =
        match this.Hostname.ToLowerInvariant() with
        | "github.com" -> RemoteHost.GitHub
        // | "gitlab.com" -> Host.GitLab
        | _ -> RemoteHost.Unknown

[<RequireQualifiedAccess>]
module ChangelogMetadata =

    type UpdateVersionInfo =
        {
            File: string
            Pattern: string
        }

        static member Decoder: Decoder<UpdateVersionInfo> =
            Decode.object (fun get ->
                {
                    File = get.Required.Field "file" Decode.string
                    Pattern = get.Required.Field "pattern" Decode.string
                }
            )

        static member Encoder(info: UpdateVersionInfo) =
            Encode.object
                [
                    "file", Encode.string info.File
                    "pattern", Encode.string info.Pattern
                ]

    type JsonUpdaterInfo =
        {
            File: string
            Pointer: string
        }

        static member Decoder: Decoder<JsonUpdaterInfo> =
            Decode.object (fun get ->
                {
                    File = get.Required.Field "file" Decode.string
                    Pointer = get.Required.Field "pointer" Decode.string
                }
            )

        static member Encoder(info: JsonUpdaterInfo) =
            Encode.object
                [
                    "file", Encode.string info.File
                    "pointer", Encode.string info.Pointer
                ]

    type XmlUpdaterInfo =
        {
            File: string
            Selector: string
        }

        static member Decoder: Decoder<XmlUpdaterInfo> =
            Decode.object (fun get ->
                {
                    File = get.Required.Field "file" Decode.string
                    Selector = get.Required.Field "selector" Decode.string
                }
            )

        static member Encoder(info: XmlUpdaterInfo) =
            Encode.object
                [
                    "file", Encode.string info.File
                    "selector", Encode.string info.Selector
                ]

    [<RequireQualifiedAccess>]
    type Updater =
        | ReplaceRegex of info: UpdateVersionInfo
        | PackageJson of file: string
        | JsonPatch of info: JsonUpdaterInfo
        | Xml of info: XmlUpdaterInfo
        | Command of command: string

        static member Decoder: Decoder<Updater> =
            Decode.oneOf
                [
                    Decode.field "regex" UpdateVersionInfo.Decoder |> Decode.map ReplaceRegex

                    Decode.field "package.json" (Decode.field "file" Decode.string)
                    |> Decode.map PackageJson

                    Decode.field "json" JsonUpdaterInfo.Decoder |> Decode.map JsonPatch

                    Decode.field "xml" XmlUpdaterInfo.Decoder |> Decode.map Xml

                    Decode.field "command" Decode.string |> Decode.map Command

                    Decode.fail
                        """Unrecognized updater type.

Please check that you are using one of the following:

- regex
- package.json
- json
- xml
- command

If yes, please check the error above for potential issues."""
                ]

        static member Encoder(updater: Updater) =
            match updater with
            | ReplaceRegex info -> Encode.object [ "replace_regex", UpdateVersionInfo.Encoder info ]
            | PackageJson file ->
                Encode.object [ "package.json", Encode.object [ "file", Encode.string file ] ]
            | JsonPatch info -> Encode.object [ "json", JsonUpdaterInfo.Encoder info ]
            | Xml info -> Encode.object [ "xml", XmlUpdaterInfo.Encoder info ]
            | Command command -> Encode.object [ "command", Encode.string command ]

type ChangelogMetadata =

    {
        LastCommitReleased: string option
        Include: string list
        Exclude: string list
        PreReleasePrefix: string option
        Priority: int option
        Name: string option
        Updaters: ChangelogMetadata.Updater list
        ForceVersion: SemVersion option
    }

    static member Empty =
        {
            LastCommitReleased = None
            Include = []
            Exclude = []
            PreReleasePrefix = None
            Priority = None
            Name = None
            Updaters = []
            ForceVersion = None
        }

    static member Decoder: Decoder<ChangelogMetadata> =
        let versionDecoder: Decoder<SemVersion> =
            Decode.string
            |> Decode.andThen (fun version ->
                match SemVersion.TryParse(version, SemVersionStyles.Strict) with
                | true, semVersion -> Decode.succeed semVersion
                | false, _ -> Decode.fail "Unable to parse version. Please make sure it is a valid semantic version, e.g. 1.0.0 or 2.1.3-beta.1"
            )

        Decode.object (fun get ->
            {
                LastCommitReleased = get.Optional.Field "last_commit_released" Decode.string
                Include =
                    get.Optional.Field "include" (Decode.list Decode.string)
                    |> Option.defaultValue []
                Exclude =
                    get.Optional.Field "exclude" (Decode.list Decode.string)
                    |> Option.defaultValue []
                PreReleasePrefix = get.Optional.Field "pre_release" Decode.string
                Priority = get.Optional.Field "priority" Decode.int
                Name = get.Optional.Field "name" Decode.string
                Updaters =
                    get.Optional.Field "updaters" (Decode.list ChangelogMetadata.Updater.Decoder)
                    |> Option.defaultValue []
                ForceVersion =  get.Optional.Field "force_version" versionDecoder
            }
        )

    static member Encoder(metadata: ChangelogMetadata) =
        Encode.Object.empty
        |> Encode.Object.addField
            "last_commit_released"
            metadata.LastCommitReleased
            (Encode.lossyOption Encode.string)
        |> Encode.Object.addFieldIfSome "pre_release" metadata.PreReleasePrefix Encode.string
        |> Encode.Object.addFieldIfSome "priority" metadata.Priority Encode.int
        |> Encode.Object.addFieldIfSome "name" metadata.Name Encode.string
        |> Encode.Object.addFieldIfNotEmpty "include" (metadata.Include |> List.map Encode.string)
        |> Encode.Object.addFieldIfNotEmpty "exclude" (metadata.Exclude |> List.map Encode.string)
        |> Encode.Object.addFieldIfNotEmpty
            "updaters"
            (metadata.Updaters |> List.map ChangelogMetadata.Updater.Encoder)
        // IMPORTANT: force_version is never encoded !!!
        // This is because if value is `None`, we don't want to output it
        // If the value is `Some ...`, it is used internally to override the version
        // during the current run, but it should not be persisted in the changelog file,
        // otherwise it would be used in the next runs as well, which is not what we want.
        |> Encode.object
