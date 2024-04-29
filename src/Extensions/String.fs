[<RequireQualifiedAccess>]
module String

let normalizeNewLines (s: string) =
    s.Replace("\r\n", "\n").Replace("\r", "\n")

let splitBy (newLineSeparator: char) (s: string) =
    s.Split(newLineSeparator) |> Array.toList

let prepend (prefix: string) (str: string) = prefix + str

let append (suffix: string) (str: string) = str + suffix

let capitalizeFirstLetter (text: string) =
    (string text.[0]).ToUpper() + text.[1..]
