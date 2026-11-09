"""Render and verify the six original project scripts with a local eSpeak NG 1.52.0."""
from argparse import ArgumentParser
from array import array
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import wave

ASSET_NAMES = ("intro-zh", "intro-en", "air-zh", "air-en", "voice-zh", "voice-en")
VOICES = {"zh": ("sit/cmn", 150), "en": ("gmw/en-US", 155)}

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def describe_wave(path):
    with wave.open(str(path), "rb") as wav:
        channels = wav.getnchannels()
        sample_width = wav.getsampwidth()
        rate = wav.getframerate()
        frames = wav.getnframes()
        if channels != 1 or sample_width != 2 or rate != 22050 or wav.getcomptype() != "NONE":
            raise ValueError(f"Unexpected WAV format: {path}")
        samples = array("h", wav.readframes(frames))
    if sys.byteorder == "big":
        samples.byteswap()
    duration = frames / rate
    peak = max(abs(sample) for sample in samples)
    rms = math.sqrt(sum(sample * sample for sample in samples) / frames) / 32768
    if not 3 <= duration <= 16 or peak >= 32767 or rms <= 0.015:
        raise ValueError(f"Invalid duration, clipping or insufficient signal: {path}")
    return {
        "sha256": sha256(path),
        "durationSeconds": round(duration, 6),
        "sampleRateHz": rate,
        "channels": channels,
        "pcmBits": 8 * sample_width,
        "frames": frames,
        "peakSample": peak,
        "rmsFullScale": round(rms, 6),
    }

def render(engine, audio_dir):
    executable = engine / "espeak-ng.exe"
    data_arg = f"--path={engine}"
    version = subprocess.check_output([str(executable), data_arg, "--version"], text=True)
    if "1.52.0" not in version:
        raise ValueError("Expected eSpeak NG 1.52.0")
    for name in ("sit/cmn", "gmw/en-US"):
        voice = engine / "espeak-ng-data" / "lang" / name
        if not voice.is_file() or "mbrola" in voice.read_text(encoding="latin1").lower():
            raise ValueError(f"Expected installed native voice: {name}")
    scripts = json.loads(Path(__file__).with_name("scripts.json").read_text(encoding="utf-8"))
    if set(scripts) != set(ASSET_NAMES):
        raise ValueError("Scripts do not match the resource names")
    results = {}
    audio_dir.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="espeak-render-", dir=engine.parent) as temporary:
        scratch = Path(temporary)
        for name in ASSET_NAMES:
            language = name.rsplit("-", 1)[1]
            voice, rate = VOICES[language]
            script = scratch / f"{name}.txt"
            script.write_text(scripts[name], encoding="utf-8")
            candidate = scratch / f"{name}.wav"
            args = [str(executable), data_arg, "-v", voice, "-s", str(rate),
                    "-a", "82", "-b", "1", "-f", str(script), "-w", str(candidate)]
            subprocess.run(args, check=True, capture_output=True)
            result = describe_wave(candidate)
            output = audio_dir / candidate.name
            if output.exists() and result["sha256"] != sha256(output):
                raise FileExistsError(f"Existing WAV differs from the generated result: {output}")
            if not output.exists():
                output.write_bytes(candidate.read_bytes())
            results[name] = {"voice": voice, "speed": rate, **result}
    return {"engine": "eSpeak NG 1.52.0", "executableSha256": sha256(executable),
            "sourceScriptsSha256": sha256(Path(__file__).with_name("scripts.json")),
            "amplitude": 82, "inputEncoding": "UTF-8", "tracks": results}

def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--engine", type=Path, required=True)
    parser.add_argument("--audio-dir", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(render(args.engine.resolve(), args.audio_dir.resolve()), indent=2))

if __name__ == "__main__":
    main()
