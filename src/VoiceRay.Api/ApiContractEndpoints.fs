namespace VoiceRay.Api

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open VoiceRay.Core
open VoiceRay.Infrastructure

/// Contract endpoints — reference (W4); analyze/compare remain stubs until W5–W6.
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

    /// Registers `POST /api/v1/reference`, `analyze`, and `compare` with OpenAPI metadata.
    let mapContractEndpoints (app: WebApplication) =
        app.MapPost(
            "/api/v1/reference",
            Func<ReferenceRequest, ReferenceService, IResult>(handleReference))
        |> ignore

        app.MapPost(
            "/api/v1/analyze",
            Func<IFormCollection, IResult>(fun _ -> notImplemented "analyze"))
        |> ignore

        app.MapPost(
            "/api/v1/compare",
            Func<CompareRequest, IResult>(fun _ -> notImplemented "compare"))
        |> ignore

        app
