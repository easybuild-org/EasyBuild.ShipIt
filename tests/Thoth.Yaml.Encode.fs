module EasyBuild.ShipIt.Tests.ThothYamlEncode

open Thoth.Yaml
open TUnit.Core
open Tests.Utils

[<Category("Thoth.Yaml.Encode")>]
type PrimitivesTests() =

    [<Test>]

    member _.``a string works``() =
        task {
            let yaml = Encode.toString (Encode.string "hello world")

            return! verifyYaml yaml
        }

    [<Test>]
    member _.``an int works``() =
        task {
            let yaml = Encode.toString (Encode.int 42)

            return! verifyYaml yaml
        }

    [<Test>]
    member _.``a list works``() =
        task {
            let yaml =
                Encode.list
                    [
                        Encode.string "Maxime"
                        Encode.int 2
                    ]
                |> Encode.toString

            return! verifyYaml yaml
        }

    [<Test>]
    member _.``an object works``() =
        task {
            let yaml =
                Encode.object
                    [
                        "name", Encode.string "Maxime"
                        "age", Encode.int 2
                        "books",
                        Encode.list
                            [
                                Encode.string "Book A"
                                Encode.string "Book B"
                            ]
                    ]
                |> Encode.toString

            return! verifyYaml yaml
        }
