# 说明：只读取三份本任务媒体，解析真实文件头、像素、波形及哈希并生成独占交付记录。
import argparse
import hashlib
import json
import math
import struct
import wave
from pathlib import Path
import numpy as np
from PIL import Image, __version__ as pillow_version

PREFIX='UnityProject/Assets/_Project/Media/'

def digest(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()

def dbfs(values):
    return round(20*math.log10(max(float(np.sqrt(np.mean(values**2))),1e-12)),3)

def new_json(path,data):
    with path.open('x',encoding='utf-8',newline='\n') as stream:
        json.dump(data,stream,ensure_ascii=False,indent=2)
        stream.write('\n')

def inspect_audio(file,production):
    raw=file.read_bytes()
    if raw[:4]!=b'RIFF' or raw[8:12]!=b'WAVE':
        raise ValueError('WAV is not a genuine RIFF/WAVE file')
    offset=12
    audio_format=None
    while offset+8<=len(raw):
        chunk,size=struct.unpack_from('<4sI',raw,offset)
        if chunk==b'fmt ':
            audio_format=struct.unpack_from('<H',raw,offset+8)[0]
        offset+=8+size+(size%2)
    if audio_format!=1:
        raise ValueError('WAV must contain linear PCM')
    with wave.open(str(file),'rb') as stream:
        rate=stream.getframerate();channels=stream.getnchannels();bits=stream.getsampwidth()*8;frames=stream.getnframes()
        samples=np.frombuffer(stream.readframes(frames),dtype='<i2').astype(float)/32768
        compression=stream.getcomptype()
    if (rate,channels,bits,compression)!=(44100,1,16,'NONE'):
        raise ValueError('Actual audio does not match the frozen format')
    pause=production['anomalousPause']
    midpoint=production['noiseChangeSeconds']
    windows=[]
    for name,start,end in [('before-noise-change',pause['startSeconds']+0.06,midpoint-0.025),
                           ('after-noise-change',midpoint+0.025,pause['endSeconds']-0.06)]:
        part=samples[round(start*rate):round(end*rate)]
        spectrum=np.abs(np.fft.rfft(part*np.hanning(len(part))))**2
        frequencies=np.fft.rfftfreq(len(part),1/rate)
        centroid=float(np.sum(frequencies*spectrum)/max(np.sum(spectrum),1e-12))
        windows.append({'name':name,'startSeconds':round(start,6),'endSeconds':round(end,6),
                        'rmsDbfs':dbfs(part),'spectralCentroidHz':round(centroid,2)})
    return {'container':'RIFF/WAVE','formatTag':audio_format,'codec':'PCM_S16LE','channels':channels,
        'sampleRate':rate,'bitsPerSample':bits,'sampleFrames':frames,'durationSeconds':frames/rate,
        'fileBytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),
        'peakDbfs':round(20*math.log10(max(float(np.max(np.abs(samples))),1e-12)),3),
        'rmsDbfs':dbfs(samples),'clippedSamples':int(np.count_nonzero((samples<=-1)|(samples>=32767/32768))),
        'pauseNoiseMeasurements':windows,'waveDecoded':True,'listeningReview':'not-performed',
        'pronunciationReview':'synthesis-input-checked; human-listening-not-performed'}

def inspect_image(file):
    with Image.open(file) as image:
        detected=image.format
        image.verify()
    with Image.open(file) as image:
        image.load()
        size=image.size;mode=image.mode
        colors=len(image.convert('RGB').resize((128,128)).getcolors(maxcolors=16384) or [])
    if detected!='PNG' or colors<30:
        raise ValueError('Image must be a genuine decoded nonempty PNG')
    return {'format':detected,'width':size[0],'height':size[1],'mode':mode,
        'fileBytes':file.stat().st_size,'sha256':digest(file),'fullyDecoded':True,
        'uniqueColorsIn128Thumbnail':colors,'visualReview':'reviewed-in-chat; no clipping or unreadable required text'}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('repository',type=Path)
    parser.add_argument('work_directory',type=Path)
    args=parser.parse_args()
    docs=args.repository/'docs/media/S3-A'
    production=json.loads((args.work_directory/'audio-production.json').read_text(encoding='utf-8'))
    images_production=json.loads((args.work_directory/'images-production.json').read_text(encoding='utf-8'))
    audio=inspect_audio(args.repository/(PREFIX+'VoiceLoan/request.wav'),production)
    payee=inspect_image(args.repository/(PREFIX+'VoiceLoan/payee.png'))
    photo=inspect_image(args.repository/(PREFIX+'PhotoPermission/sample-photo.png'))
    if (payee['width'],payee['height'])!=(1080,1080) or (photo['width'],photo['height'])!=(960,1280):
        raise ValueError('Image dimensions changed unexpectedly')
    assets=[]
    for asset_id,media_id,kind,relative,facts,status in [
        ('AUD-001','voice-loan.media.request','Audio','VoiceLoan/request.wav',audio,'pending'),
        ('IMG-VOICE-PAYEE','voice-loan.media.payee','Image','VoiceLoan/payee.png',payee,'self-authored'),
        ('photo-permission.asset.photo',None,'Image','PhotoPermission/sample-photo.png',photo,'self-authored')]:
        item={'assetId':asset_id,'mediaId':media_id,'kind':kind,'path':PREFIX+relative,'sha256':facts['sha256'],
            'durationSeconds':facts.get('durationSeconds',0),'width':facts.get('width',0),'height':facts.get('height',0),
            'transcriptPath':'docs/media/S3-A/request-transcript.txt' if kind=='Audio' else None,
            'subtitlesPath':None,'provenancePath':'docs/media/S3-A/provenance.md','licenseStatus':status,
            'format':facts.get('container',facts.get('format')),'codec':facts.get('codec'),
            'sampleRate':facts.get('sampleRate',0),'bitsPerSample':facts.get('bitsPerSample',0),
            'channels':facts.get('channels',0),'fileBytes':facts['fileBytes'],
            'notes':{'AUD-001':'本机普通 Huihui TTS；无真人参考或克隆。0.72秒句中停顿与原创底噪变化。音频输出分发条款尚未核实，不把本状态视为主窗口批准。',
                     'IMG-VOICE-PAYEE':'原创 SVG 几何/排版，Noto Sans SC OFL 字体渲染；杜禾、800 元、教学标记；没有二维码载荷或真实账户。',
                     'photo-permission.asset.photo':'原创 SVG 抽象虚构人物，非真人、无证件数据；无 Domain mediaId，只供指定照片范围示例。'}[asset_id]}
        assets.append(item)
    inspection={'taskId':'S3-A','inspectionDate':'2026-10-19','scope':'media-file-inspection-only',
        'audio':audio,'payee':payee,'samplePhoto':photo,'audioProduction':production,
        'imageProduction':images_production,'pillowVersion':pillow_version,
        'notVerified':['human listening and pronunciation','Unity import/playback','Android device playback','licensing of Microsoft voice output redistribution','binding and approvedForUse/trainingEnabled']}
    new_json(docs/'manifest.json',{'schemaVersion':1,'taskId':'S3-A','assets':assets})
    new_json(docs/'inspection.json',inspection)
    lines=['语音借款｜实际制作文字稿','mediaId: voice-loan.media.request',
           '实际文件: UnityProject/Assets/_Project/Media/VoiceLoan/request.wav',
           '实测格式: RIFF/WAVE, PCM signed 16-bit little-endian, 44100 Hz, mono',
           '实测时长: 14.000000 秒（617400 个采样帧）','',
           '完整口述：','是我，林澈。我这边急用八百块。收款码发你了，你先转过去。现在不方便接电话，晚点跟你说。','',
           '实际成品时间段（不是原稿拟定时码；由最终音频拼接采样位置计算）：']
    for cue in production['cues']:
        lines.append(f"{cue['startSeconds']:09.6f}–{cue['endSeconds']:09.6f}  {cue['text']}")
    lines += ['',f"{production['anomalousPause']['startSeconds']:.6f}–{production['anomalousPause']['endSeconds']:.6f}  “急用 / 八百块”之间保留 0.720 秒不自然停顿。",
              f"{production['noiseChangeSeconds']:.6f} 秒附近 16 ms 过渡：底噪由较轻高频声底切换到略高的低频声底。",
              '前段 SAPI rate=-2，后两句 rate=-1；同一普通合成音，分段衔接与速度变化作为预设教学线索。',
              '拟定稿 4.8–5.5 秒只是制作指导，本次实际句中停顿更早，以本表为准；全文未改写。',
              '教学模拟说明由播放页持续显示，不把“这是克隆语音”等答案追加到角色口述中。',
              '没有真人参考音、录音、声纹、克隆或模仿对象；Microsoft Huihui Desktop 为本机普通中文 TTS。',
              '已核对合成输入文字和真实文件格式/波形；逐字人工听审及 Unity/真机播放未执行。','']
    with (docs/'request-transcript.txt').open('x',encoding='utf-8',newline='\n') as file:
        file.write('\n'.join(lines))
    print(json.dumps({'audio':audio,'payee':payee,'samplePhoto':photo},ensure_ascii=False,indent=2))

if __name__=='__main__':
    main()
