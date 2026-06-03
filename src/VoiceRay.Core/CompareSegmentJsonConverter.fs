namespace VoiceRay.Core

open System
open System.Text.Json
open System.Text.Json.Serialization

/// Serializes <see cref="CompareSegment"/> as tagged JSON objects (`kind` + optional IPA fields).
type CompareSegmentJsonConverter() =
    inherit JsonConverter<CompareSegment>()

    override _.Read(reader, _, options) =
        if reader.TokenType <> JsonTokenType.StartObject then
            raise (JsonException("Expected StartObject for CompareSegment"))

        let mutable kind: string = null
        let mutable referenceIpa: string = null
        let mutable userIpa: string = null

        while reader.Read() && reader.TokenType <> JsonTokenType.EndObject do
            if reader.TokenType = JsonTokenType.PropertyName then
                match reader.GetString() with
                | "kind" ->
                    reader.Read() |> ignore
                    kind <- reader.GetString()
                | "referenceIpa" ->
                    reader.Read() |> ignore
                    referenceIpa <- reader.GetString()
                | "userIpa" ->
                    reader.Read() |> ignore
                    userIpa <- reader.GetString()
                | name ->
                    reader.Read() |> ignore
                    reader.Skip() |> ignore
                    ()

        match kind with
        | "match" -> CompareSegment.Match
        | "substitution" ->
            if isNull referenceIpa || isNull userIpa then
                raise (JsonException("substitution requires referenceIpa and userIpa"))
            CompareSegment.Substitution(referenceIpa, userIpa)
        | "omission" ->
            if isNull referenceIpa then
                raise (JsonException("omission requires referenceIpa"))
            CompareSegment.Omission referenceIpa
        | "insertion" ->
            if isNull userIpa then
                raise (JsonException("insertion requires userIpa"))
            CompareSegment.Insertion userIpa
        | other -> raise (JsonException($"Unknown CompareSegment kind: {other}"))

    override _.Write(writer, value, _) =
        writer.WriteStartObject()

        match value with
        | CompareSegment.Match ->
            writer.WriteString("kind", "match")
        | CompareSegment.Substitution(refIpa, userIpa) ->
            writer.WriteString("kind", "substitution")
            writer.WriteString("referenceIpa", refIpa)
            writer.WriteString("userIpa", userIpa)
        | CompareSegment.Omission refIpa ->
            writer.WriteString("kind", "omission")
            writer.WriteString("referenceIpa", refIpa)
        | CompareSegment.Insertion userIpa ->
            writer.WriteString("kind", "insertion")
            writer.WriteString("userIpa", userIpa)

        writer.WriteEndObject()
