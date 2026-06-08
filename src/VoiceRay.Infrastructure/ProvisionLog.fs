namespace VoiceRay.Infrastructure

open System
open System.Collections.Generic

/// Thread-safe setup log and run state for UI polling.
module ProvisionLog =
    type RunState =
        | Idle
        | Running
        | Succeeded
        | Failed

    type LogLevel =
        | Info
        | Warn
        | Error

    type LogEntry =
        { At: DateTimeOffset
          Message: string
          Level: string }

    let private maxEntries = 200
    let private sync = obj ()
    let private entries = List<LogEntry>()
    let mutable private state = Idle
    let mutable private lastError: string option = None

    let private toLevel level =
        match level with
        | Warn -> "warn"
        | Error -> "error"
        | _ -> "info"

    let append level message =
        lock sync (fun () ->
            entries.Add(
                { At = DateTimeOffset.UtcNow
                  Message = message
                  Level = toLevel level }
            )

            if entries.Count > maxEntries then
                entries.RemoveAt(0))

    let info message = append Info message
    let warn message = append Warn message
    let error message = append Error message

    let tryBeginRun () =
        lock sync (fun () ->
            if state = Running then
                false
            else
                entries.Clear()
                state <- Running
                lastError <- None
                true)

    let endRun success err =
        lock sync (fun () ->
            state <- if success then Succeeded else Failed
            lastError <- err)

    let markIdle () =
        lock sync (fun () -> state <- Idle)

    let currentState () =
        lock sync (fun () -> state)

    let snapshot () =
        lock sync (fun () ->
            {| state =
                match state with
                | Idle -> "idle"
                | Running -> "running"
                | Succeeded -> "succeeded"
                | Failed -> "failed"
               logs = entries |> Seq.toArray
               lastError = lastError |})

    let isRunning () = currentState () = Running
