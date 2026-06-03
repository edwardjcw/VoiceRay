namespace VoiceRay.Api

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open VoiceRay.Core

/// Contract endpoint stubs (501 until W4–W6 pipelines). Shapes match <see cref="docs/api.md"/>.
module ApiContractEndpoints =

    let notImplemented (name: string) : IResult =
        Results.Json(
            {| error = "Not implemented"
               endpoint = name |},
            statusCode = StatusCodes.Status501NotImplemented)

    /// Registers `POST /api/v1/reference`, `analyze`, and `compare` with OpenAPI metadata.
    let mapContractEndpoints (app: WebApplication) =
        app.MapPost(
            "/api/v1/reference",
            Func<ReferenceRequest, IResult>(fun _ -> notImplemented "reference"))
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
