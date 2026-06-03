open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open VoiceRay.Api
open VoiceRay.Core
open VoiceRay.Infrastructure

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddCors(fun options ->
        options.AddDefaultPolicy(fun policy ->
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin() |> ignore))
    |> ignore

    builder.Services.AddOpenApi() |> ignore

    let app = builder.Build()

    let speechProvider =
        builder.Configuration.["Speech:Provider"]
        |> Option.ofObj
        |> SpeechConfiguration.parseProvider

    app.UseCors() |> ignore

    if app.Environment.IsDevelopment() then
        app.MapOpenApi() |> ignore

    app.MapGet("/", Func<string>(fun () -> $"{ApplicationInfo.productName} API")) |> ignore

    app.MapGet(
        "/api/v1/health",
        Func<string>(fun () ->
            $"""{{"status":"ok","product":"{ApplicationInfo.productName}","apiVersion":"{ApplicationInfo.apiVersion}","speechProvider":"{SpeechConfiguration.providerName speechProvider}"}}""")
    )
    |> ignore

    ApiContractEndpoints.mapContractEndpoints app |> ignore

    app.Run()
    0
