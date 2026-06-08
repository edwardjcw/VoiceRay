namespace VoiceRay.Infrastructure

open System
open System.Diagnostics

/// Discovers a Python interpreter that can `import whisper`.
module WhisperPython =
    type Launcher =
        { FileName: string
          PrefixArgs: string list }

    let private runImportCheck (launcher: Launcher) =
        try
            let psi =
                ProcessStartInfo(
                    FileName = launcher.FileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                )

            for arg in launcher.PrefixArgs do
                psi.ArgumentList.Add arg

            psi.ArgumentList.Add("-c")
            psi.ArgumentList.Add("import whisper")

            use proc = Process.Start psi

            if isNull proc then
                false
            else
                proc.WaitForExit 8000 && proc.ExitCode = 0
        with _ ->
            false

    let tryResolve () =
        match Environment.GetEnvironmentVariable "VOICERAY_PYTHON" with
        | null
        | "" -> None
        | path ->
            let launcher = { FileName = path; PrefixArgs = [] }

            if runImportCheck launcher then
                Some launcher
            else
                None
        |> Option.orElseWith (fun () ->
            [ "-3.12"; "-3.14"; "-3.11"; "-3.10" ]
            |> List.tryPick (fun version ->
                let launcher = { FileName = "py"; PrefixArgs = [ version ] }

                if runImportCheck launcher then
                    Some launcher
                else
                    None))
        |> Option.orElseWith (fun () ->
            let launcher = { FileName = "python"; PrefixArgs = [] }

            if runImportCheck launcher then
                Some launcher
            else
                None)

    let describe (launcher: Launcher) =
        if launcher.PrefixArgs.IsEmpty then
            launcher.FileName
        else
            $"{launcher.FileName} {String.Join(' ', launcher.PrefixArgs)}"
