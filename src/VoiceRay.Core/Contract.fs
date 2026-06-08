namespace VoiceRay.Core

open System.Text.Json.Serialization

/// BCP-47 locale tag (e.g. `en-US`).
type Locale = string

/// One timed IPA segment on a phoneme timeline.
type PhonemeSegment =
    { [<JsonPropertyName("ipa")>]
      Ipa: string
      [<JsonPropertyName("startMs")>]
      StartMs: int
      [<JsonPropertyName("endMs")>]
      EndMs: int }

/// SVG layer pose: CSS/SVG transform and/or path `d` morph.
type LayerPose =
    { [<JsonPropertyName("transform")>]
      Transform: string option
      [<JsonPropertyName("d")>]
      D: string option }

/// Animation keyframe for one phoneme window on the sagittal rig.
type ArticulatoryKeyframe =
    { [<JsonPropertyName("ipa")>]
      Ipa: string
      [<JsonPropertyName("startMs")>]
      StartMs: int
      [<JsonPropertyName("endMs")>]
      EndMs: int
      [<JsonPropertyName("layers")>]
      Layers: Map<string, LayerPose>
      [<JsonPropertyName("highlight")>]
      Highlight: string list }

/// Phoneme-level pronunciation score from analyze (Azure/MFA).
type PhonemeScore =
    { [<JsonPropertyName("ipa")>]
      Ipa: string
      [<JsonPropertyName("score")>]
      Score: float
      [<JsonPropertyName("accuracy")>]
      Accuracy: string option }

/// Rule-based coaching line for compare UI.
type CoachingMessage =
    { [<JsonPropertyName("message")>]
      Message: string
      [<JsonPropertyName("highlightLayers")>]
      HighlightLayers: string list
      [<JsonPropertyName("referenceIpa")>]
      ReferenceIpa: string option
      [<JsonPropertyName("userIpa")>]
      UserIpa: string option }

/// Alignment segment between reference and user timelines.
type CompareSegment =
    | Match
    | Substitution of ReferenceIpa: string * UserIpa: string
    | Omission of ReferenceIpa: string
    | Insertion of UserIpa: string

/// `POST /api/v1/reference` request body.
type ReferenceRequest =
    { [<JsonPropertyName("text")>]
      Text: string
      [<JsonPropertyName("locale")>]
      Locale: Locale }

/// `POST /api/v1/reference` response body.
type ReferenceResponse =
    { [<JsonPropertyName("audioUrl")>]
      AudioUrl: string option
      [<JsonPropertyName("audioBase64")>]
      AudioBase64: string option
      [<JsonPropertyName("phonemes")>]
      Phonemes: PhonemeSegment list
      [<JsonPropertyName("keyframes")>]
      Keyframes: ArticulatoryKeyframe list
      [<JsonPropertyName("ipaDisplay")>]
      IpaDisplay: string }

/// OSS alignment metadata for analyze UI (device banner, engine path).
type AnalyzeMetadata =
    { [<JsonPropertyName("alignmentEngine")>]
      AlignmentEngine: string
      [<JsonPropertyName("computeDevice")>]
      ComputeDevice: string
      [<JsonPropertyName("deviceBanner")>]
      DeviceBanner: string
      [<JsonPropertyName("sampleRateHz")>]
      SampleRateHz: int
      [<JsonPropertyName("channels")>]
      Channels: int
      [<JsonPropertyName("phonemeInference")>]
      PhonemeInference: string option
      [<JsonPropertyName("inferredWord")>]
      InferredWord: string option
      [<JsonPropertyName("inferenceNote")>]
      InferenceNote: string option }

/// `POST /api/v1/analyze` JSON response (multipart request; see docs/api.md).
type AnalyzeResponse =
    { [<JsonPropertyName("phonemes")>]
      Phonemes: PhonemeSegment list
      [<JsonPropertyName("keyframes")>]
      Keyframes: ArticulatoryKeyframe list
      [<JsonPropertyName("scores")>]
      Scores: PhonemeScore list
      [<JsonPropertyName("audioEcho")>]
      AudioEcho: string option
      [<JsonPropertyName("metadata")>]
      Metadata: AnalyzeMetadata }

/// `POST /api/v1/compare` request body.
type CompareRequest =
    { [<JsonPropertyName("referencePhonemes")>]
      ReferencePhonemes: PhonemeSegment list
      [<JsonPropertyName("userPhonemes")>]
      UserPhonemes: PhonemeSegment list
      [<JsonPropertyName("locale")>]
      Locale: Locale }

/// `POST /api/v1/compare` response body.
type CompareResponse =
    { [<JsonPropertyName("segments")>]
      Segments: CompareSegment list
      [<JsonPropertyName("coaching")>]
      Coaching: CoachingMessage list }
