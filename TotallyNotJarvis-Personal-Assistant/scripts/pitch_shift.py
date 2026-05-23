"""
Pitch-shift a WAV file using resampling (numpy only — no extra deps).
Slightly speeds up speech while raising pitch — barely perceptible at small factors.

Usage: python pitch_shift.py <input.wav> [output.wav] [factor]
  factor > 1.0 = higher pitch (e.g. 1.08 = ~1.3 semitones up)
  Overwrites input if no output path given.
"""
import sys
import wave
import numpy as np

def pitch_shift(input_path: str, output_path: str, factor: float = 1.08) -> None:
    with wave.open(input_path, 'rb') as wf:
        nchannels = wf.getnchannels()
        sampwidth = wf.getsampwidth()
        framerate = wf.getframerate()
        frames    = wf.readframes(wf.getnframes())

    dtype = np.int16 if sampwidth == 2 else np.int32
    audio = np.frombuffer(frames, dtype=dtype).astype(np.float64)

    if nchannels == 2:
        audio = audio.reshape(-1, 2)
        new_len = int(len(audio) / factor)
        shifted = np.zeros((new_len, 2), dtype=np.float64)
        idx = np.linspace(0, len(audio) - 1, new_len)
        for ch in range(2):
            shifted[:, ch] = np.interp(idx, np.arange(len(audio)), audio[:, ch])
        out = shifted.flatten()
    else:
        new_len = int(len(audio) / factor)
        idx = np.linspace(0, len(audio) - 1, new_len)
        out = np.interp(idx, np.arange(len(audio)), audio)

    out_int = np.clip(out, np.iinfo(dtype).min, np.iinfo(dtype).max).astype(dtype)

    with wave.open(output_path, 'wb') as wf:
        wf.setnchannels(nchannels)
        wf.setsampwidth(sampwidth)
        wf.setframerate(framerate)
        wf.writeframes(out_int.tobytes())

if __name__ == '__main__':
    inp    = sys.argv[1]
    out    = sys.argv[2] if len(sys.argv) > 2 else inp
    factor = float(sys.argv[3]) if len(sys.argv) > 3 else 1.08
    pitch_shift(inp, out, factor)
