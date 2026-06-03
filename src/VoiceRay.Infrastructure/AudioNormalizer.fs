namespace VoiceRay.Infrastructure

open System
open System.Collections.Generic
open System.IO
open System.Text

/// Ensures uploaded WAV is 16 kHz mono 16-bit PCM (see `docs/plan.md`).
module AudioNormalizer =
    type NormalizerError =
        | EmptyInput
        | InvalidWav of message: string
        | UnsupportedFormat of message: string

    type WavPcm =
        { SampleRate: int
          Channels: int
          BitsPerSample: int
          Samples: int16[] }

    let private targetSampleRate = 16000
    let private targetChannels = 1
    let private targetBits = 16

    let private readChunks (reader: BinaryReader) (ms: MemoryStream) =
        let mutable sampleRate = 0
        let mutable channels = 0
        let mutable bitsPerSample = 0
        let data = ResizeArray<byte>()

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
                data.AddRange(reader.ReadBytes chunkSize)
            | _ -> ms.Position <- ms.Position + int64 chunkSize

        if sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0 || data.Count = 0 then
            None
        else
            Some(sampleRate, channels, bitsPerSample, data.ToArray())

    let tryParsePcm (wavBytes: byte[]) =
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
                    match readChunks reader ms with
                    | None -> None
                    | Some(sampleRate, channels, bitsPerSample, data) ->
                        if bitsPerSample <> 16 then
                            None
                        else
                            let bytesPerSample = bitsPerSample / 8
                            let sampleCount = data.Length / bytesPerSample
                            let samples =
                                Array.init sampleCount (fun i ->
                                    BitConverter.ToInt16(data, i * bytesPerSample))

                            Some
                                { SampleRate = sampleRate
                                  Channels = channels
                                  BitsPerSample = bitsPerSample
                                  Samples = samples }

    let private toMono (pcm: WavPcm) =
        if pcm.Channels = 1 then
            pcm.Samples
        elif pcm.Channels = 2 then
            let frames = pcm.Samples.Length / 2
            Array.init frames (fun i ->
                let left = pcm.Samples.[i * 2]
                let right = pcm.Samples.[i * 2 + 1]
                int16 ((int left + int right) / 2))
        else
            failwith "Unsupported channel count"

    let private resample (samples: int16[]) (sourceRate: int) (targetRate: int) =
        if sourceRate = targetRate then
            samples
        else
            let ratio = float sourceRate / float targetRate
            let outLen = max 1 (int (ceil (float samples.Length / ratio)))
            Array.init outLen (fun i ->
                let srcPos = float i * ratio
                let idx = int srcPos
                let frac = srcPos - float idx

                if idx >= samples.Length - 1 then
                    samples.[samples.Length - 1]
                else
                    let a = float samples.[idx]
                    let b = float samples.[idx + 1]
                    int16 (a + (b - a) * frac))

    let private encodeWav (samples: int16[]) =
        let dataBytes = samples.Length * 2
        let buffer = Array.zeroCreate<byte> (44 + dataBytes)
        let writer (offset: int) (text: string) =
            Encoding.ASCII.GetBytes(text).CopyTo(buffer, offset)

        writer 0 "RIFF"
        BitConverter.GetBytes(36 + dataBytes).CopyTo(buffer, 4)
        writer 8 "WAVE"
        writer 12 "fmt "
        BitConverter.GetBytes(16).CopyTo(buffer, 16)
        BitConverter.GetBytes(int16 1).CopyTo(buffer, 20)
        BitConverter.GetBytes(int16 targetChannels).CopyTo(buffer, 22)
        BitConverter.GetBytes(targetSampleRate).CopyTo(buffer, 24)
        BitConverter.GetBytes(targetSampleRate * targetChannels * targetBits / 8).CopyTo(buffer, 28)
        BitConverter.GetBytes(int16 (targetChannels * targetBits / 8)).CopyTo(buffer, 32)
        BitConverter.GetBytes(int16 targetBits).CopyTo(buffer, 34)
        writer 36 "data"
        BitConverter.GetBytes(dataBytes).CopyTo(buffer, 40)

        for i in 0 .. samples.Length - 1 do
            BitConverter.GetBytes(samples.[i]).CopyTo(buffer, 44 + i * 2)

        buffer

    let normalize (wavBytes: byte[]) =
        if isNull wavBytes || wavBytes.Length = 0 then
            Error EmptyInput
        else
            match tryParsePcm wavBytes with
            | None -> Error(InvalidWav "Expected a PCM WAV file (RIFF/WAVE).")
            | Some pcm when pcm.BitsPerSample <> 16 ->
                Error(UnsupportedFormat "Only 16-bit PCM WAV is supported.")
            | Some pcm when pcm.Channels > 2 ->
                Error(UnsupportedFormat "WAV must be mono or stereo.")
            | Some pcm ->
                try
                    let mono = toMono pcm
                    let resampled = resample mono pcm.SampleRate targetSampleRate
                    Ok(encodeWav resampled)
                with ex ->
                    Error(UnsupportedFormat ex.Message)
