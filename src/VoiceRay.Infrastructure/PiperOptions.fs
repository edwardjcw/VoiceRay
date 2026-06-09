namespace VoiceRay.Infrastructure

open Microsoft.Extensions.Configuration

/// Piper CLI paths (see `docs/providers.md`). `VoiceModel` is the default (en-US) voice;
/// `Voices` maps a locale onto its own ONNX voice so typed words can be synthesized in
/// other languages (e.g. fr-FR).
type PiperOptions =
    { Executable: string
      VoiceModel: string
      MediaRoot: string
      Voices: Map<string, string> }

module PiperOptions =
    let sectionName = "Speech:Piper"

    [<Literal>]
    let defaultLocale = "en-US"

    /// Built-in voice ONNX file names per locale (downloaded by `PiperProvisioner`).
    let builtInVoiceFiles =
        Map.ofList
            [ "en-US", "en_US-lessac-medium.onnx"
              "fr-FR", "fr_FR-siwis-medium.onnx" ]

    let private voicesRoot (voiceModel: string) =
        let dir = System.IO.Path.GetDirectoryName voiceModel

        if System.String.IsNullOrEmpty dir then
            System.IO.Path.Combine("models", "piper", "voices")
        else
            dir

    let load (configuration: IConfiguration) (contentRoot: string) =
        let section = configuration.GetSection(sectionName)

        let resolve (relative: string) =
            if System.IO.Path.IsPathRooted relative then
                relative
            else
                System.IO.Path.GetFullPath(System.IO.Path.Combine(contentRoot, relative))

        let voiceModel =
            resolve (
                section.["VoiceModel"]
                |> Option.ofObj
                |> Option.defaultValue "models/piper/voices/en_US-lessac-medium.onnx"
            )

        let voiceDir = voicesRoot voiceModel

        // Start from built-in defaults, resolved next to the default voice model.
        let defaults =
            builtInVoiceFiles
            |> Map.map (fun locale fileName ->
                if locale = defaultLocale then
                    voiceModel
                else
                    resolve (System.IO.Path.Combine(voiceDir, fileName)))

        // Optional config overrides: `Speech:Piper:Voices:<locale> = path/to/voice.onnx`.
        let configured =
            section.GetSection("Voices").GetChildren()
            |> Seq.choose (fun child ->
                match child.Value |> Option.ofObj with
                | Some value when not (System.String.IsNullOrWhiteSpace value) -> Some(child.Key, resolve value)
                | _ -> None)
            |> Map.ofSeq

        let voices =
            configured |> Map.fold (fun acc locale path -> Map.add locale path acc) defaults

        { Executable = resolve (section.["Executable"] |> Option.ofObj |> Option.defaultValue "models/piper/bin/piper/piper.exe")
          VoiceModel = voiceModel
          MediaRoot =
              resolve (section.["MediaRoot"] |> Option.ofObj |> Option.defaultValue "wwwroot/media/reference")
          Voices = voices }

    /// Resolves the voice ONNX path for a locale, falling back to the default voice.
    let resolveVoice (options: PiperOptions) (locale: string) =
        let locale = if System.String.IsNullOrWhiteSpace locale then defaultLocale else locale

        match options.Voices.TryFind locale with
        | Some path -> path
        | None -> options.VoiceModel

    /// True when the Piper binary and the default (en-US) voice are present.
    let isConfigured (options: PiperOptions) =
        System.IO.File.Exists options.Executable && System.IO.File.Exists options.VoiceModel

    /// True when the Piper binary and the voice for `locale` are both present.
    let isVoiceReady (options: PiperOptions) (locale: string) =
        System.IO.File.Exists options.Executable
        && System.IO.File.Exists(resolveVoice options locale)
