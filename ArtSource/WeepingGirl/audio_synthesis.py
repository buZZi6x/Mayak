"""Original, deterministic apparition sound design; no sampled voices or third-party audio."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Lighthouse/Assets/Art/WeepingGirl/Audio'
OUT.mkdir(parents=True, exist_ok=True)
RATE = 44100
rng = np.random.default_rng(11142026)
evidence = []

def colored_noise(n, low, high):
    raw = rng.normal(0, 1, n)
    f = np.fft.rfftfreq(n, 1/RATE)
    mask = (1-np.exp(-(f/max(1, low))**4))*np.exp(-(f/high)**4)
    signal = np.fft.irfft(np.fft.rfft(raw)*mask, n)
    return signal / max(.01, np.std(signal))

def voice(t, pitch, formants):
    phase = np.cumsum(pitch)/RATE
    result = np.zeros(len(t))
    for harmonic in range(1, 32):
        freq = pitch*harmonic
        gain = sum(np.exp(-((freq-center)/width)**2)*weight for center,width,weight in formants)
        result += np.sin(2*np.pi*phase*harmonic)*(gain+.025)/harmonic**.55
    return result

def save(name, sound, peak=.8, stereo=False):
    # Soft limiting and short endpoint fades keep transients loud without clipped samples.
    sound = np.tanh(sound)
    fade = min(2205, len(sound)//10)
    envelope = np.ones(len(sound)); envelope[:fade] = np.linspace(0,1,fade); envelope[-fade:] = np.linspace(1,0,fade)
    sound *= envelope[:,None] if sound.ndim == 2 else envelope
    sound *= peak / max(.00001, np.max(np.abs(sound)))
    if stereo and sound.ndim == 1:
        delayed = np.roll(sound, int(.011*RATE))
        sound = np.column_stack((sound*.9+delayed*.1,sound*.85-delayed*.15))
    with wave.open(str(OUT/name),'wb') as output:
        output.setnchannels(2 if sound.ndim == 2 else 1); output.setsampwidth(2); output.setframerate(RATE)
        output.writeframes((sound*32767).astype('<i2').tobytes())
    evidence.append(dict(file=name,seconds=round(len(sound)/RATE,3),peak=float(np.max(np.abs(sound))),rms=float(np.sqrt(np.mean(sound**2))),clippedSamples=int(np.count_nonzero(np.abs(sound)>=.999))))

# Three broken, breathy sobs with uneven pitch collapse; roomless, spatialized in Unity.
t = np.arange(int(8.6*RATE))/RATE
cry = np.zeros(len(t))
for onset,length,p in [(0.35,1.25,367),(2.05,.72,414),(3.42,1.85,355),(6.12,.93,392)]:
    local=t-onset; active=(local>=0)&(local<length); s=local[active]; n=len(s)
    pitch=p-65*(s/length)+12*np.sin(s*31)+5*np.sin(s*71)
    voiced=voice(s,pitch,[(610,220,.9),(1300,290,.52),(2900,470,.18)])
    sob=np.maximum(0,np.sin(s*np.pi/length))**1.4 * (.76+.24*np.sin(s*28))
    cry[active] += (voiced*.24+colored_noise(n,850,4600)*.025)*sob
    inhale=(local>length)&(local<length+.32); b=local[inhale]-length
    if len(b): cry[inhale] += colored_noise(len(b),1300,6500)*np.sin(b*np.pi/.32)*.023
for delay,amount in [(.087,.12),(.19,.065),(.33,.035)]:
    count=int(delay*RATE); cry[count:]+=cry[:-count]*amount
save('Girl_Weeping.wav',cry,.49)

# Ascending electrical strain, deliberately quiet enough to retain the world underneath.
t=np.arange(int(1.6*RATE))/RATE
signal=colored_noise(len(t),80,6800)*(.08+.10*t/t[-1])
signal+=.08*np.sin(2*np.pi*(46*t+24*t*t))+.045*np.sin(2*np.pi*113*t)
signal*=np.minimum(1,t/.025)*(1-.4*np.clip((t-1.1)/.5,0,1))
save('Girl_Signal.wav',signal,.64,True)

# Inhale/inhuman vocal rasp -> sub impact at .38s -> decaying metallic tail.
t=np.arange(int(1.8*RATE))/RATE
attack=np.zeros(len(t))
wind=colored_noise(len(t),240,6700)
attack+=wind*.16*np.exp(-((t-.23)/.18)**2)
pitch=510+170*np.exp(-((t-.31)/.15)**2)-210*np.clip(t/.9,0,1)
rasp=voice(t,pitch,[(770,290,1),(1700,380,.58),(3100,600,.35)])
attack+=rasp*.26*np.minimum(1,t/.07)*np.exp(-np.maximum(0,t-.35)*5)
impact=np.maximum(t-.38,0); env=(t>=.38)*np.exp(-impact*9)
attack+=env*(.66*np.sin(2*np.pi*(72*impact-18*impact*impact))+.3*colored_noise(len(t),50,2100))
attack+=np.sin(2*np.pi*1873*t)*.035*np.exp(-np.maximum(0,t-.38)*4)*(t>=.38)
save('Girl_Lunge.wav',attack,.88,True)
(ROOT/'Artifacts/WeepingGirl/audio-evidence.json').write_text(json.dumps(evidence,indent=2),encoding='utf8')
print(json.dumps(evidence,indent=2))
