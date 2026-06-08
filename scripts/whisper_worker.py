"""Long-lived Whisper worker — loads the model once, transcribes paths from stdin."""
import json
import sys


def resolve_device(requested: str) -> str:
    import torch

    if requested == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    return requested


def main() -> int:
    if len(sys.argv) < 3:
        print(
            json.dumps({"error": "usage: whisper_worker.py <cache_dir> <model> [device]"}),
            file=sys.stderr,
            flush=True,
        )
        return 2

    cache_dir, model_name = sys.argv[1:3]
    device = resolve_device(sys.argv[3] if len(sys.argv) > 3 else "auto")

    try:
        import whisper
        import torch
    except ImportError:
        print(
            json.dumps(
                {
                    "error": "openai-whisper not installed",
                    "hint": "pip install openai-whisper torch",
                }
            ),
            file=sys.stderr,
            flush=True,
        )
        return 3

    try:
        model = whisper.load_model(model_name, download_root=cache_dir, device=device)
    except Exception as exc:  # noqa: BLE001
        print(json.dumps({"error": f"model load failed: {exc}"}), file=sys.stderr, flush=True)
        return 4

    print(
        json.dumps(
            {
                "status": "ready",
                "model": model_name,
                "device": device,
                "cudaAvailable": bool(torch.cuda.is_available()),
            }
        ),
        flush=True,
    )

    for line in sys.stdin:
        wav_path = line.strip()
        if not wav_path:
            continue

        try:
            result = model.transcribe(
                wav_path,
                language="en",
                fp16=device == "cuda",
            )
            text = (result.get("text") or "").strip().lower()
            print(json.dumps({"text": text}), flush=True)
        except Exception as exc:  # noqa: BLE001
            print(json.dumps({"error": str(exc)}), flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
