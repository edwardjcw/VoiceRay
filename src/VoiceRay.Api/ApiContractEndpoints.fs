namespace VoiceRay.Api

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open VoiceRay.Core
open VoiceRay.Infrastructure

/// Contract endpoints — reference (W4), analyze (W5); compare stub until W6.
module ApiContractEndpoints =
    let notImplemented (name: string) : IResult =
        Results.Json(
            {| error = "Not implemented"
               endpoint = name |},
            statusCode = StatusCodes.Status501NotImplemented)

    let handleReference (request: ReferenceRequest) (service: ReferenceService) : IResult =
        match service.Generate request with
        | Ok response -> Results.Json response
        | Error(ReferenceServiceError.InvalidRequest message) ->
            Results.Json({| error = message |}, statusCode = StatusCodes.Status400BadRequest)
        | Error ReferenceServiceError.G2pUnavailable ->
            Results.Json(
                {| error = "Unknown word or unsupported locale; demo lexicon only (en-US)." |},
                statusCode = StatusCodes.Status400BadRequest)
        | Error ReferenceServiceError.TtsUnavailable ->
            Results.Json(
                {| error = "Piper TTS is not available; provision models/piper (see docs/providers.md)." |},
                statusCode = StatusCodes.Status503ServiceUnavailable)

    let handleAnalyze (form: IFormCollection) (service: AnalyzeService) : Task<IResult> =
        task {
            let readAudio () =
                let file = form.Files.GetFile "audio"

                if isNull file || file.Length = 0L then
                    None
                else
                    use stream = new MemoryStream()
                    file.CopyTo stream
                    Some(stream.ToArray())

            let text = form.["text"].ToString()
            let locale = form.["locale"].ToString()

            match readAudio () with
            | None ->
                return
                    Results.Json(
                        {| error = "audio file is required (16 kHz mono WAV)." |},
                        statusCode = StatusCodes.Status400BadRequest)
            | Some audioBytes ->
                match service.Analyze(audioBytes, text, locale) with
                | Ok response -> return Results.Json response
                | Error(AnalyzeServiceError.InvalidRequest message) ->
                    return Results.Json({| error = message |}, statusCode = StatusCodes.Status400BadRequest)
                | Error AnalyzeServiceError.MissingAudio ->
                    return
                        Results.Json(
                            {| error = "audio file is required (16 kHz mono WAV)." |},
                            statusCode = StatusCodes.Status400BadRequest)
                | Error(AnalyzeServiceError.InvalidAudio message) ->
                    return Results.Json({| error = message |}, statusCode = StatusCodes.Status400BadRequest)
                | Error AnalyzeServiceError.G2pUnavailable ->
                    return
                        Results.Json(
                            {| error = "Unknown word or unsupported locale; demo lexicon only (en-US)." |},
                            statusCode = StatusCodes.Status400BadRequest)
        }

    /// Registers `POST /api/v1/reference`, `analyze`, and `compare` with OpenAPI metadata.
    let mapContractEndpoints (app: WebApplication) =
        app.MapPost(
            "/api/v1/reference",
            Func<ReferenceRequest, ReferenceService, IResult>(handleReference))
        |> ignore

        app.MapPost(
            "/api/v1/analyze",
            Func<IFormCollection, AnalyzeService, Task<IResult>>(handleAnalyze))
        |> ignore

        app.MapPost(
            "/api/v1/compare",
            Func<CompareRequest, IResult>(fun _ -> notImplemented "compare"))
        |> ignore

        app
