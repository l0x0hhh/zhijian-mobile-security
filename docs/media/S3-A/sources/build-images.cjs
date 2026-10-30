// 说明：用原创 SVG 几何与已入库 OFL 中文字体制作教学收款图、抽象人物图及真实 PNG。
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const [repo, modules, mode] = process.argv.slice(2);
const replaceOwn = mode === '--replace-own';
if (!repo || !modules) throw new Error('Usage: node build-images.cjs <repository> <existing-node-modules>');
const sharp = require(path.join(modules, 'sharp'));
const font = path.join(repo, 'UnityProject/Assets/_Project/Art/Fonts/NotoSansSC-Regular.otf');
const sourceDir = __dirname;
const base = path.join(repo, 'UnityProject/Assets/_Project/Media');
const escape = x => x.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const text = (value, x, y, size, fill='#202923') => ({value,x,y,size,fill});
const payee = {
  name: 'payee', width:1080, height:1080, destination:'VoiceLoan/payee.png',
  shapes:`<rect width="1080" height="1080" fill="#F6F6F2"/>
<path d="M80 210H1000M80 748H1000" stroke="#D6DDD4" stroke-width="2"/>
<rect x="805" y="76" width="195" height="68" rx="18" fill="#E7EFE8"/>
<circle cx="112" cy="115" r="30" fill="none" stroke="#294D42" stroke-width="4"/>
<path d="M99 114l10 10 18-22" fill="none" stroke="#294D42" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
<rect x="80" y="800" width="920" height="140" rx="20" fill="#ECEFE9"/>
<path d="M116 841h66v62h-66zM127 852l44 40M171 852l-44 40" fill="none" stroke="#626D64" stroke-width="4" stroke-linecap="round"/>
<path d="M80 978H1000" stroke="#D6DDD4" stroke-width="2"/>`,
  texts:[text('收款信息',168,82,58),text('教学示意',839,94,31,'#294D42'),
    text('收款人',80,262,38,'#626D64'),text('杜禾',76,330,116),
    text('请求金额',80,506,38,'#626D64'),text('800 元',76,576,110),
    text('教学收款码 · 不可扫描',220,838,43,'#294D42'),
    text('虚构收款信息 · 不涉及真实账户或支付',80,1006,32,'#626D64')]
};
const photo = {
  name:'sample-photo',width:960,height:1280,destination:'PhotoPermission/sample-photo.png',
  shapes:`<defs><linearGradient id="paper" x2="0" y2="1"><stop stop-color="#E6EEE8"/><stop offset="1" stop-color="#CBDDD2"/></linearGradient>
<linearGradient id="shirt" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#375D50"/><stop offset="1" stop-color="#25483C"/></linearGradient></defs>
<rect width="960" height="1280" fill="#F6F6F2"/>
<rect x="48" y="140" width="864" height="1034" rx="24" fill="url(#paper)"/>
<circle cx="480" cy="563" r="285" fill="#D9E6DC"/>
<path d="M183 1174c9-157 27-278 127-327 53-27 99-38 170-39 71 1 117 12 170 39 100 49 118 170 127 327" fill="url(#shirt)"/>
<path d="M415 730h130l7 106-72 94-72-94z" fill="#C99679"/>
<path d="M408 807l72 88-82 75-49-133zM552 807l-72 88 82 75 49-133z" fill="#E9EEE7"/>
<path d="M270 536c-12-171 39-283 205-289 174-7 239 131 219 303l-10 216-122 28-171 3-111-48z" fill="#303831"/>
<ellipse cx="299" cy="581" rx="31" ry="54" fill="#D4A182"/><ellipse cx="661" cy="581" rx="31" ry="54" fill="#D4A182"/>
<path d="M317 497c6-111 75-168 163-170 96 1 157 59 164 170l-10 138c-10 84-78 143-154 151-76-8-144-67-154-151z" fill="#DDB191"/>
<path d="M304 519c-2-165 65-241 179-240 111 0 191 86 174 235-57-33-90-86-111-141-42 63-120 98-242 146z" fill="#303831"/>
<path d="M374 553q35-17 63-2M523 551q28-15 63 2" fill="none" stroke="#3F4238" stroke-width="12" stroke-linecap="round"/>
<ellipse cx="410" cy="587" rx="9" ry="12" fill="#303831"/><ellipse cx="550" cy="587" rx="9" ry="12" fill="#303831"/>
<path d="M479 589l-13 65 24 2" fill="none" stroke="#BF8B70" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M438 699q42 19 84-1" fill="none" stroke="#995E53" stroke-width="8" stroke-linecap="round"/>
<path d="M247 1038v136M713 1038v136M480 960v214" fill="none" stroke="#426657" stroke-width="5"/>
<rect x="48" y="1074" width="864" height="100" rx="0" fill="#25483C"/>
<path d="M48 1148v2q0 24 24 24h816q24 0 24-24v-2" fill="#25483C"/>`,
  texts:[text('示例照片',48,60,49),text('原创虚构人物',602,76,32,'#626D64'),
    text('教学示意 · 非真人证件照',197,1106,38,'#FFFFFF'),
    text('仅演示指定照片范围，不含真实身份资料',137,1213,32,'#626D64')]
};
async function build(spec) {
  const vectorTexts=spec.texts.map(t=>`<text x="${t.x}" y="${t.y+t.size*.89}" font-family="Noto Sans SC" font-size="${t.size}" fill="${t.fill}">${escape(t.value)}</text>`).join('\n');
  const start=`<?xml version="1.0" encoding="UTF-8"?>\n<!-- 说明：原创教学矢量图；文字使用项目已有的 Noto Sans SC，许可见制作记录。 -->\n<svg xmlns="http://www.w3.org/2000/svg" width="${spec.width}" height="${spec.height}" viewBox="0 0 ${spec.width} ${spec.height}">\n`;
  const svg=start+spec.shapes+'\n'+vectorTexts+'\n</svg>\n';
  const source=path.join(sourceDir,spec.name+'.svg');
  const destination=path.join(base,spec.destination);
  if (!replaceOwn && (fs.existsSync(source)||fs.existsSync(destination))) throw new Error('Refusing to overwrite an existing source or asset: '+spec.name);
  fs.writeFileSync(source,svg,{flag:replaceOwn ? 'w' : 'wx'});
  const overlays=[]; const boxes=[];
  for (const t of spec.texts) {
    const tile=await sharp({text:{text:`<span foreground="${t.fill}">${escape(t.value)}</span>`,font:`Noto Sans SC ${t.size}`,fontfile:font,dpi:72,rgba:true}}).png().toBuffer({resolveWithObject:true});
    if(t.x+tile.info.width>spec.width-24 || t.y+tile.info.height>spec.height-12) throw new Error('Text exceeds safe edge: '+t.value);
    overlays.push({input:tile.data,left:t.x,top:t.y});
    boxes.push({text:t.value,x:t.x,y:t.y,width:tile.info.width,height:tile.info.height});
  }
  const background=Buffer.from(start+spec.shapes+'\n</svg>');
  const png=await sharp(background).composite(overlays).flatten({background:'#F6F6F2'}).png({compressionLevel:9,adaptiveFiltering:true}).toBuffer();
  fs.writeFileSync(destination,png,{flag:replaceOwn ? 'w' : 'wx'});
  return {path:path.relative(repo,destination).split(path.sep).join('/'),source:path.relative(repo,source).split(path.sep).join('/'),width:spec.width,height:spec.height,textBoxes:boxes};
}
(async()=>{
  const assets=[];
  for(const spec of [payee,photo]) assets.push(await build(spec));
  process.stdout.write(JSON.stringify({sharpVersions:sharp.versions,fontPath:path.relative(repo,font).split(path.sep).join('/'),assets},null,2)+'\n');
})().catch(error=>{console.error(error);process.exitCode=1});
