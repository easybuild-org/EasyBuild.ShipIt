namespace Spectre.Console.Cli

open Spectre.Console.Cli

[<RequireQualifiedAccess>]
module FlagValue =

    /// <summary>
    /// Returns the value of the flag if it was set, otherwise returns the provided default value.
    /// </summary>
    /// <param name="orValue">The default value to return if the flag was not set</param>
    /// <param name="flagValue">The FlagValue to evaluate</param>
    /// <returns>The value of the flag if set, otherwise the provided default value</returns>
    let orBool (orValue: bool) (flagValue: FlagValue<bool>) : bool =
        if flagValue.IsSet then
            flagValue.Value
        else
            orValue

    /// <summary>Returns the value of the flag if it was set, otherwise returns true.</summary>
    /// <param name="flagValue">The FlagValue to evaluate</param>
    /// <returns>The value of the flag if set, otherwise true</returns>
    let orTrue (flagValue: FlagValue<bool>) : bool = orBool true flagValue

    /// <summary>Returns the value of the flag if it was set, otherwise returns false.</summary>
    /// <param name="flagValue">The FlagValue to evaluate</param>
    /// <returns>The value of the flag if set, otherwise false</returns>
    let orFalse (flagValue: FlagValue<bool>) : bool = orBool false flagValue
