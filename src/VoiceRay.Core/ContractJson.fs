namespace VoiceRay.Core

open System.Text.Json

/// Standard JSON options for API contract serialization (camelCase).
module ContractJson =
    let options =
        let o = JsonSerializerOptions(JsonSerializerDefaults.Web)
        o.Converters.Add(CompareSegmentJsonConverter())
        o

    let serialize<'T> (value: 'T) = JsonSerializer.Serialize(value, options)

    let deserialize<'T> (json: string) = JsonSerializer.Deserialize<'T>(json, options)
