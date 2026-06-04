namespace VoiceRay.Infrastructure

open System
open System.Diagnostics
open System.IO
open System.Text

type PiperTtsError =
    | NotConfigured
    | ProcessFailed of exitCode: int * stderr: string

module PiperTts =
    let synthesize (options: PiperOptions) (text: string) =
        if not (PiperOptions.isConfigured options) then
            Error NotConfigured
        else
            let tempDir = Path.Combine(Path.GetTempPath(), "voiceray-piper")
            Directory.CreateDirectory tempDir |> ignore

            let outputPath =
                Path.Combine(tempDir, $"ref-{Guid.NewGuid():N}.wav")

            try
                let psi =
                    ProcessStartInfo(
                        FileName = options.Executable,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    )

                psi.ArgumentList.Add("-m")
                psi.ArgumentList.Add(options.VoiceModel)
                psi.ArgumentList.Add("-f")
                psi.ArgumentList.Add(outputPath)
                psi.ArgumentList.Add("--")
                psi.ArgumentList.Add(text)

                use piperProc = Process.Start psi

                if isNull piperProc then
                    Error(ProcessFailed(-1, "Failed to start Piper process"))
                else
                    let stderr = piperProc.StandardError.ReadToEnd()
                    piperProc.WaitForExit()

                    if piperProc.ExitCode <> 0 || not (File.Exists outputPath) then
                        Error(ProcessFailed(piperProc.ExitCode, stderr))
                    else
                        Ok(File.ReadAllBytes outputPath)
            finally
                if File.Exists outputPath then
                    File.Delete outputPath |> ignore
