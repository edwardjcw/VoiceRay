"""Transcribe a short WAV clip (openai-whisper when .pt cache exists, else faster-whisper)."""
import json
import os
import sys


def transcribe_openai(cache_dir: str, model_name: str, wav_path: str) -> str:
    import whisper

    model = whisper.load_model(model_name, download_root=cache_dir)
    result = model.transcribe(wav_path, language="en", fp16=False)
    return (result.get("text") or "").strip().lower()


def transcribe_faster(cache_dir: str, model_name: str, wav_path: str) -> str:
    from faster_whisper import WhisperModel

    model = WhisperModel(model_name, device="cpu", compute_type="int8", download_root=cache_dir)
    segments, _info = model.transcribe(wav_path, language="en", beam_size=1, vad_filter=True)
    return " ".join(segment.text.strip() for segment in segments).strip().lower()


def main() -> int:
    if len(sys.argv) != 4:
        print(
            json.dumps({"error": "usage: whisper_transcribe.py <cache_dir> <model> <wav_path>"}),
            file=sys.stderr,
        )
        return 2

    cache_dir, model_name, wav_path = sys.argv[1:4]
    pt_path = os.path.join(cache_dir, f"{model_name}.pt")

    try:
        if os.path.isfile(pt_path):
            text = transcribe_openai(cache_dir, model_name, wav_path)
        else:
            text = transcribe_faster(cache_dir, model_name, wav_path)
    except ImportError:
        print(
            json.dumps(
                {
                    "error": "No Whisper runtime installed",
                    "hint": "py -3.12 -m pip install openai-whisper  (or faster-whisper)",
                }
            ),
            file=sys.stderr,
        )
        return 3
    except Exception as exc:  # noqa: BLE001
        print(json.dumps({"error": str(exc)}), file=sys.stderr)
        return 4

    print(json.dumps({"text": text}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
