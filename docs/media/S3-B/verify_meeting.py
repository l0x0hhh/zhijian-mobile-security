# 说明：实际探测和解码 S3-B 成片，核对字幕活动范围并提取关键帧；不运行 Unity 或工程测试。
from pathlib import Path
import argparse, hashlib, json, re, subprocess, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[3]
DOC=Path(__file__).resolve().parent
VIDEO=ROOT/'UnityProject/Assets/_Project/Media/FaceMeeting/meeting.mp4'
SRT=VIDEO.with_suffix('.srt')
FONT=ROOT/'UnityProject/Assets/_Project/Art/Fonts/NotoSansSC-Regular.otf'

def run(args):
    p=subprocess.run([str(x) for x in args],capture_output=True,text=True,encoding='utf-8',errors='replace')
    if p.returncode: raise RuntimeError(p.stderr)
    return p.stdout

def wav(p):
    with wave.open(str(p),'rb') as f:
        return np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(np.float64)/32768

def stamp(s):
    h,m,sec,ms=map(int,re.split('[:,]',s)); return h*3600+m*60+sec+ms/1000

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def main(ffmpeg,ffprobe,work):
    out=DOC/'verification'; out.mkdir(exist_ok=True)
    info=json.loads(run([ffprobe,'-v','error','-show_streams','-show_format','-of','json',VIDEO]))
    (out/'ffprobe.json').write_text(json.dumps(info,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    video=next(s for s in info['streams'] if s['codec_type']=='video')
    audio=next(s for s in info['streams'] if s['codec_type']=='audio')
    assert video['codec_name']=='h264' and video['pix_fmt']=='yuv420p'
    assert (video['width'],video['height'],video['avg_frame_rate'],int(video['nb_frames']))==(720,1280,'25/1',400)
    assert audio['codec_name']=='aac' and audio['sample_rate']=='48000' and audio['channels']==1
    assert float(video['duration'])==16.0 and float(audio['duration'])==16.0
    assert VIDEO.read_bytes()[4:8]==b'ftyp'
    run([ffmpeg,'-hide_banner','-v','error','-i',VIDEO,'-f','null','-'])
    decoded=work/'decoded-meeting.wav'
    run([ffmpeg,'-v','error','-y','-i',VIDEO,'-map','0:a:0','-c:a','pcm_s16le','-ar','48000','-ac','1',decoded])
    a=wav(decoded); original=wav(DOC/'meeting-voice.wav')
    n=min(len(a),len(original)); correlation=float(np.corrcoef(a[:n],original[:n])[0,1])
    blocks=re.findall(r'(\d+)\n(\d\d:\d\d:\d\d,\d\d\d) --> (\d\d:\d\d:\d\d,\d\d\d)\n([^\n]+(?:\n[^\n]+)*)',SRT.read_text(encoding='utf-8').strip())
    assert len(blocks)==4 and SRT.read_bytes()==(DOC/'meeting.srt').read_bytes()
    checks=[]; previous=0
    for num,start,end,text in blocks:
        lo,hi=stamp(start),stamp(end)
        assert 0<=lo<hi<=16 and lo>=previous; previous=hi
        # Activity is measured from the final decoded AAC, not the intended edit timings.
        left=max(0,round((lo-.15)*48000)); right=min(len(a),round((hi+.15)*48000))
        active=np.flatnonzero(np.abs(a[left:right])>=140/32768)
        assert len(active)>0
        first=(left+int(active[0]))/48000; last=(left+int(active[-1])+1)/48000
        contained=lo<=first and last<=hi
        assert contained, f'Subtitle {num} misses decoded activity'
        checks.append(dict(index=int(num),subtitleStart=lo,subtitleEnd=hi,decodedActiveStart=first,decodedActiveEnd=last,enclosesDetectedActivity=contained,text=text))
    times=[.52,.96,4.80,5.04,9.44,13.00]; frames=[]
    for t in times:
        p=work/f'frame-{t:.2f}.png'
        run([ffmpeg,'-v','error','-y','-ss',str(t),'-i',VIDEO,'-frames:v','1',p])
        frames.append(Image.open(p).convert('RGB'))
    f=ImageFont.truetype(str(FONT),24)
    sheet=Image.new('RGB',(720,3*680),'#F6F6F2'); draw=ImageDraw.Draw(sheet)
    for i,(t,im) in enumerate(zip(times,frames)):
        x=(i%2)*360; y=(i//2)*680
        draw.text((x+12,y+8),f'{t:.2f} 秒 · 实际解码帧',font=f,fill='#202923')
        sheet.paste(im.resize((360,640),Image.Resampling.LANCZOS),(x,y+40))
    sheet.save(out/'contact-sheet.jpg',quality=92)
    detail=Image.new('RGB',(960,1120),'#F6F6F2'); d=ImageDraw.Draw(detail)
    d.text((24,16),'实际成片局部：0.52 / 0.96 秒',font=f,fill='#202923')
    for i in range(2):
        crop=frames[i].crop((285,530,435,620)).resize((420,252),Image.Resampling.NEAREST)
        detail.paste(crop,(30+i*480,64))
    d.text((24,338),'转头局部：4.80 / 5.04 秒',font=f,fill='#202923')
    for i in range(2):
        crop=frames[i+2].crop((415,407,510,620)).resize((300,672),Image.Resampling.NEAREST)
        detail.paste(crop,(30+i*330,392))
    d.text((665,338),'9.44 秒',font=f,fill='#202923')
    crop=frames[4].crop((260,387,450,762)).resize((266,525),Image.Resampling.LANCZOS)
    detail.paste(crop,(670,392)); detail.save(out/'anomaly-detail.png')
    report=dict(container='MP4 / isom',durationSeconds=16.0,width=720,height=1280,frames=400,fps='25/1',
                videoCodec=video['codec_name'],videoProfile=video['profile'],pixelFormat=video['pix_fmt'],
                audioCodec=audio['codec_name'],audioProfile=audio['profile'],audioSampleRate=48000,audioChannels=1,
                fullDecodeExitCode=0,decodedAudioSamples=len(a),sourceAudioSamples=len(original),sourceDecodedCorrelation=correlation,
                subtitleCueCount=len(blocks),subtitleMaxEndSeconds=max(c['subtitleEnd'] for c in checks),
                subtitleCopyIdentical=True,subtitleActivityChecks=checks,keyframeTimes=times,
                videoBytes=VIDEO.stat().st_size,videoSha256=sha(VIDEO),subtitleSha256=sha(SRT),
                unityPlaybackVerified=False,androidPlaybackVerified=False,
                limits=['音量阈值用于检查字幕覆盖语音活动，不是逐字语音识别或人工听审。','三异常还需查看关键帧与动态播放，源码参数不是实际鉴定结果。'])
    (out/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--ffmpeg',type=Path,required=True);p.add_argument('--ffprobe',type=Path,required=True);p.add_argument('--work-temp',type=Path,required=True);a=p.parse_args();main(a.ffmpeg,a.ffprobe,a.work_temp)
