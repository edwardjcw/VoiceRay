namespace VoiceRay.Api

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open VoiceRay.Core
open VoiceRay.Infrastructure

/// Contract endpoints — reference (W4), analyze (W5), compare (W6).
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
        | Error(ReferenceServiceError.RecognitionUnavailable message) ->
            Results.Json(
                {| error =
                    "Phoneme recognition is required for free-typed words but is not ready: "
                    + message
                    + " Use setup in the app to download the phoneme model."
                   code = "recognition_not_ready"
                   canProvision = true |},
                statusCode = StatusCodes.Status503ServiceUnavailable)
        | Error ReferenceServiceError.TtsUnavailable ->
            Results.Json(
                {| error = "Speech engine is not ready. Use setup in the app to download it."
                   code = "speech_not_ready"
                   canProvision = OperatingSystem.IsWindows() |},
                statusCode = StatusCodes.Status503ServiceUnavailable)

    let handleSetupStatus (env: IWebHostEnvironment) (piper: PiperOptions) (alignment: AlignmentOptions) : IResult =
        Results.Json(SetupProvisioner.buildStatus (RepoPaths.resolveRepoRoot env.ContentRootPath) piper alignment)

    let handleSetupRun (env: IWebHostEnvironment) (piper: PiperOptions) (alignment: AlignmentOptions) : IResult =
        let status = SetupProvisioner.buildStatus (RepoPaths.resolveRepoRoot env.ContentRootPath) piper alignment

        if status.ready then
            Results.Json(
                {| state = "succeeded"
                   message = "All required resources are already ready." |})
        elif SetupProvisioner.startBackground (RepoPaths.resolveRepoRoot env.ContentRootPath) piper alignment then
            Results.Json(
                {| state = "running"
                   message = "Setup started. Poll GET /api/v1/setup/status for progress." |},
                statusCode = StatusCodes.Status202Accepted)
        else
            Results.Json(
                SetupProvisioner.buildStatus (RepoPaths.resolveRepoRoot env.ContentRootPath) piper alignment,
                statusCode = StatusCodes.Status409Conflict)

    /// Legacy alias — starts full resource setup (not Piper-only).
    let handleProvisionSpeech (env: IWebHostEnvironment) (piper: PiperOptions) (alignment: AlignmentOptions) : IResult =
        handleSetupRun env piper alignment

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

    let handleCompare (request: CompareRequest) (service: CompareService) : IResult =
        match service.Compare request with
        | Ok response ->
            Results.Content(ContractJson.serialize response, "application/json")
        | Error(CompareServiceError.InvalidRequest message) ->
            Results.Json({| error = message |}, statusCode = StatusCodes.Status400BadRequest)
        | Error CompareServiceError.UnsupportedLocale ->
            Results.Json(
                {| error = "locale must be en-US for compare (demo MVP)." |},
                statusCode = StatusCodes.Status400BadRequest)

    let mapSetupEndpoints (app: WebApplication) =
        app.MapGet(
            "/api/v1/setup/status",
            Func<IWebHostEnvironment, PiperOptions, AlignmentOptions, IResult>(handleSetupStatus))
        |> ignore

        app.MapPost(
            "/api/v1/setup/run",
            Func<IWebHostEnvironment, PiperOptions, AlignmentOptions, IResult>(handleSetupRun))
        |> ignore

        app

    /// Registers `POST /api/v1/reference`, `analyze`, and `compare` with OpenAPI metadata.
    let mapContractEndpoints (app: WebApplication) =
        app.MapPost(
            "/api/v1/reference",
            Func<ReferenceRequest, ReferenceService, IResult>(handleReference))
        |> ignore

        app
            .MapPost(
                "/api/v1/analyze",
                Func<IFormCollection, AnalyzeService, Task<IResult>>(handleAnalyze))
            .DisableAntiforgery()
        |> ignore

        app.MapPost(
            "/api/v1/compare",
            Func<CompareRequest, CompareService, IResult>(handleCompare))
        |> ignore

        app.MapPost(
            "/api/v1/provision/speech",
            Func<IWebHostEnvironment, PiperOptions, AlignmentOptions, IResult>(handleProvisionSpeech))
        |> ignore

        app
