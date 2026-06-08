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

/// Articulatory pose: normalized (0..1) articulator parameters that the
/// frontend rig converts into sagittal geometry. Decouples phonetics (backend)
/// from rig geometry (frontend `SagittalPlayer`).
type ArticulatoryPose =
    { /// Mandible aperture: 0 = closed, 1 = wide open.
      [<JsonPropertyName("jawOpen")>]
      JawOpen: float
      /// Tongue body height: 0 = low, 1 = high (close to palate).
      [<JsonPropertyName("tongueHeight")>]
      TongueHeight: float
      /// Tongue body advancement: 0 = front, 1 = back (toward pharynx).
      [<JsonPropertyName("tongueBackness")>]
      TongueBackness: float
      /// Tongue tip raising toward the alveolar ridge (coronals): 0..1.
      [<JsonPropertyName("tongueTip")>]
      TongueTip: float
      /// Tongue tip protrusion between the teeth (interdental θ/ð): 0..1.
      [<JsonPropertyName("interdental")>]
      Interdental: float
      /// Lip rounding/protrusion: 0 = spread/neutral, 1 = rounded.
      [<JsonPropertyName("lipRounding")>]
      LipRounding: float
      /// Lip closure (bilabials): 0 = open, 1 = sealed.
      [<JsonPropertyName("lipClosure")>]
      LipClosure: float
      /// Velum (soft palate): 0 = raised/oral, 1 = lowered/nasal.
      [<JsonPropertyName("velum")>]
      Velum: float }

/// Animation keyframe for one phoneme window on the sagittal rig.
type ArticulatoryKeyframe =
    { [<JsonPropertyName("ipa")>]
      Ipa: string
      [<JsonPropertyName("startMs")>]
      StartMs: int
      [<JsonPropertyName("endMs")>]
      EndMs: int
      [<JsonPropertyName("pose")>]
      Pose: ArticulatoryPose
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
