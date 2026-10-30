# 说明：用原创矢量几何、独立 SAPI 配音和实测样本边界生成 S3-B 教学视频，不调用 Unity。
from pathlib import Path
import argparse, csv, hashlib, html, json, math, subprocess, tempfile, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
DOC = Path(__file__).resolve().parent
MEDIA = ROOT / 'UnityProject/Assets/_Project/Media/FaceMeeting'
FONT = ROOT / 'UnityProject/Assets/_Project/Art/Fonts/NotoSansSC-Regular.otf'
W, H, FPS, DURATION, SR, SCALE = 720, 1280, 25, 16.0, 48000, 2
TEXTS = [
    '今天临时加个会，用轻会助手参加。',
    '下载链接已经发到聊天里了。',
    '装好后打开短信和通讯录。',
    '我们马上开始，先把工具准备好。',
]
SUB_LINES = [
    ['今天临时加个会，', '用轻会助手参加。'],
    ['下载链接已经发到聊天里了。'],
    ['装好后打开短信和通讯录。'],
    ['我们马上开始，', '先把工具准备好。'],
]
# These placements are edit decisions; subtitle boundaries below come from the actual samples.
PLACEMENTS = [0.320, 4.440, 8.360, 12.040]
INK, GREEN, PAPER, HAIR = '#202923', '#294D42', '#F6F6F2', '#2C3834'

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def read_wav(p):
    with wave.open(str(p), 'rb') as f:
        if (f.getnchannels(), f.getsampwidth(), f.getframerate()) != (1, 2, SR):
            raise ValueError('Unexpected input format: ' + str(p))
        return np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(np.float64) / 32768.0

