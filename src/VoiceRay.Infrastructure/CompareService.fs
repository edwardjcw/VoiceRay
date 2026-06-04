namespace VoiceRay.Infrastructure

open System
open VoiceRay.Core

type CompareServiceError =
    | InvalidRequest of message: string
    | UnsupportedLocale

type CompareService() =
    member _.Compare(request: CompareRequest) =
        if String.IsNullOrWhiteSpace request.Locale then
            Error(InvalidRequest "locale is required")
        elif request.Locale <> "en-US" then
            Error UnsupportedLocale
        elif List.isEmpty request.ReferencePhonemes then
            Error(InvalidRequest "referencePhonemes must be non-empty")
        elif List.isEmpty request.UserPhonemes then
            Error(InvalidRequest "userPhonemes must be non-empty")
        else
            Ok(ComparePipeline.compare request.Locale request.ReferencePhonemes request.UserPhonemes)
