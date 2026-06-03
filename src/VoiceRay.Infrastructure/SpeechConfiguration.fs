namespace VoiceRay.Infrastructure

/// Server-side speech provider selection (see appsettings Speech:Provider).
module SpeechConfiguration =
    type SpeechProvider =
        | Local
        | Azure

    let parseProvider (value: string option) =
        match value with
        | Some "Azure" -> Azure
        | _ -> Local

    let providerName =
        function
        | Local -> "Local"
        | Azure -> "Azure"
