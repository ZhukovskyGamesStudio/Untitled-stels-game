"""
Sound board: every sound of the game on one page, to listen to and judge without running the game.

    python Tools/Audio/sound_board.py            # writes Logs/sounds/index.html and opens it in the browser
    python Tools/Audio/sound_board.py --no-open

The page plays the project's own files (run it again after changing sounds: the list and the waveforms are read
then). Each sound: play, loop, waveform (click to seek), length and loudness, the SoundBank field it fills, the
scripts that play it. Mark it Keep / Improve / Replace with a note; "Copy list" copies the marked ones as text.
Marks are kept in the browser. Standard library and numpy only; nothing is downloaded or sent anywhere.
"""
import glob
import json
import math
import os
import re
import sys
import wave
import webbrowser

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO = os.path.join(ROOT, "Assets", "_Game", "Art", "Audio")
SCRIPTS = os.path.join(ROOT, "Assets", "_Game", "Scripts")
BANK_ASSET = os.path.join(ROOT, "Assets", "_Game", "Data", "SoundBank.asset")
BANK_CODE = os.path.join(SCRIPTS, "Audio", "SoundBank.cs")
OUT = os.path.join(ROOT, "Logs", "sounds")
BARS = 160  # columns of a waveform


def read(path):
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def db(x):
    return round(20 * math.log10(max(float(x), 1e-5)), 1)


