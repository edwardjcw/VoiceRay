module WavTestHelpers

open System
open System.Text

let encodeMonoPcm (sampleRate: int) (channels: int) (samples: int16[]) =
    let bitsPerSample = 16
    let dataBytes = samples.Length * 2
    let buffer = Array.zeroCreate<byte> (44 + dataBytes)

    let writeText (offset: int) (text: string) =
        Encoding.ASCII.GetBytes(text).CopyTo(buffer, offset)

    writeText 0 "RIFF"
    BitConverter.GetBytes(36 + dataBytes).CopyTo(buffer, 4)
    writeText 8 "WAVE"
    writeText 12 "fmt "
    BitConverter.GetBytes(16).CopyTo(buffer, 16)
    BitConverter.GetBytes(int16 1).CopyTo(buffer, 20)
    BitConverter.GetBytes(int16 channels).CopyTo(buffer, 22)
    BitConverter.GetBytes(sampleRate).CopyTo(buffer, 24)
    BitConverter.GetBytes(sampleRate * channels * bitsPerSample / 8).CopyTo(buffer, 28)
    BitConverter.GetBytes(int16 (channels * bitsPerSample / 8)).CopyTo(buffer, 32)
    BitConverter.GetBytes(int16 bitsPerSample).CopyTo(buffer, 34)
    writeText 36 "data"
    BitConverter.GetBytes(dataBytes).CopyTo(buffer, 40)

    for i in 0 .. samples.Length - 1 do
        BitConverter.GetBytes(samples.[i]).CopyTo(buffer, 44 + i * 2)

    buffer
