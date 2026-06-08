namespace VoiceRay.Infrastructure

open System.Text.Json.Serialization
open VoiceRay.Core

/// Runtime resource status for health and setup UI.
type SpeechCapabilities =
    { [<JsonPropertyName("piperReady")>]
      PiperReady: bool
      [<JsonPropertyName("piperStatus")>]
      PiperStatus: string
      [<JsonPropertyName("canAutoProvision")>]
      CanAutoProvision: bool
      [<JsonPropertyName("whisperCacheAvailable")>]
      WhisperCacheAvailable: bool
      [<JsonPropertyName("vocalTractReady")>]
      VocalTractReady: bool
      [<JsonPropertyName("allRequiredReady")>]
      AllRequiredReady: bool
      [<JsonPropertyName("setupState")>]
      SetupState: string }

module SpeechCapabilities =
    let build (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        let snap = ProvisionLog.snapshot ()

        { PiperReady = PiperProvisioner.isReady piper
          PiperStatus = PiperProvisioner.statusMessage piper
          CanAutoProvision = System.OperatingSystem.IsWindows()
          WhisperCacheAvailable = WhisperProvisioner.isReady alignment
          VocalTractReady = VocalTractProvisioner.isReady contentRoot
          AllRequiredReady = SetupProvisioner.allRequiredReady contentRoot piper alignment
          SetupState = snap.state }