def analyse(path):
    """Length, peak and loudness (RMS of the loudest half second), clipping, the waveform's columns (0..1)."""
    with wave.open(path) as w:
        rate, channels, width = w.getframerate(), w.getnchannels(), w.getsampwidth()
        raw = w.readframes(w.getnframes())
    if width != 2:
        return None
    x = np.frombuffer(raw, dtype="<i2").astype(np.float64).reshape(-1, channels).mean(axis=1) / 32768.0
    if len(x) == 0:
        return None
    win = max(1, min(len(x), rate // 2))
    total = np.concatenate([[0.0], np.cumsum(x ** 2)])
    power = (total[win:] - total[:-win]) / win
    edges = np.linspace(0, len(x), BARS + 1).astype(int)
    bars = [round(float(np.max(np.abs(x[a:max(b, a + 1)]))), 3) if a < len(x) else 0 for a, b in zip(edges, edges[1:])]
    return {
        "length": round(len(x) / rate, 2), "peak": db(np.max(np.abs(x))), "rms": db(math.sqrt(float(np.max(power)))),
        # (a constant signal is the voice chat's carrier, not a clipped sound)
        "clipped": int(np.sum(np.abs(x) >= 0.999)) if np.ptp(x) > 0.01 else 0, "bars": bars,
    }


def bank_fields():
    """GUID of a clip -> the SoundBank fields it fills; and the fields that are empty."""
    by_guid, empty, field = {}, [], None
    for line in read(BANK_ASSET).splitlines():
        m = re.match(r"  (\w+):(.*)$", line)
        if m:
            field = m.group(1)
            if not field.startswith("m_") and "{fileID: 0}" in m.group(2):
                empty.append(field)
        g = re.search(r"guid: ([0-9a-f]{32})", line)
        if g and field and not field.startswith("m_"):
            by_guid.setdefault(g.group(1), []).append(field)
    return by_guid, empty


def tooltips():
    """Field -> its [Tooltip], from SoundBank.cs (a tooltip covers every field of its declaration)."""
    tips, tip = {}, None
    for line in read(BANK_CODE).splitlines():
        m = re.search(r'\[Tooltip\("(.*)"\)\]', line)
        if m:
            tip = m.group(1)
        m = re.search(r"public AudioClip(?:\[\])? ([^;=]+)", line)
        if m:
            for name in m.group(1).split(","):
                if tip:
                    tips[name.strip()] = tip
            tip = None
    return tips


def code_uses(fields):
    """Field -> the scripts that name it (`bank.Field`, `Bank?.Field`, `b => b.Field`)."""
    uses = {f: [] for f in fields}
    for path in sorted(glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True)):
        if os.path.abspath(path) == os.path.abspath(BANK_CODE):
            continue
        text = read(path)
        if "Bank" not in text and "bank" not in text:
            continue
        for f in set(re.findall(r"(?:[Bb]ank\??|\bb)\.([A-Z]\w*)", text)):
            if f in uses:
                uses[f].append(os.path.basename(path))
    # fields the bank itself hands out (Steps(surface), Of(sound)): "=> Field,"
    for f in set(re.findall(r"=>\s*([A-Z]\w*),", read(BANK_CODE))):
        if f in uses:
            uses[f].append("SoundBank.cs (chosen there)")
    return uses


def asset_uses(guids):
    """GUID -> prefabs, scenes and data assets that point at the clip directly (past the SoundBank)."""
    uses = {g: [] for g in guids}
    game = os.path.join(ROOT, "Assets", "_Game")
    for pattern in ("Prefabs/**/*.prefab", "Scenes/*.unity", "Data/*.asset", "Data/Config/*.asset"):  # (not the generated meshes under Art)
        for path in glob.glob(os.path.join(game, *pattern.split("/")), recursive=True):
            if os.path.abspath(path) == os.path.abspath(BANK_ASSET) or os.path.getsize(path) > 40_000_000:
                continue
            with open(path, "rb") as f:
                data = f.read()
            if b"8300000" not in data:  # (an AudioClip reference's file id)
                continue
            text = data.decode("utf-8", "replace")
            for g in set(re.findall(r"fileID: 8300000, guid: ([0-9a-f]{32})", text)):
                if g in uses:
                    uses[g].append(os.path.basename(path))
    return uses


def collect():
    by_guid, empty = bank_fields()
    tips = tooltips()
    fields = sorted({f for fs in by_guid.values() for f in fs})
    scripts = code_uses(fields)
    credits = read(os.path.join(AUDIO, "CREDITS.md"))
    files = sorted(glob.glob(os.path.join(AUDIO, "**", "*.wav"), recursive=True))
    guid_of = {}
    for p in files:
        m = re.search(r"guid: ([0-9a-f]{32})", read(p + ".meta")) if os.path.exists(p + ".meta") else None
        guid_of[p] = m.group(1) if m else ""
    direct = asset_uses([g for g in guid_of.values() if g])
    rows = []
    for p in files:
        rel = os.path.relpath(p, AUDIO).replace("\\", "/")
        info = analyse(p)
        if info is None:
            print("skipped (not 16-bit, or empty):", rel)
            continue
        group, _, name = rel.rpartition("/")
        fs = by_guid.get(guid_of[p], [])
        info.update({
            "id": rel[:-4], "group": group or "(root)", "name": name[:-4],
            "src": os.path.relpath(p, OUT).replace("\\", "/"), "stamp": int(os.path.getmtime(p)),
            "fields": fs, "tip": next((tips[f] for f in fs if f in tips), ""),
            "scripts": sorted({s for f in fs for s in scripts.get(f, [])}), "assets": sorted(set(direct.get(guid_of[p], []))),
            "recorded": ("| %s " % rel) in credits or rel[:-4] in re.split(r"[|,]\s*|\s+\|", credits),
            "voice": group == "Voices" or name.startswith("voice_"),
        })
        rows.append(info)
    return rows, empty


PAGE = r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Sound board</title>
<style>
:root{--bg:#f4f4f1;--card:#fff;--ink:#1d1f1e;--dim:#676c69;--line:#dcdcd6;--accent:#0f6b5c;--keep:#2f7d3b;--improve:#a86a00;--replace:#b3261e;--wave:#b9c4bf}
@media (prefers-color-scheme:dark){:root{--bg:#151716;--card:#1e2120;--ink:#e8eae8;--dim:#9aa19d;--line:#313634;--accent:#5cc7b2;--keep:#6cc47a;--improve:#e0a83e;--replace:#f07a70;--wave:#4a5450}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.4 system-ui,Segoe UI,sans-serif}
header{position:sticky;top:0;z-index:2;background:var(--bg);border-bottom:1px solid var(--line);padding:12px 16px}
header .in,main{max-width:1180px;margin:0 auto}
.bar{display:flex;flex-wrap:wrap;gap:8px 12px;align-items:center}
h1{font-size:18px;margin:0 8px 0 0}
input,select,button,textarea{font:inherit;color:inherit;background:var(--card);border:1px solid var(--line);border-radius:6px;padding:5px 10px}
button{cursor:pointer}button:hover{border-color:var(--accent)}
:focus-visible{outline:2px solid var(--accent);outline-offset:1px}
#q{flex:1 1 200px;min-width:140px}
.hint,.count{color:var(--dim);font-size:13px}
main{padding:8px 16px 80px}
h2{font-size:15px;margin:26px 0 8px;display:flex;gap:12px;align-items:center}
h2 span{color:var(--dim);font-weight:400}
.row{display:grid;grid-template-columns:44px minmax(150px,1.1fr) 170px minmax(150px,1.3fr) auto minmax(120px,1fr);gap:12px;align-items:center;background:var(--card);border:1px solid var(--line);border-left:4px solid var(--line);border-radius:6px;padding:7px 10px;margin-bottom:6px}
.row.on{border-color:var(--accent);border-left-color:var(--accent)}
.row[data-v=keep]{border-left-color:var(--keep)}.row[data-v=improve]{border-left-color:var(--improve)}.row[data-v=replace]{border-left-color:var(--replace)}
.play{width:40px;height:40px;border-radius:50%;padding:0;font-size:15px}
.name{font-weight:600;overflow-wrap:anywhere}
.sub{color:var(--dim);font-size:12.5px;overflow-wrap:anywhere}
canvas{width:170px;height:40px;display:block;cursor:pointer}
.tags{display:flex;flex-wrap:wrap;gap:4px;margin-top:3px}
.tag{font-size:11.5px;border:1px solid var(--line);border-radius:4px;padding:0 5px;color:var(--dim)}
.tag.warn{color:var(--improve);border-color:currentColor}
.verdict{display:flex;gap:4px}
.verdict button{padding:4px 8px;font-size:13px}
.verdict [aria-pressed=true]{color:#fff;border-color:transparent}
.verdict [data-v=keep][aria-pressed=true]{background:#2f7d3b}.verdict [data-v=improve][aria-pressed=true]{background:#a86a00}.verdict [data-v=replace][aria-pressed=true]{background:#b3261e}
.note{width:100%}
textarea#out{width:100%;height:180px;margin-top:8px;display:none}
label{display:inline-flex;gap:6px;align-items:center;font-size:13.5px}
@media (max-width:980px){.row{grid-template-columns:44px 1fr 170px}.row .use,.row .verdict,.row .note{grid-column:2/-1}}
@media (max-width:520px){.row{grid-template-columns:44px 1fr}.row canvas{grid-column:2}.row .use,.row .verdict,.row .note{grid-column:2}}
</style></head><body>
<header><div class="in">
<div class="bar"><h1>Sound board</h1>
<input id="q" type="search" placeholder="Search name, use or script" aria-label="Search">
<select id="show" aria-label="Show"><option value="">All sounds</option><option value="none">Not marked</option><option value="keep">Keep</option><option value="improve">Improve</option><option value="replace">Replace</option><option value="unused">Not used in the game</option></select>
<label>Volume <input id="vol" type="range" min="0" max="1" step="0.01" value="0.7"></label>
<label><input id="loop" type="checkbox"> Loop</label>
<button id="stop">Stop</button><button id="copy">Copy list</button><span class="count" id="count" role="status"></span></div>
<div class="hint">Keys: ↑ ↓ choose and play · Space play/stop · 1 keep · 2 improve · 3 replace · 0 clear. Volumes are the files' own; the game mixes them on top.</div>
<textarea id="out" aria-label="Marked sounds"></textarea></div></header>
<main id="list"></main>
<script>
const DATA=/*DATA*/, EMPTY=/*EMPTY*/, KEY="usg-sound-board";
let marks={}; try{marks=JSON.parse(localStorage.getItem(KEY))||{}}catch(e){}
const save=()=>{try{localStorage.setItem(KEY,JSON.stringify(marks))}catch(e){}};
const $=id=>document.getElementById(id), el=(t,c,x)=>{const e=document.createElement(t);if(c)e.className=c;if(x!=null)e.textContent=x;return e};
const audio=new Audio(); let cur=null, rows={}, queue=[];
const css=n=>getComputedStyle(document.documentElement).getPropertyValue(n).trim();
function draw(s){const c=rows[s.id]&&rows[s.id].canvas;if(!c)return;const r=window.devicePixelRatio||1;c.width=170*r;c.height=40*r;const g=c.getContext("2d");g.scale(r,r);
 const at=cur===s&&audio.duration?audio.currentTime/audio.duration:0,w=170/s.bars.length,top=Math.max(...s.bars,0.001);
 s.bars.forEach((b,i)=>{const h=Math.max(1,b/top*38);g.fillStyle=i/s.bars.length<at?css("--accent"):css("--wave");g.fillRect(i*w,20-h/2,Math.max(.6,w-.3),h)})}
function ui(){for(const s of DATA){const r=rows[s.id];if(!r)continue;const on=cur===s&&!audio.paused;r.row.classList.toggle("on",cur===s);r.play.textContent=on?"■":"▶";r.play.setAttribute("aria-label",(on?"Stop ":"Play ")+s.name)}}
function play(s,from){const old=cur;cur=s;if(old!==s||!audio.src)audio.src=s.src+"?v="+s.stamp;audio.loop=$("loop").checked;audio.volume=+$("vol").value;
 audio.play().then(()=>{if(from!=null&&audio.duration)audio.currentTime=from*audio.duration}).catch(e=>{if(e.name!=="AbortError")$("count").textContent="Cannot play "+s.id+": "+e.message});
 if(old&&old!==s)draw(old);ui()}
function stop(){queue=[];audio.pause();audio.currentTime=0;if(cur)draw(cur);ui()}
audio.onended=()=>{if(cur)draw(cur);const n=queue.shift();n?play(n):ui()};audio.onpause=ui;audio.onplay=ui;
(function tick(){if(cur&&!audio.paused)draw(cur);requestAnimationFrame(tick)})();
function mark(s,v){const m=marks[s.id]||(marks[s.id]={});m.v=m.v===v||!v?"":v;if(!m.v&&!m.note)delete marks[s.id];save();paint(s);count()}
function paint(s){const r=rows[s.id];if(!r)return;const v=(marks[s.id]||{}).v||"";r.row.dataset.v=v;r.btns.forEach(b=>b.setAttribute("aria-pressed",b.dataset.v===v))}
function count(){const n={keep:0,improve:0,replace:0};DATA.forEach(s=>{const v=(marks[s.id]||{}).v;if(v)n[v]++});$("count").textContent=DATA.length+" sounds · "+n.keep+" keep · "+n.improve+" improve · "+n.replace+" replace"}
const unused=s=>!s.scripts.length&&!s.assets.length;
function visible(){const q=$("q").value.toLowerCase(),f=$("show").value;return DATA.filter(s=>{const v=(marks[s.id]||{}).v||"";
 if(f==="unused"?!unused(s):f==="none"?v:f&&v!==f)return false;
 return !q||[s.id,s.tip,s.fields.join(" "),s.scripts.join(" "),s.assets.join(" "),(marks[s.id]||{}).note||""].join(" ").toLowerCase().includes(q)})}
let shown=[];
function render(){const list=$("list");list.textContent="";rows={};shown=visible();const groups=[...new Set(shown.map(s=>s.group))];
 if(!shown.length)list.append(el("p","sub","No sounds match. Clear the search or choose All sounds."));
 for(const g of groups){const in_=shown.filter(s=>s.group===g),h=el("h2",null,g);h.append(el("span",null,in_.length+""));const all=el("button",null,"Play all");all.onclick=()=>{$("loop").checked=false;queue=in_.slice(1);play(in_[0])};h.append(all);list.append(h);
 for(const s of in_){const row=el("div","row"),p=el("button","play","▶"),c=el("canvas"),a=el("div"),u=el("div","use"),vd=el("div","verdict"),note=el("input","note");
  p.onclick=()=>{if(cur===s&&!audio.paused)stop();else{queue=[];play(s)}};
  a.append(el("div","name",s.name),el("div","sub",s.length.toFixed(2)+" s · loudness "+s.rms+" dB · peak "+s.peak+" dB"));
  const t=el("div","tags");t.append(el("span","tag",s.recorded?"recorded (CC0)":s.voice?"voice":"synthesized"));
  if(s.clipped>3)t.append(el("span","tag warn","clips"));if(s.rms<-40)t.append(el("span","tag warn","very quiet"));if(unused(s))t.append(el("span","tag warn","not used"));a.append(t);
  c.title="Click to play from here";c.onclick=e=>{const b=c.getBoundingClientRect();queue=[];play(s,(e.clientX-b.left)/b.width)};
  if(s.tip)u.append(el("div","sub",s.tip));
  if(s.fields.length)u.append(el("div","sub","SoundBank."+s.fields.join(", ")));
  u.append(el("div","sub",[...s.scripts,...s.assets].slice(0,6).join(", ")||"nothing plays it"));
  const btns=["keep","improve","replace"].map(v=>{const b=el("button",null,v[0].toUpperCase()+v.slice(1));b.dataset.v=v;b.onclick=()=>mark(s,v);vd.append(b);return b});
  note.placeholder="Note";note.setAttribute("aria-label","Note for "+s.name);note.value=(marks[s.id]||{}).note||"";
  note.oninput=()=>{const m=marks[s.id]||(marks[s.id]={});m.note=note.value;if(!m.v&&!m.note)delete marks[s.id];save()};
  row.append(p,a,c,u,vd,note);list.append(row);rows[s.id]={row,play:p,canvas:c,btns};paint(s);draw(s)}}
 ui();count()}
function text(){const o=[];for(const v of["replace","improve","keep",""]){const l=DATA.filter(s=>marks[s.id]&&(marks[s.id].v||"")===v);if(!l.length)continue;o.push((v?v[0].toUpperCase()+v.slice(1):"Notes only")+":");l.forEach(s=>o.push("- "+s.id+(marks[s.id].note?": "+marks[s.id].note:"")));o.push("")}return o.join("\n").trim()}
$("copy").onclick=async()=>{const t=text(),o=$("out");if(!t){$("count").textContent="Nothing marked yet.";return}o.value=t;try{await navigator.clipboard.writeText(t);$("count").textContent="Copied. Paste it into the chat."}catch(e){$("count").textContent="Select and copy the text below."}o.style.display="block";o.select()};
$("stop").onclick=stop;$("vol").oninput=()=>audio.volume=+$("vol").value;$("loop").onchange=()=>audio.loop=$("loop").checked;
$("q").oninput=render;$("show").onchange=render;
document.onkeydown=e=>{if(/^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName)&&!(e.target.type==="range"||e.target.type==="checkbox"))return;if(e.ctrlKey||e.metaKey||e.altKey)return;
 if(e.key==="ArrowDown"||e.key==="ArrowUp"){if(e.target.type==="range"||!shown.length)return;e.preventDefault();const i=shown.indexOf(cur),n=shown[Math.max(0,Math.min(shown.length-1,i<0?0:i+(e.key==="ArrowDown"?1:-1)))];queue=[];play(n);rows[n.id].row.scrollIntoView({block:"nearest"})}
 else if(e.key===" "&&e.target.tagName!=="BUTTON"&&e.target.type!=="checkbox"&&cur){e.preventDefault();audio.paused?play(cur):stop()}
 else if(cur&&"0123".includes(e.key)&&e.key.length===1)mark(cur,["","keep","improve","replace"][+e.key])};
matchMedia("(prefers-color-scheme:dark)").addEventListener("change",()=>shown.forEach(draw));
if(EMPTY.length)document.querySelector(".hint").append(" Empty SoundBank fields: "+EMPTY.join(", ")+".");
render();
</script></body></html>
"""


def main():
    rows, empty = collect()
    os.makedirs(OUT, exist_ok=True)
    data = json.dumps(rows, ensure_ascii=False).replace("</", "<\\/")
    page = PAGE.replace("/*DATA*/", data).replace("/*EMPTY*/", json.dumps(empty))
    path = os.path.join(OUT, "index.html")
    with open(path, "w", encoding="utf-8") as f:
        f.write(page)
    unused = [r["id"] for r in rows if not r["scripts"] and not r["assets"]]
    print(f"{len(rows)} sounds -> {path}")
    print("clipping:", ", ".join(r["id"] for r in rows if r["clipped"] > 3) or "none")
    print("not used by any script, prefab or scene:", ", ".join(unused) or "none")
    if "--no-open" not in sys.argv:
        webbrowser.open("file:///" + path.replace("\\", "/"))


if __name__ == "__main__":
    main()
