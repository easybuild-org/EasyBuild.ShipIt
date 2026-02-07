module EasyBuild.ShipIt.Tests.ThothYamlDecode

open Thoth.Yaml
open TUnit.Core
open VerifyTUnit

open Tests.Utils

open type Verifier

type Price =
    | Normal of float
    | Reduced of float option
    | Zero

[<Category("Thoth.Yaml.Decode")>]
type ErrorsTests() =

    [<Test>]
    member _.``invalid yaml``() =
        let yaml = "this: is: invalid: yaml"
        let result = Decode.fromString Decode.string yaml

        let expected =
            Error
                "Given an invalid YAML: While scanning a plain scalar value, found invalid mapping."

        Expect.equal result expected

[<Category("Thoth.Yaml.Decode")>]
type PrimitivesTests() =

    [<Test>]
    member _.``null works``() =
        let decoder = Decode.field "prop" (Decode.nil 0)

        Expect.equal (Decode.unsafeFromString decoder "prop: null") 0
        Expect.equal (Decode.unsafeFromString decoder "prop:") 0
        Expect.equal (Decode.unsafeFromString decoder "prop: Null") 0
        Expect.equal (Decode.unsafeFromString decoder "prop: NULL") 0
        Expect.equal (Decode.unsafeFromString decoder "prop: ~") 0

    [<Test>]
    member _.``null as string works``() =
        let decoder = Decode.field "prop" Decode.string
        let result = Decode.unsafeFromString decoder "prop: \"null\""

        Expect.equal result "null"

    [<Test>]
    member _.``a string works``() =
        let result = Decode.fromString Decode.string "value"

        Expect.equal result (Ok "value")

    [<Test>]
    member _.``an int works``() =
        let result = Decode.fromString Decode.int "42"

        Expect.equal result (Ok 42)

    [<Test>]
    member _.``an int represented as a string works``() =
        let result = Decode.fromString Decode.int " \"42\" "

        Expect.equal result (Ok 42)

[<Category("Thoth.Yaml.Decode")>]
type ObjectPrimitivesTests() =

    [<Test>]
    member _.``field works``() =
        let yaml =
            """
name: Maxime
age: 30
"""

        let actual = Decode.unsafeFromString (Decode.field "name" Decode.string) yaml

        Expect.equal actual "Maxime"

    [<Test>]
    member _.``field output an error when the field is missing``() =
        task {
            let yaml =
                """
age: 30
city: Paris
"""

            let actual = Decode.fromString (Decode.field "name" Decode.string) yaml

            return! Verify actual
        }

    [<Test>]
    member _.``at works``() =
        let yaml =
            """
person:
    name: Maxime
    age: 30
"""

        let actual =
            Decode.unsafeFromString
                (Decode.at
                    [
                        "person"
                        "age"
                    ]
                    Decode.int)
                yaml

        Expect.equal actual 30

    [<Test>]
    member _.``at output an error if the path failed``() =
        task {
            let yaml =
                """
person:
    name: Maxime
    age: 30
"""

            let actual =
                Decode.fromString
                    (Decode.at
                        [
                            "person"
                            "address"
                        ]
                        Decode.string)
                    yaml

            return! Verify actual
        }

    [<Test>]
    member _.``optional works``() =
        let yaml =
            """
name: Maxime
age: 30
something_undefined: null
"""

        let valid = Decode.unsafeFromString (Decode.optional "name" Decode.string) yaml

        let expectingAnError = Decode.fromString (Decode.optional "name" Decode.int) yaml

        let missingShouldBeNone =
            Decode.unsafeFromString (Decode.optional "missing_field" Decode.string) yaml

        let undefinedFieldShouldBeNone =
            Decode.unsafeFromString (Decode.optional "something_undefined" Decode.int) yaml

        Expect.equal valid (Some "Maxime")

        Expect.equal
            expectingAnError
            (Error
                """Error at: `$.name`
Expecting int but instead got:
Maxime

Reason: Could not parse integer""")

        Expect.equal missingShouldBeNone None
        Expect.equal undefinedFieldShouldBeNone None

    [<Test>]
    member _.``optional returns Error value if decoder fails``() =
        task {
            let yaml =
                """name: Maxime
age: thirty
"""

            let actual = Decode.fromString (Decode.optional "age" Decode.int) yaml

            return! Verify actual
        }

    [<Test>]
    member _.``optionalAt works``() =
        let yaml =
            """
data:
    name: Maxime
    age: 30
something_undefined: null
"""

        let valid =
            Decode.unsafeFromString
                (Decode.optionalAt
                    [
                        "data"
                        "name"
                    ]
                    Decode.string)
                yaml

        let expectingAnError =
            Decode.fromString
                (Decode.optionalAt
                    [
                        "data"
                        "name"
                    ]
                    Decode.int)
                yaml

        let missingShouldBeNone =
            Decode.unsafeFromString
                (Decode.optionalAt
                    [
                        "data"
                        "missing_field"
                    ]
                    Decode.int)
                yaml

        let undefinedFieldShouldBeNone =
            Decode.unsafeFromString
                (Decode.optionalAt
                    [
                        "data"
                        "something_undefined"
                    ]
                    Decode.string)
                yaml

        Expect.equal valid (Some "Maxime")

        let errorMessage =
            Error
                """Error at: `$.data.name`
Expecting int but instead got:
Maxime

Reason: Could not parse integer"""

        Expect.equal expectingAnError errorMessage
        Expect.equal missingShouldBeNone None
        Expect.equal undefinedFieldShouldBeNone None

    [<Test>]
    member _.``optionalAt returns Error value if decoder fails``() =
        task {
            let yaml =
                """name: Maxime
age: thirty
"""

            let actual = Decode.fromString (Decode.optionalAt [ "age" ] Decode.int) yaml

            return! Verify actual
        }