def write_wav(p, a):
    with wave.open(str(p), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(SR)
        f.writeframes(np.round(np.clip(a, -1, 1)*32767).astype('<i2').tobytes())

def prepare_audio(work):
    result = np.zeros(round(SR * DURATION), dtype=np.float64)
    cues = []
    for i, (line, placement) in enumerate(zip(TEXTS, PLACEMENTS)):
        raw_path = work / f'speech-{i+1}.wav'
        raw = read_wav(raw_path)
        active = np.flatnonzero(np.abs(raw) >= 140/32768)
        if not len(active): raise ValueError('Empty synthesized speech')
        lo, hi = max(0, int(active[0])-960), min(len(raw), int(active[-1])+1441)
        a = raw[lo:hi].copy()
        fade = min(240, len(a)//4)
        a[:fade] *= np.linspace(0, 1, fade); a[-fade:] *= np.linspace(1, 0, fade)
        start = round(placement * SR)
        if start+len(a)>len(result): raise ValueError('Speech exceeds final video')
        result[start:start+len(a)] += a
        speech_start = (start + int(active[0])-lo)/SR
        speech_end = (start + int(active[-1])-lo+1)/SR
        cues.append(dict(index=i+1, text=line, subtitleLines=SUB_LINES[i],
                         rawDurationSeconds=len(raw)/SR, rawActiveStart=active[0]/SR,
                         rawActiveEnd=(active[-1]+1)/SR, placedClipStart=start/SR,
                         placedClipEnd=(start+len(a))/SR, speechStart=speech_start,
                         speechEnd=speech_end, rawSha256=sha(raw_path)))
    write_wav(DOC/'meeting-voice.wav', result)
    return result, cues

def srt_time(t):
    m = int(round(t*1000)); h,m=divmod(m,3600000); minute,m=divmod(m,60000); sec,ms=divmod(m,1000)
    return f'{h:02}:{minute:02}:{sec:02},{ms:03}'

def save_texts(cues):
    blocks=[]
    for c in cues:
        # A short readable hold surrounds the measured activity, never a proposed 4-second block.
        c['subtitleStart'] = max(0, math.floor((c['speechStart']-.060)*1000)/1000)
        c['subtitleEnd'] = min(DURATION, math.ceil((c['speechEnd']+.120)*1000)/1000)
        blocks.append(f"{c['index']}\n{srt_time(c['subtitleStart'])} --> {srt_time(c['subtitleEnd'])}\n"+'\n'.join(c['subtitleLines']))
    text='\n\n'.join(blocks)+'\n'
    for p in [MEDIA/'meeting.srt', DOC/'meeting.srt']:
        p.write_text(text,encoding='utf-8',newline='\n')
    transcript=['会议视频实际文字稿', '虚构人物：许岚；普通合成配音，非真人克隆。', '']
    for c in cues:
        transcript += [f"{srt_time(c['speechStart'])}–{srt_time(c['speechEnd'])}  {c['text']}"]
    transcript += ['', '画面始终显示：虚构情境 · 教学模拟。',
                   '0–4 秒嘴形驱动比实际音轨延后 0.46 秒；4–8 秒转头时局部轮廓漂移；8–12 秒脸与颈部受光方向相反。',
                   '异常是人工制作的教学演示，不是真实换脸或真实鉴定。']
    (DOC/'meeting-transcript.txt').write_text('\n'.join(transcript)+'\n',encoding='utf-8')
    with (DOC/'subtitle-timing.csv').open('w',encoding='utf-8',newline='') as f:
        fields=['index','text','rawDurationSeconds','rawActiveStart','rawActiveEnd','placedClipStart','placedClipEnd','speechStart','speechEnd','subtitleStart','subtitleEnd','rawSha256']
        writer=csv.DictWriter(f,fields,extrasaction='ignore'); writer.writeheader(); writer.writerows(cues)

FONTS={}
def font(n):
    n=round(n*SCALE)
    if n not in FONTS: FONTS[n]=ImageFont.truetype(str(FONT),n)
    return FONTS[n]

def mix(a,b,p):
    aa=[int(a[i:i+2],16) for i in (1,3,5)]; bb=[int(b[i:i+2],16) for i in (1,3,5)]
    return '#'+''.join(f'{round(x+(y-x)*p):02x}' for x,y in zip(aa,bb))

class VectorScene:
    """One original vector scene, rasterized for encoding and serializable as editable SVG."""
    def __init__(self):
        self.im=Image.new('RGB',(W*SCALE,H*SCALE),PAPER)
        self.draw=ImageDraw.Draw(self.im); self.svg=[]
    def rect(self,box,fill,radius=0):
        b=tuple(round(x*SCALE) for x in box)
        if radius: self.draw.rounded_rectangle(b,radius=round(radius*SCALE),fill=fill)
        else: self.draw.rectangle(b,fill=fill)
        x,y,x2,y2=box
        self.svg.append(f'<rect x="{x:.2f}" y="{y:.2f}" width="{x2-x:.2f}" height="{y2-y:.2f}" rx="{radius}" fill="{fill}"/>')
    def ellipse(self,box,fill):
        self.draw.ellipse(tuple(round(x*SCALE) for x in box),fill=fill)
        x,y,x2,y2=box
        self.svg.append(f'<ellipse cx="{(x+x2)/2:.2f}" cy="{(y+y2)/2:.2f}" rx="{(x2-x)/2:.2f}" ry="{(y2-y)/2:.2f}" fill="{fill}"/>')
    def poly(self,points,fill):
        self.draw.polygon([(round(x*SCALE),round(y*SCALE)) for x,y in points],fill=fill)
        self.svg.append('<polygon points="'+' '.join(f'{x:.2f},{y:.2f}' for x,y in points)+f'" fill="{fill}"/>')
    def line(self,points,fill,width=2):
        self.draw.line([(round(x*SCALE),round(y*SCALE)) for x,y in points],fill=fill,width=round(width*SCALE),joint='curve')
        self.svg.append('<polyline points="'+' '.join(f'{x:.2f},{y:.2f}' for x,y in points)+f'" stroke="{fill}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round" fill="none"/>')
    def text(self,xy,text,size=28,fill=INK,center=False):
        f=font(size); x,y=xy
        if center: x-=self.draw.textlength(text,font=f)/(2*SCALE)
        self.draw.text((round(x*SCALE),round(y*SCALE)),text,font=f,fill=fill,anchor='lt')
        self.svg.append(f'<text x="{x:.2f}" y="{y:.2f}" font-family="Noto Sans SC, sans-serif" font-size="{size}" fill="{fill}" dominant-baseline="text-before-edge">{html.escape(text)}</text>')
    def export_svg(self,p):
        header='<!-- 说明：原创许岚矢量角色与会议画面，几何由 render_meeting.py 生成，无真人或第三方角色参考。 -->\n'
        Path(p).write_text(header+f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">\n'+'\n'.join(self.svg)+'\n</svg>\n',encoding='utf-8')
    def image(self):
        return self.im.resize((W,H),Image.Resampling.LANCZOS)

def envelope(audio,t):
    c=round(t*SR)
    if c<0 or c>=len(audio): return 0.0
    a=audio[max(0,c-720):min(len(audio),c+720)]
    return float(np.sqrt(np.mean(a*a)))

def features(t,audio):
    delay=.46 if t<4.0 else 0
    e=envelope(audio,t-delay)
    opening=0 if e<.008 else min(1,(e/.17)**.8)
    turn=0; edge=0
    if 4.15<t<7.90:
        phase=(t-4.15)/3.75
        turn=21*math.sin(2*math.pi*phase)
        edge=(7+5*math.sin(t*math.pi*5.4))*math.sin(math.pi*phase)**.6
    light=0
    if 8.0<t<12.0:
        light=min(1,(t-8.0)/.35,(12.0-t)/.35)
    return dict(delay=delay,opening=opening,turn=turn,edge=edge,light=light,energy=e)

def render(t,audio,cues,save_svg=None):
    f=features(t,audio); s=VectorScene()
    # Quiet original studio geometry, never a real conferencing platform.
    s.rect((0,0,720,80),GREEN)
    s.text((42,24),'虚构情境 · 教学模拟',30,'#FFFFFF')
    s.text((42,118),'会议说明',48)
    s.text((44,185),'青栎工作室',26,'#626D64')
    s.rect((42,242,678,1007),'#E7EFE8',22)
    s.rect((67,267,653,545),'#F2F3EC',10)
    s.line([(460,268),(460,543)],'#D5DDD2',3)
    s.line([(69,436),(652,436)],'#D5DDD2',3)
    # Plant and books are drawn here from simple polygons and ellipses.
    s.poly([(567,798),(613,798),(605,853),(575,853)],'#BC8C70')
    s.line([(589,799),(586,650)],'#6D866B',5)
    s.ellipse((550,668,587,724),'#859C78'); s.ellipse((587,697,627,752),'#6D866B')
    s.ellipse((555,732,589,775),'#A1AF8B')
    s.rect((91,848,177,860),'#AC7B65',3); s.rect((100,832,174,845),'#BFC6B2',3)
    # Body, collar and neck are separate vectors so opposing illumination is visible.
    s.poly([(146,1007),(159,841),(189,780),(276,728),(321,716),(399,716),(444,730),(532,780),(561,841),(575,1007)],GREEN)
    s.poly([(301,720),(360,760),(419,720),(411,817),(309,817)],'#F3EDE0')
    s.poly([(324,613),(396,613),(405,727),(360,760),(316,727)],'#C48E73')
    neck_l=mix('#CB957B','#8C5148',f['light']); neck_r=mix('#D9A388','#F3C498',f['light'])
    s.poly([(324,613),(360,613),(360,758),(316,727)],neck_l)
    s.poly([(360,613),(396,613),(405,727),(360,758)],neck_r)
    s.poly([(301,717),(354,758),(328,807),(282,747)],'#F2F0E7')
    s.poly([(419,717),(366,758),(392,807),(438,747)],'#F2F0E7')
    s.line([(360,817),(360,994)],'#42675A',3)
    # Head turn is a position/perspective change; local edge instability is independent.
    dx=f['turn']; cx=360+dx; turn=dx/21
    s.ellipse((cx-148,301,cx+149,667),HAIR)
    s.poly([(cx-145,435),(cx-124,686),(cx-103,712),(cx-96,563)],HAIR)
    s.poly([(cx+145,435),(cx+124,686),(cx+103,712),(cx+96,563)],HAIR)
    s.ellipse((cx-127,343,cx-87,434),HAIR)
    s.ellipse((cx-130,455,cx-89,533),'#CF967B')
    s.ellipse((cx+91,455,cx+128,533),'#CF967B')
    face=[(cx-109,395),(cx-88,357),(cx-30,336),(cx+54,344),(cx+103,390),(cx+113,486),(cx+102,565),(cx+70,621),(cx+27,651),(cx-22,651),(cx-73,620),(cx-104,565),(cx-115,487)]
    s.poly(face,'#DCA184')
    # Broad, opposite-facing highlight shapes make lighting directional rather than just skin tone.
    left=mix('#EBC0A1','#FFE2B6',f['light']); right=mix('#DFA386','#BD745E',f['light'])
    s.poly([(cx-105,397),(cx-67,359),(cx-15,347),(cx+5,422),(cx-3,644),(cx-65,613),(cx-98,562),(cx-110,487)],left)
    s.poly([(cx+5,360),(cx+58,352),(cx+99,399),(cx+109,488),(cx+100,558),(cx+68,617),(cx+17,646)],right)
    if f['edge']>.1:
        v=f['edge']; jag=[]
        for i,y in enumerate(range(423,583,16)):
            x=cx+107+v*(.35+.65*math.sin(i*1.7+t*11))
            jag.append((x,y))
        s.poly([(cx+97,417)]+jag+[(cx+90,590)],'#EFCCB1')
        s.line(jag,'#B17A68',2)
    # Hair sweep and original bob silhouette.
    s.poly([(cx-129,444),(cx-126,364),(cx-92,316),(cx-41,295),(cx+41,297),(cx+112,336),(cx+137,400),(cx+123,474),(cx+97,413),(cx+76,366),(cx+25,363),(cx-30,389),(cx-94,411)],HAIR)
    s.line([(cx-91,352),(cx-48,326),(cx+9,321)],'#4A5750',5)
    s.ellipse((cx-115,525,cx-103,539),'#B3755A')
    s.ellipse((cx+103,525,cx+115,539),'#B3755A')
    # Brows, eyes and nose follow the same turn, avoiding a frozen cutout.
    ex=turn*9
    s.line([(cx-77+ex,450),(cx-52+ex,445),(cx-29+ex,449)],'#55423C',5)
    s.line([(cx+23+ex,449),(cx+47+ex,445),(cx+70+ex,450)],'#55423C',5)
    blink=abs((t-2.9)%5.4)<.075
    for ox in (-53,48):
        if blink: s.line([(cx+ox-14+ex,480),(cx+ox+13+ex,480)],INK,4)
        else:
            s.ellipse((cx+ox-16+ex,468,cx+ox+16+ex,486),'#F9F6EA')
            s.ellipse((cx+ox-7+ex,466,cx+ox+5+ex,486),'#35453C')
            s.ellipse((cx+ox-3+ex,469,cx+ox+1+ex,473),'#FFFFFF')
    s.line([(cx+ex,481),(cx-5+ex,526),(cx+10+ex,529)],'#B37862',4)
    s.ellipse((cx-84+ex,524,cx-46+ex,541),'#E3A58C')
    s.ellipse((cx+42+ex,524,cx+79+ex,541),'#D9937C')
    mx=cx+ex; my=575
    if f['opening']<.03:
        s.line([(mx-22,my),(mx-4,my+3),(mx+20,my)],'#8C4E49',4)
    else:
        mh=5+34*f['opening']; mw=23+5*math.sin(t*22)**2
        s.ellipse((mx-mw,my-mh/2,mx+mw,my+mh/2),'#713F3D')
        s.ellipse((mx-mw+4,my-mh/2+2,mx+mw-4,my-mh/2+7),'#F9ECE0')
        if mh>18: s.ellipse((mx-12,my+2,mx+13,my+mh/2-3),'#BC706B')
    # Minimal information hierarchy, persistent teaching label above and no answer labels.
    s.text((44,1038),'许岚（虚构角色）',28)
    s.line([(44,1093),(676,1093)],'#D6DDD1',2)
    for c in cues:
        if c['subtitleStart']<=t<c['subtitleEnd']:
            for j,line in enumerate(c['subtitleLines']): s.text((360,1124+52*j),line,36,GREEN,center=True)
    s.text((44,1233),'普通合成配音 · 无真人素材',21,'#626D64')
    if save_svg: s.export_svg(save_svg)
    return s.image(), f

def build(ffmpeg,work):
    MEDIA.mkdir(parents=True,exist_ok=True)
    audio,cues=prepare_audio(work); save_texts(cues)
    render(1.5,audio,cues,DOC/'character.svg')
    command=[str(ffmpeg),'-hide_banner','-loglevel','warning','-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','pipe:0','-i',str(DOC/'meeting-voice.wav'),'-map','0:v:0','-map','1:a:0','-c:v','libx264','-preset','medium','-crf','20','-profile:v','main','-level:v','3.1','-pix_fmt','yuv420p','-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-c:a','aac','-b:a','128k','-ar',str(SR),'-ac','1','-t',str(DURATION),'-movflags','+faststart',str(MEDIA/'meeting.mp4')]
    (DOC/'encoding-command.txt').write_text(subprocess.list2cmdline(command)+'\n',encoding='utf-8')
    with (DOC/'motion-cues.csv').open('w',newline='',encoding='utf-8') as file:
        writer=csv.writer(file); writer.writerow(['frame','timeSeconds','audioRms','mouthDriveRms','mouthDelaySeconds','mouthOpening','headTurnPixels','edgeOffsetPixels','lightingBlend'])
        proc=subprocess.Popen(command,stdin=subprocess.PIPE,stderr=(work/'encode-stderr.txt').open('w',encoding='utf-8'))
        try:
            for frame in range(round(DURATION*FPS)):
                t=frame/FPS; im,fx=render(t,audio,cues)
                proc.stdin.write(im.tobytes())
                writer.writerow([frame,f'{t:.3f}',f'{envelope(audio,t):.6f}',f"{fx['energy']:.6f}",fx['delay'],f"{fx['opening']:.5f}",f"{fx['turn']:.5f}",f"{fx['edge']:.5f}",f"{fx['light']:.5f}"])
                if frame%50==0: print(f'rendered {frame}/{round(DURATION*FPS)}',flush=True)
        finally:
            proc.stdin.close()
        code=proc.wait()
        if code: raise RuntimeError((work/'encode-stderr.txt').read_text(encoding='utf-8'))
    (DOC/'build-measurements.json').write_text(json.dumps(dict(width=W,height=H,fps=FPS,durationSeconds=DURATION,sampleRate=SR,cues=cues,fontSha256=sha(FONT),videoSha256=sha(MEDIA/'meeting.mp4'),voiceSha256=sha(DOC/'meeting-voice.wav')),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('video and aligned subtitles written',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--ffmpeg',type=Path,required=True); parser.add_argument('--work-temp',type=Path,default=Path(tempfile.gettempdir())/'zhijian-S3-B-20261019'); args=parser.parse_args()
    build(args.ffmpeg,args.work_temp)
