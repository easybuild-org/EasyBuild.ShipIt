[<AutoOpen>]
module TaskExtensions

type System.Threading.Tasks.Task with

    static member inline RunSynchronously(task: System.Threading.Tasks.Task<'T>) : 'T =
        task.GetAwaiter().GetResult()

    static member inline RunSynchronously(task: System.Threading.Tasks.Task) : unit =
        task.GetAwaiter().GetResult()
