module JsonPointer

open System.Text.RegularExpressions
open System

type JsonPointer = JsonPointer of string

module JsonPointer =

    module Advanced =

        /// <summary>
        /// Try to parse a segment as an array index as described in RFC 6901.
        ///
        /// array-index = %x30 / ( %x31-39 *(%x30-39) )
        ///               ; "0", or digits without a leading "0"
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        let tryParseArrayIndex (segment: string) =
            let regex = Regex(@"^(0|[1-9][0-9]*)$")

            if regex.IsMatch(segment) then
                System.Int32.Parse(segment) |> Some
            else
                None

    let create (pointer: string) : JsonPointer =
        // TODO: Validate pointer according to RFC 6901
        JsonPointer pointer

    let split (JsonPointer pointer) : string list =
        if String.IsNullOrEmpty(pointer) then
            []
        else
            pointer.Split('/', StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList
            |> List.map (fun segment -> segment.Replace("~1", "/").Replace("~0", "~"))
