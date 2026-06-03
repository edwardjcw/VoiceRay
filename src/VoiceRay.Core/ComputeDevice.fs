namespace VoiceRay.Core

open System

/// CPU vs GPU selection for OSS alignment stubs (CUDA probe for Whisper path).
module ComputeDevice =
    type DeviceKind =
        | Cpu
        | Cuda

    let private cudaPathPresent () =
        [ "CUDA_PATH"; "CUDA_HOME" ]
        |> List.exists (fun name ->
            match Environment.GetEnvironmentVariable name with
            | null
            | "" -> false
            | _ -> true)

    let private forceCpu () =
        match Environment.GetEnvironmentVariable "VOICERAY_FORCE_CPU" with
        | null
        | "" -> false
        | value ->
            value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)

    let resolve () =
        if forceCpu () || not (cudaPathPresent ()) then
            Cpu
        else
            Cuda

    let deviceName =
        function
        | Cpu -> "cpu"
        | Cuda -> "cuda"

    let deviceBanner =
        function
        | Cpu ->
            "Alignment running on CPU — enable CUDA for GPU acceleration (VOICERAY_FORCE_CPU unset)."
        | Cuda -> "Alignment using GPU (CUDA detected)."
