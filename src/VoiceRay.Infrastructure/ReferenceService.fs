namespace VoiceRay.Infrastructure

open System
open System.IO
open System.Security.Cryptography
open System.Text
open VoiceRay.Core

type ReferenceServiceError =
    | InvalidRequest of message: string
    | G2pUnavailable
    | TtsUnavailable

module ReferenceMedia =
    let save (options: PiperOptions) (text: string) (wavBytes: byte[]) =
        try
            Directory.CreateDirectory options.MediaRoot |> ignore
            let hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))
            let id = Convert.ToHexString(hash).ToLowerInvariant()[..15]
            let fileName = $"{id}.wav"
            let fullPath = Path.Combine(options.MediaRoot, fileName)
            File.WriteAllBytes(fullPath, wavBytes)
            Some($"/media/reference/{fileName}")
        with _ ->
            None

type ReferenceService(options: PiperOptions) =
    member _.Options = options

    member _.Generate(request: ReferenceRequest) =
        if String.IsNullOrWhiteSpace request.Text then
            Error(InvalidRequest "text is required")
        elif String.IsNullOrWhiteSpace request.Locale then
            Error(InvalidRequest "locale is required")
        else
            match G2pStub.tryLookup request.Locale request.Text with
            | None -> Error G2pUnavailable
            | Some g2p ->
                let synthesize () = PiperTts.synthesize options request.Text

                let wavResult : Result<byte[], ReferenceServiceError> =
                    if not (PiperProvisioner.isReady options) then
                        Error TtsUnavailable
                    else
                        match synthesize () with
                        | Ok wav -> Ok wav
                        | Error PiperTtsError.NotConfigured -> Error TtsUnavailable
                        | Error PiperTtsError.TimedOut -> Error TtsUnavailable
                        | Error(PiperTtsError.ProcessFailed _) -> Error TtsUnavailable

                match wavResult with
                | Error _ -> Error TtsUnavailable
                | Ok wavBytes ->
                    let durationMs =
                        WavDuration.tryGetDurationMs wavBytes
                        |> Option.defaultValue 320

                    let session =
                        ReferencePipeline.buildSession request.Locale g2p durationMs

                    let audioUrl = ReferenceMedia.save options request.Text wavBytes

                    let audioBase64 =
                        if audioUrl.IsSome then
                            None
                        else
                            Some(Convert.ToBase64String wavBytes)

                    Ok
                        { AudioUrl = audioUrl
                          AudioBase64 = audioBase64
                          Phonemes = session.Phonemes
                          Keyframes = session.Keyframes
                          IpaDisplay = session.IpaDisplay }