[<Category("Thoth.Yaml.Decode")>]
type DataStructuresTests() =
    [<Test>]
    member _.``list of int works``() =
        let yaml =
            """
- 1
- 2
- 3
"""

        let actual = Decode.unsafeFromString (Decode.list Decode.int) yaml

        Expect.equal
            actual
            ([
                1
                2
                3
            ])

    [<Test>]
    member _.``nested list works``() =
        let yaml =
            """
- - 1
  - 2
"""

        let actual = Decode.unsafeFromString (Decode.list (Decode.list Decode.int)) yaml

        Expect.equal
            actual
            ([
                [
                    1
                    2
                ]
            ])

    [<Test>]
    member _.``an invalid list output an error``() =
        task {
            let yaml =
                """
not a list:
    name: Maxime
    age: 30
"""

            let actual = Decode.fromString (Decode.list Decode.int) yaml

            return! Verify actual
        }

    [<Test>]
    member _.``a list with some invalid element output an error``() =
        task {
            let yaml =
                """
- 1
- two
- 3
"""

            let actual = Decode.fromString (Decode.list Decode.int) yaml

            return! Verify actual
        }

[<Category("Thoth.Yaml.Decode")>]
type InconsistentStructureTests() =

    [<Test>]
    member _.``oneOf works``() =
        let badInt =
            Decode.oneOf
                [
                    Decode.int
                    Decode.nil 0
                ]

        let actual = Decode.fromString (Decode.list badInt) "[1,2,null,4]"

        Expect.equal
            actual
            (Ok
                [
                    1
                    2
                    0
                    4
                ])

    [<Test>]
    member _.``oneOf works in combination with object builders``() =
        let json =
            """
Bar:
    name: maxime
    age: 25
"""

        let decoder1 =
            Decode.object (fun get ->
                {|
                    fieldA = get.Required.Field "name" Decode.string
                |}
            )

        let decoder2 =
            Decode.oneOf
                [
                    Decode.field "Foo" decoder1 |> Decode.map Choice1Of2
                    Decode.field "Bar" decoder1 |> Decode.map Choice2Of2
                ]

        let actual = Decode.fromString decoder2 json

        Expect.equal
            actual
            (Ok(
                Choice2Of2
                    {|
                        fieldA = "maxime"
                    |}
            ))

    [<Test>]
    member _.``oneOf works with optional``() =
        let decoder =
            Decode.oneOf
                [
                    Decode.field "Normal" Decode.float |> Decode.map Normal
                    Decode.field "Reduced" (Decode.lossyOption Decode.float) |> Decode.map Reduced
                    Decode.field "Zero" Decode.bool |> Decode.map (fun _ -> Zero)
                ]

        let normal = Decode.unsafeFromString decoder """Normal: 4.5"""
        let reducedSome = Decode.unsafeFromString decoder """Reduced: 4.5"""
        let reducedNone = Decode.unsafeFromString decoder """Reduced: null"""
        let zero = Decode.unsafeFromString decoder """Zero: true"""

        Expect.equal normal (Normal 4.5)
        Expect.equal reducedSome (Reduced(Some 4.5))
        Expect.equal reducedNone (Reduced None)
        Expect.equal zero Zero

    [<Test>]
    member _.``oneOf output errors if all case fails``() =
        task {
            let badInt =
                Decode.oneOf
                    [
                        Decode.int
                        Decode.field "test" Decode.string |> Decode.map int
                    ]

            let actual = (Decode.fromString (Decode.list badInt) "[null,2,null,4]")

            return! Verify actual
        }

