open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
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

    let repoRoot = RepoPaths.resolveRepoRoot builder.Environment.ContentRootPath

    let piperOptions =
        // `PiperOptions.load` anchors every relative path at the repo root, which is correct
        // for models/ but wrong for reference audio: static files are served from the API's
        // web root (ContentRoot/wwwroot), not the repo root. Re-anchor MediaRoot there so the
        // saved /media/reference/*.wav is actually reachable for any newly synthesized word.
        let loaded = PiperOptions.load builder.Configuration repoRoot

        let mediaRel =
            builder.Configuration.[$"{PiperOptions.sectionName}:MediaRoot"]
            |> Option.ofObj
            |> Option.defaultValue "wwwroot/media/reference"

        let mediaRoot =
            if System.IO.Path.IsPathRooted mediaRel then
                mediaRel
            else
                System.IO.Path.GetFullPath(System.IO.Path.Combine(builder.Environment.ContentRootPath, mediaRel))

        { loaded with MediaRoot = mediaRoot }
    let alignmentOptions = AlignmentOptions.load builder.Configuration repoRoot
    // Make the configured wav2vec2 precision variant the process default (env still overrides)
    // so provisioning/readiness/model-path resolution all agree on one filename.
    Wav2Vec2Provisioner.setDefaultVariant alignmentOptions.Wav2Vec2Variant
    builder.Services.AddSingleton(piperOptions) |> ignore
    builder.Services.AddSingleton(alignmentOptions) |> ignore
    builder.Services.AddSingleton<ReferenceService>(fun _ ->
        ReferenceService(piperOptions, alignmentOptions, repoRoot))
    |> ignore
    builder.Services.AddSingleton<AnalyzeService>(fun (sp: IServiceProvider) ->
        let env = sp.GetService(typeof<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>) :?> Microsoft.AspNetCore.Hosting.IWebHostEnvironment
        AnalyzeService(alignmentOptions, piperOptions, RepoPaths.resolveRepoRoot env.ContentRootPath))
    |> ignore
    builder.Services.AddSingleton<CompareService>() |> ignore

    let app = builder.Build()

    Task.Run(fun () -> WhisperTranscriber.warmUp repoRoot alignmentOptions)
    |> ignore

    Task.Run(fun () -> Wav2Vec2Phoneme.warmUp repoRoot) |> ignore

    let speechProvider =
        builder.Configuration.["Speech:Provider"]
        |> Option.ofObj
        |> SpeechConfiguration.parseProvider

    app.UseCors() |> ignore
    app.UseStaticFiles() |> ignore

    if app.Environment.IsDevelopment() then
        app.MapOpenApi() |> ignore

    app.MapGet("/", Func<string>(fun () -> $"{ApplicationInfo.productName} API")) |> ignore

    app.MapGet(
        "/api/v1/health",
        Func<IWebHostEnvironment, PiperOptions, AlignmentOptions, IResult>(fun env piper alignment ->
            let capabilities = SpeechCapabilities.build (RepoPaths.resolveRepoRoot env.ContentRootPath) piper alignment

            Results.Json(
                {| status = "ok"
                   product = ApplicationInfo.productName
                   apiVersion = ApplicationInfo.apiVersion
                   speechProvider = SpeechConfiguration.providerName speechProvider
                   speech = capabilities |})))
    |> ignore

    ApiContractEndpoints.mapSetupEndpoints app |> ignore
    ApiContractEndpoints.mapContractEndpoints app |> ignore

    app.Run()
    0
