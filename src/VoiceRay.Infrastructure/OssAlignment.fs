namespace VoiceRay.Infrastructure

open VoiceRay.Core

/// OSS forced-alignment stubs (Whisper timing path, MFA fallback) — no Azure.
module OssAlignment =
    type AlignmentEngine =
        | WhisperStub
        | MfaStub

    type AlignmentResult =
        { Phonemes: PhonemeSegment list
          Engine: AlignmentEngine
          Device: ComputeDevice.DeviceKind }

    let engineName =
        function
        | WhisperStub -> "whisper-stub"
        | MfaStub -> "mfa-stub"

    let private alignWithEngine (engine: AlignmentEngine) (locale: Locale) (g2p: G2pStub.G2pResult) (durationMs: int) =
        let device = ComputeDevice.resolve ()
        let phonemes = G2pStub.buildTimeline g2p.IpaSymbols durationMs

        let phonemes =
            match engine with
            | WhisperStub -> phonemes
            | MfaStub ->
                // Stub MFA path: slight boundary nudge so UI can distinguish engine metadata.
                phonemes
                |> List.map (fun p ->
                    { p with
                        StartMs = min (p.EndMs - 1) (p.StartMs + 5)
                        EndMs = p.EndMs })

        { Phonemes = phonemes
          Engine = engine
          Device = device }

    let align (options: AlignmentOptions) (locale: Locale) (g2p: G2pStub.G2pResult) (durationMs: int) =
        let preferWhisper =
            options.Provider = AlignmentProvider.Whisper
            && AlignmentOptions.whisperCacheAvailable options

        let engine =
            if preferWhisper then
                WhisperStub
            else
                MfaStub

        alignWithEngine engine locale g2p durationMs

    let toMetadata (result: AlignmentResult) =
        { AlignmentEngine = engineName result.Engine
          ComputeDevice = ComputeDevice.deviceName result.Device
          DeviceBanner = ComputeDevice.deviceBanner result.Device
          SampleRateHz = 16000
          Channels = 1
          PhonemeInference = None
          InferredWord = None
          InferenceNote = None }

    let withInference
        (metadata: AnalyzeMetadata)
        (inference: string option)
        (inferredWord: string option)
        (note: string option)
        =
        { metadata with
            PhonemeInference = inference
            InferredWord = inferredWord
            InferenceNote = note }