[<Category("Thoth.Yaml.Decode")>]
type ObjectBuilderTests() =
    [<Test>]
    member _.``get.Required.Field works``() =
        let yaml =
            """
name: Maxime
age: 30
"""

        let decoder =
            Decode.object (fun get ->
                {|
                    Name = get.Required.Field "name" Decode.string
                    Age = get.Required.Field "age" Decode.int
                |}
            )

        let actual = Decode.unsafeFromString decoder yaml

        Expect.equal
            actual
            {|
                Name = "Maxime"
                Age = 30
            |}

    [<Test>]
    member _.``get.Required.Field returns Error if field is missing``() =
        task {
            let yaml =
                """
age: 30
"""

            let decoder =
                Decode.object (fun get ->
                    {|
                        Name = get.Required.Field "name" Decode.string
                        Age = get.Required.Field "age" Decode.int
                    |}
                )

            let actual = Decode.fromString decoder yaml

            return! Verify actual
        }

    [<Test>]
    member _.``get.Required.Field returns Error if type is incorrect``() =
        task {
            let yaml =
                """
name: Maxime
age: thirty
"""

            let decoder =
                Decode.object (fun get ->
                    {|
                        Name = get.Required.Field "name" Decode.string
                        Age = get.Required.Field "age" Decode.int
                    |}
                )

            let actual = Decode.fromString decoder yaml

            return! Verify actual
        }

    [<Test>]
    member _.``get.Optional.Field works``() =
        let yaml =
            """
name: Maxime
age: 30
"""

        let decoder = Decode.object (fun get -> get.Optional.Field "name" Decode.string)

        let actual = Decode.unsafeFromString decoder yaml

        Expect.equal actual (Some "Maxime")

    [<Test>]
    member _.``get.Optional.Field returns None value if field is missing``() =
        let yaml =
            """
age: 30
"""

        let decoder = Decode.object (fun get -> get.Optional.Field "name" Decode.string)

        let actual = Decode.unsafeFromString decoder yaml

        Expect.equal actual None

    [<Test>]
    member _.``get.Optional.Field returns None if field is null``() =
        let yaml =
            """
name: null
age: 30
"""

        let decoder = Decode.object (fun get -> get.Optional.Field "name" Decode.string)

        let actual = Decode.unsafeFromString decoder yaml

        Expect.equal actual None

    [<Test>]
    member _.``get.Optional.Field returns Error value if decoder fails``() =
        task {
            let yaml =
                """
name: Maxime
age: thirty
"""

            let decoder = Decode.object (fun get -> get.Optional.Field "age" Decode.int)

            let actual = Decode.fromString decoder yaml

            return! Verify actual
        }

    [<Test>]
    member _.``nested get.Optional.Field > get.Required.Field returns None if field is null``() =
        let yaml =
            """
user: null
age: 30
"""

        let userDecoder =
            Decode.object (fun get ->
                {|
                    Name = get.Optional.Field "name" Decode.string
                    Email = get.Optional.Field "email" Decode.string
                |}
            )

        let decoder =
            Decode.object (fun get ->
                {|
                    User = get.Optional.Field "user" userDecoder
                    Age = get.Required.Field "age" Decode.int
                |}
            )

        let actual = Decode.unsafeFromString decoder yaml

        Expect.equal
            actual
            {|
                User = None
                Age = 30
            |}
