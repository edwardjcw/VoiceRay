namespace VoiceRay.Infrastructure

open System
open System.IO
open System.Text

/// Reads duration from a PCM WAV RIFF header.
module WavDuration =
    let tryGetDurationMs (wavBytes: byte[]) =
        if isNull wavBytes || wavBytes.Length < 44 then
            None
        else
            use ms = new MemoryStream(wavBytes)
            use reader = new BinaryReader(ms)

            let riff = reader.ReadBytes 4 |> Encoding.ASCII.GetString

            if riff <> "RIFF" then
                None
            else
                reader.ReadInt32() |> ignore
                let wave = reader.ReadBytes 4 |> Encoding.ASCII.GetString

                if wave <> "WAVE" then
                    None
                else
                    let mutable sampleRate = 0
                    let mutable channels = 0
                    let mutable bitsPerSample = 0
                    let mutable dataBytes = 0

                    while ms.Position < ms.Length - 8L do
                        let chunkId = reader.ReadBytes 4 |> Encoding.ASCII.GetString
                        let chunkSize = reader.ReadInt32()

                        match chunkId with
                        | "fmt " ->
                            reader.ReadInt16() |> ignore
                            channels <- int (reader.ReadInt16())
                            sampleRate <- reader.ReadInt32()
                            reader.ReadInt32() |> ignore
                            reader.ReadInt16() |> ignore
                            bitsPerSample <- int (reader.ReadInt16())
                            let remaining = chunkSize - 16

                            if remaining > 0 then
                                reader.ReadBytes remaining |> ignore
                        | "data" ->
                            dataBytes <- chunkSize
                            ms.Position <- ms.Position + int64 chunkSize
                        | _ -> ms.Position <- ms.Position + int64 chunkSize

                    if sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0 || dataBytes <= 0 then
                        None
                    else
                        let bytesPerSample = bitsPerSample / 8
                        let frameCount = dataBytes / (channels * bytesPerSample)
                        Some(int (1000.0 * float frameCount / float sampleRate))
