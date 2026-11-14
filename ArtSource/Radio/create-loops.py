"""Original deterministic receiver noise and distant tonal ambience; no sampled music."""
from pathlib import Path
import shutil, wave
import numpy as np

root = Path(__file__).resolve().parents[2]
runtime = root / 'Lighthouse/Assets/Resources/KeeperRadio'
preview = root / 'ArtSource/UI/assets/radio'
runtime.mkdir(parents=True, exist_ok=True)
preview.mkdir(parents=True, exist_ok=True)
rate, seconds = 24000, 16
t = np.arange(rate * seconds, dtype=np.float64) / rate
rng = np.random.default_rng(1468)
noise = rng.normal(0, 1, len(t))
noise = np.convolve(noise, np.array([.12, .2, .36, .2, .12]), mode='same')
noise = np.tanh(noise) * .28
# A periodic tonal bed, with slow amplitude motion and no recognizable melody.
ambient = sum(np.sin(2*np.pi*f*t + .16*np.sin(2*np.pi*t/seconds*(i+1))) * a
              for i,(f,a) in enumerate([(65.4375,.12),(98.125,.075),(130.8125,.06),(196.25,.025)]))
ambient *= .68 + .2*np.sin(2*np.pi*t/seconds)
for name,samples in [('static',noise),('ambient',ambient)]:
    # Noise endpoints meet in silence; tonal loop is periodic.
    if name == 'static':
        fade = int(rate*.025)
        samples[:fade] *= np.linspace(0,1,fade)
        samples[-fade:] *= np.linspace(1,0,fade)
    path = runtime / (name+'.wav')
    with wave.open(str(path),'wb') as out:
        out.setnchannels(1);out.setsampwidth(2);out.setframerate(rate)
        out.writeframes((np.clip(samples,-.95,.95)*32767).astype('<i2').tobytes())
    shutil.copy2(path,preview/path.name)
print('Generated original static and ambient loops.')
