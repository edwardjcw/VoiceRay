namespace VoiceRay.Infrastructure

open Microsoft.Extensions.Configuration

/// Piper CLI paths (see `docs/providers.md`).
type PiperOptions =
    { Executable: string
      VoiceModel: string
      MediaRoot: string }

module PiperOptions =
    let sectionName = "Speech:Piper"

    let load (configuration: IConfiguration) (contentRoot: string) =
        let section = configuration.GetSection(sectionName)

        let resolve (relative: string) =
            if System.IO.Path.IsPathRooted relative then
                relative
            else
                System.IO.Path.GetFullPath(System.IO.Path.Combine(contentRoot, relative))

        { Executable = resolve (section.["Executable"] |> Option.ofObj |> Option.defaultValue "models/piper/bin/piper/piper.exe")
          VoiceModel =
              resolve (
                  section.["VoiceModel"]
                  |> Option.ofObj
                  |> Option.defaultValue "models/piper/voices/en_US-lessac-medium.onnx"
              )
          MediaRoot =
              resolve (section.["MediaRoot"] |> Option.ofObj |> Option.defaultValue "wwwroot/media/reference") }

    let isConfigured (options: PiperOptions) =
        System.IO.File.Exists options.Executable && System.IO.File.Exists options.VoiceModel
