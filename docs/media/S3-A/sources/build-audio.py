# 说明：拼接虚构语音，保留不自然停顿并原创合成轻微底噪，输出真实 44.1kHz PCM WAV。
import argparse
import json
import math
import platform
import wave
from pathlib import Path
import numpy as np

RATE = 44100
TARGET_SECONDS = 14.0
SEED = 20261019

def read_voice(file):
    with wave.open(str(file), 'rb') as stream:
        if stream.getnchannels() != 1 or stream.getsampwidth() != 2 or stream.getframerate() != RATE or stream.getcomptype() != 'NONE':
            raise ValueError('Source must be 44.1 kHz 16-bit mono PCM: ' + str(file))
        samples = np.frombuffer(stream.readframes(stream.getnframes()), dtype='<i2').astype(np.float64) / 32768.0
    active = np.flatnonzero(np.abs(samples) > 0.001)
    if not len(active):
        raise ValueError('Source voice is silent: ' + str(file))
    guard = round(RATE * 0.035)
    return samples[max(0, active[0]-guard): min(len(samples),active[-1]+guard+1)]

def stretch(samples, target_length):
    # 普通相位声码器只调整时长，保留原语音音高；没有学习或模仿任何真人声音。
    size, hop = 2048, 256
    window = np.hanning(size)
    padded = np.pad(samples, (size//2, size//2+size))
    frames = np.stack([np.fft.rfft(padded[i:i+size]*window) for i in range(0, len(padded)-size+1, hop)], axis=1)
    factor = len(samples) / target_length
    times = np.arange(0, max(1,frames.shape[1]-2), factor)
    phase = np.angle(frames[:,0])
    advance = 2.0*np.pi*hop*np.arange(frames.shape[0])/size
    output = np.zeros(hop*(len(times)+1)+size)
    weight = np.zeros_like(output)
    for index, time in enumerate(times):
        left = min(int(time),frames.shape[1]-2)
        fraction = time-left
        magnitude = (1.0-fraction)*np.abs(frames[:,left])+fraction*np.abs(frames[:,left+1])
        signal = np.fft.irfft(magnitude*np.exp(1j*phase),n=size)*window
        start = index*hop
        output[start:start+size] += signal
        weight[start:start+size] += window**2
        delta = np.angle(frames[:,left+1])-np.angle(frames[:,left])-advance
        delta -= 2.0*np.pi*np.round(delta/(2.0*np.pi))
        phase += advance+delta
    nonzero = weight > 1e-8
    output[nonzero] /= weight[nonzero]
    output = output[size//2:size//2+target_length]
    if len(output) < target_length:
        output = np.pad(output,(0,target_length-len(output)))
    edge = min(round(RATE*0.012),len(output)//2)
    output[:edge] *= np.linspace(0,1,edge)
    output[-edge:] *= np.linspace(1,0,edge)
    return output

def rms(values):
    return float(np.sqrt(np.mean(np.square(values)))) if len(values) else 0.0

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('repository',type=Path)
    parser.add_argument('work_directory',type=Path)
    args = parser.parse_args()
    info = json.loads((args.work_directory/'voice-source.json').read_text(encoding='utf-8'))
    raw = [read_voice(args.work_directory/item['file']) for item in info['segments']]
    # 句中“急用 / 八百块”的 0.72 秒停顿为可观察的教学异常。
    gaps = [0.10,0.16,0.72,0.18,0.12,0.20]
    desired_speech = round((TARGET_SECONDS-sum(gaps))*RATE)
    natural_lengths = np.array([len(item) for item in raw])
    targets = np.rint(natural_lengths/natural_lengths.sum()*desired_speech).astype(int)
    targets[-1] += desired_speech-int(targets.sum())
    parts = [np.zeros(round(gaps[0]*RATE))]
    cues = []
    position = len(parts[0])
    anomaly = None
    for index, (samples, target, segment) in enumerate(zip(raw,targets,info['segments'])):
        spoken = stretch(samples,int(target))
        peak = np.max(np.abs(spoken))
        spoken *= 0.78 / max(peak,1e-8)
        start = position
        parts.append(spoken)
        position += len(spoken)
        cues.append({'text':segment['text'],'startSeconds':round(start/RATE,6),'endSeconds':round(position/RATE,6),
            'sourceDurationSeconds':round(len(samples)/RATE,6),'timeStretchFactor':round(len(spoken)/len(samples),6),'sapiRate':segment['rate']})
        if index == 1:
            anomaly = {'startSeconds':round(position/RATE,6),'endSeconds':round(position/RATE+gaps[index+1],6),'durationSeconds':gaps[index+1]}
        silence = np.zeros(round(gaps[index+1]*RATE))
        parts.append(silence)
        position += len(silence)
    voice = np.concatenate(parts)
    target_samples = round(TARGET_SECONDS*RATE)
    if len(voice) != target_samples:
        raise ValueError('Duration allocation is not exactly 14 seconds')
    rng = np.random.default_rng(SEED)
    frequencies = np.fft.rfftfreq(target_samples,1/RATE)
    colored=[]
    for low,high in [(600,6500),(140,2600)]:
        spectrum=np.fft.rfft(rng.normal(size=target_samples))
        mask=np.minimum(1,frequencies/max(low,1))*np.exp(-(frequencies/high)**4)
        noise=np.fft.irfft(spectrum*mask,n=target_samples)
        colored.append(noise/max(rms(noise),1e-10))
    change=(anomaly['startSeconds']+anomaly['endSeconds'])/2
    blend=np.clip((np.arange(target_samples)/RATE-change+0.008)/0.016,0,1)
    noise=colored[0]*10**(-49/20)*(1-blend)+colored[1]*10**(-43/20)*blend
    edge=round(RATE*0.03)
    noise[:edge]*=np.linspace(0,1,edge)
    noise[-edge:]*=np.linspace(1,0,edge)
    mixed=voice+noise
    mixed-=mixed.mean()
    peak=float(np.max(np.abs(mixed)))
    if peak>=0.97:
        mixed*=0.95/peak
    pcm=np.rint(np.clip(mixed,-1,1-1/32768)*32768).astype('<i2')
    destination=args.repository/'UnityProject/Assets/_Project/Media/VoiceLoan/request.wav'
    if destination.exists():
        raise FileExistsError('Refusing to overwrite existing audio')
    with destination.open('xb') as file:
        with wave.open(file,'wb') as stream:
            stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(RATE);stream.writeframes(pcm.tobytes())
    facts={'voiceSource':info,'pythonVersion':platform.python_version(),'numpyVersion':np.__version__,
        'sampleRate':RATE,'channels':1,'bitsPerSample':16,'sampleFrames':len(pcm),'durationSeconds':len(pcm)/RATE,
        'anomalousPause':anomaly,'cues':cues,'noiseSeed':SEED,'noiseRmsBeforeDbfs':-49,'noiseRmsAfterDbfs':-43,
        'noiseChangeSeconds':round(change,6),'peakDbfs':round(20*math.log10(max(np.max(np.abs(pcm.astype(float)))/32768,1e-12)),3),
        'rmsDbfs':round(20*math.log10(max(rms(pcm.astype(float)/32768),1e-12)),3),
        'clippedSamples':int(np.count_nonzero((pcm==-32768)|(pcm==32767))),'pitchPreservingTimeFit':'NumPy STFT phase vocoder, Hann 2048 / hop 256'}
    (args.work_directory/'audio-production.json').write_text(json.dumps(facts,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(facts,ensure_ascii=False,indent=2))

if __name__=='__main__':
    main()
