# Provision Piper TTS for VoiceRay (Windows amd64).
# Output (gitignored): models/piper/bin/, models/piper/voices/
# See docs/providers.md and docs/status.md.

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$PiperDir = Join-Path $RepoRoot "models\piper"
$BinDir = Join-Path $PiperDir "bin"
$VoiceDir = Join-Path $PiperDir "voices"

New-Item -ItemType Directory -Force -Path $BinDir, $VoiceDir | Out-Null

$ReleaseTag = "2023.11.14-2"
$ZipUrl = "https://github.com/rhasspy/piper/releases/download/$ReleaseTag/piper_windows_amd64.zip"
$ZipPath = Join-Path $env:TEMP "piper_windows_amd64.zip"

if (-not (Test-Path (Join-Path $BinDir "piper\piper.exe"))) {
    Write-Host "Downloading Piper $ReleaseTag..."
    Invoke-WebRequest -Uri $ZipUrl -OutFile $ZipPath -UseBasicParsing
    Expand-Archive -Path $ZipPath -DestinationPath $BinDir -Force
    Remove-Item $ZipPath -Force
}

$VoiceBase = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/lessac/medium"
$Onnx = Join-Path $VoiceDir "en_US-lessac-medium.onnx"
$Json = Join-Path $VoiceDir "en_US-lessac-medium.onnx.json"

foreach ($pair in @(
    @{ Url = "$VoiceBase/en_US-lessac-medium.onnx"; Out = $Onnx },
    @{ Url = "$VoiceBase/en_US-lessac-medium.onnx.json"; Out = $Json }
)) {
    if (-not (Test-Path $pair.Out)) {
        Write-Host "Downloading $($pair.Out)..."
        Invoke-WebRequest -Uri $pair.Url -OutFile $pair.Out -UseBasicParsing
    }
}

$Exe = Join-Path $BinDir "piper\piper.exe"
& $Exe --version
"pat" | & $Exe -m $Onnx -f (Join-Path $env:TEMP "voiceray-piper-smoke.wav") -q
if (-not (Test-Path (Join-Path $env:TEMP "voiceray-piper-smoke.wav"))) {
    throw "Piper smoke synthesis failed (stdin text required)."
}
Write-Host "Piper ready: $Exe"
Write-Host "Voice: $Onnx"
