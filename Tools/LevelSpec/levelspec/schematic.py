"""Schematics of level specs: a plan of the level, every piece drawn where it is (see geometry), shaded by height, with
the movement checks (see checks) over it. As an HTML page (pan and zoom, layers to show and hide, each piece's settings
on hover, the checks listed) or a plain SVG.
"""

import html
import json
import math
import os

from .checks import check, load_envelope
from .geometry import layout, LAYERS

#: Layers drawn bottom to top.
ORDER = ["ground", "foliage", "cells", "solid", "deck", "stairs", "structure", "rails", "mechanics", "markings", "markers"]
SHADES = 9

PALETTE_LIGHT = """
  --paper: #F3F4F5; --sheet: #FBFBFC; --ink: #2D3138; --ink-soft: #5E6672; --rule: #CDD2D8; --grid: #E3E6EA;
  --accent: #D9702A; --cyan: #2E9FB0; --good: #3B8F5E; --warn: #C43C33; --tree: #7FA36A; --lawn: #DCE8D2; --hole: #FBFBFC;
  --h0: #E6E8EB; --h1: #D7DADE; --h2: #C5CAD0; --h3: #B1B7BF; --h4: #9CA3AD; --h5: #868E99; --h6: #707884; --h7: #5A626D; --h8: #454C56;
  --ground: #EEF0F2; --stairs: #F5E3D3; --cells: #E9EEF5;"""
PALETTE_DARK = """
  color-scheme: dark;
  --paper: #16191D; --sheet: #1C1F24; --ink: #D6D9DD; --ink-soft: #9AA2AD; --rule: #333941; --grid: #23272D;
  --accent: #EE8A3E; --cyan: #54C6D6; --good: #5DBA84; --warn: #E8645A; --tree: #6C9A58; --lawn: #22301F; --hole: #1C1F24;
  --h0: #262A31; --h1: #2F343C; --h2: #3A4049; --h3: #454C56; --h4: #515965; --h5: #5E6773; --h6: #6C7682; --h7: #7B8591; --h8: #8B96A2;
  --ground: #1E2227; --stairs: #3A2E25; --cells: #1F2630;"""

SVG_STYLE = """
.g-ground .poly { fill: var(--ground); stroke: var(--rule); stroke-width: 0.15; }
.poly { stroke: var(--ink); stroke-width: 0.12; stroke-linejoin: round; fill-rule: evenodd; }
.g-cells .poly { fill: var(--cells); stroke: var(--ink-soft); stroke-dasharray: 0.8 0.4; }
.g-stairs .poly { fill: var(--stairs); stroke: var(--accent); }
.g-foliage .poly { fill: var(--lawn); stroke: none; }
.g-mechanics .poly { fill: none; stroke: var(--cyan); stroke-width: 0.35; }
.g-structure .poly { fill: var(--h6); stroke: none; }
.line { fill: none; stroke-width: 0.25; stroke-linecap: round; stroke-linejoin: round; }
.g-rails .line { stroke: var(--ink-soft); stroke-width: 0.15; }
.g-markings .line { stroke: var(--accent); stroke-width: 0.2; }
.g-mechanics .line { stroke: var(--cyan); stroke-width: 0.5; }
.g-structure .line { stroke: var(--ink-soft); stroke-width: 0.3; stroke-dasharray: 0.6 0.4; }
.g-foliage .line { stroke: var(--tree); stroke-dasharray: 1 0.6; }
.g-solid .line, .g-cells .line { stroke: var(--ink); stroke-width: 0.3; }
.g-markers .line { stroke: var(--ink-soft); stroke-dasharray: 0.3 0.5; }
.point { fill: var(--ink); }
.g-foliage .point { fill: var(--tree); }
.g-markers .point { fill: var(--accent); }
.arrow { stroke: var(--accent); stroke-width: 0.3; fill: none; marker-end: url(#arrowhead); }
.g-mechanics .arrow { stroke: var(--cyan); }
.text { fill: var(--ink); font: 600 1.6px/1 "Barlow Condensed", "Arial Narrow", sans-serif; text-anchor: middle; dominant-baseline: middle; }
.g-markers .text { fill: var(--accent); font-size: 2.2px; }
.check { stroke-width: 0.45; stroke-linecap: round; fill: none; }
.check.info { stroke: var(--good); }
.check.warning { stroke: var(--warn); }
.check-label { font: 600 1.7px/1 "IBM Plex Mono", ui-monospace, monospace; text-anchor: middle; paint-order: stroke; stroke: var(--sheet); stroke-width: 0.5; }
.check-label.info { fill: var(--good); }
.check-label.warning { fill: var(--warn); }
"""


def _shade(y, low, high):
    if high - low < 1e-6:
        return 3
    return max(0, min(SHADES - 1, int(round((y - low) / (high - low) * (SHADES - 1)))))


def _pts(points):
    return " ".join(f"{x:.3f},{-z:.3f}" for x, z in points)


def _d(points, closed=True):
    return "M" + " L".join(f"{x:.3f},{-z:.3f}" for x, z in points) + (" Z" if closed else "")


def svg(spec, findings=None, envelope=None, standalone=False):
    """The plan as SVG markup: (svg text, the shapes drawn, the height range)."""
    shapes = layout(spec)
    if findings is None:
        findings = check(spec, envelope or load_envelope(), shapes)
    solid = [s.y1 for s in shapes if s.layer in ("solid", "deck", "stairs")]
    low, high = (min(solid), max(solid)) if solid else (0.0, 1.0)

    xs, zs = [], []
    for s in shapes:
        x0, z0, x1, z1 = s.bounds
        xs += [x0, x1]
        zs += [z0, z1]
    if not xs:
        xs, zs = [0, 10], [0, 10]
    pad = 6
    vx0, vx1, vz0, vz1 = min(xs) - pad, max(xs) + pad, min(zs) - pad, max(zs) + pad
    w, h = vx1 - vx0, vz1 - vz0

    groups = {layer: [] for layer in ORDER}
    for n, s in enumerate(sorted(enumerate(shapes), key=lambda t: (t[1].y1, t[0]))):
        k, s = s
        layer = s.layer if s.layer in groups else "markers"
        attrs = f'data-s="{k}"'
        if s.kind == "poly":
            fill = f' style="fill: var(--h{_shade(s.y1, low, high)})"' if layer in ("solid", "deck") else ""
            d = _d(s.points) + "".join(" " + _d(hole) for hole in s.holes)
            groups[layer].append(f'<path class="poly" d="{d}"{fill} {attrs}/>')
        elif s.kind == "line":
            groups[layer].append(f'<path class="line" d="{_d(s.points, s.closed)}" {attrs}/>')
        elif s.kind == "point":
            r = 1.8 if layer == "foliage" else 0.7
            x, z = s.points[0]
            groups[layer].append(f'<circle class="point" cx="{x:.3f}" cy="{-z:.3f}" r="{r}" {attrs}/>')
        elif s.kind == "text":
            x, z = s.points[0]
            groups[layer].append(f'<text class="text" x="{x:.3f}" y="{-z:.3f}" {attrs}>{html.escape(str(s.label or ""))}</text>')
        if s.arrow:
            (ax, az), (bx, bz) = s.arrow
            if math.hypot(bx - ax, bz - az) > 0.5:
                groups[layer].append(f'<path class="arrow" d="M{ax:.3f},{-az:.3f} L{bx:.3f},{-bz:.3f}" {attrs}/>')

    checks = []
    for i, f in enumerate(findings):
        if f.line:
            checks.append(f'<path class="check {f.severity}" d="{_d(f.line, False)}" data-f="{i}"/>')
        else:
            x, z = f.at
            checks.append(f'<circle class="check {f.severity}" cx="{x:.3f}" cy="{-z:.3f}" r="1.2" data-f="{i}"/>')
        if f.kind in ("gap", "climb"):
            x, z = f.at
            label = f.text.split(" ", 2)[0] + " m" + (" ↑" if f.kind == "climb" else "")
            checks.append(f'<text class="check-label {f.severity}" x="{x:.3f}" y="{-z - 1.4:.3f}" data-f="{i}">{label}</text>')

    grid = []
    step = 10 if max(w, h) < 400 else 20
    gx = math.floor(vx0 / step) * step
    while gx <= vx1:
        grid.append(f'<path d="M{gx},{-vz1} V{-vz0}" class="{"major" if gx % (step * 5) == 0 else ""}"/>')
        gx += step
    gz = math.floor(vz0 / step) * step
    while gz <= vz1:
        grid.append(f'<path d="M{vx0},{-gz} H{vx1}" class="{"major" if gz % (step * 5) == 0 else ""}"/>')
        gz += step

    style = SVG_STYLE
    if standalone:
        style = ":root, svg {" + PALETTE_LIGHT + "}\nsvg { background: var(--sheet); }\n.grid path { stroke: var(--grid); stroke-width: 0.1; }\n.grid .major { stroke: var(--rule); stroke-width: 0.2; }\n" + style
    size = f' width="{w * 4:.0f}" height="{h * 4:.0f}"' if standalone else ""
    label = html.escape(spec.get("name", "the level"))
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" id="plan" viewBox="{vx0:.2f} {-vz1:.2f} {w:.2f} {h:.2f}"{size} role="img" aria-label="Plan of {label}">',
           f"<style>{style}</style>",
           '<defs><marker id="arrowhead" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="5" markerHeight="5" orient="auto-start-reverse">'
           '<path d="M0,1 L9,5 L0,9 Z" fill="context-stroke"/></marker></defs>',
           '<g class="grid">' + "".join(grid) + "</g>"]
    for layer in ORDER:
        out.append(f'<g class="g-{layer}" data-layer="{layer}">' + "".join(groups[layer]) + "</g>")
    out.append('<g class="g-checks" data-layer="checks">' + "".join(checks) + "</g>")
    out.append("</svg>")
    return "\n".join(out), shapes, (low, high)


def html_page(spec, findings=None, envelope=None):
    """The schematic as an HTML page."""
    envelope = envelope or load_envelope()
    shapes = layout(spec)
    if findings is None:
        findings = check(spec, envelope, shapes)
    plan, shapes, (low, high) = svg(spec, findings, envelope)
    counts = {}
    for s in shapes:
        counts[s.layer] = counts.get(s.layer, 0) + 1
    pieces_total = sum(len(p.get("pieces", [])) for p in spec.get("parts", []))
    info = [{"label": s.label, "part": spec["parts"][s.part]["name"], "piece": s.piece, "y0": round(s.y0, 3), "y1": round(s.y1, 3)} for s in shapes]
    # A finding names pieces; the page highlights the shapes drawn for them.
    found = []
    for f in findings:
        d = f.as_dict()
        d["pieces"] = [k for k, s in enumerate(shapes) if s.index in f.pieces]
        found.append(d)
    layer_rows = "".join(
        f'<label class="layer"><input type="checkbox" data-toggle="{key}" checked> {html.escape(title)} <span class="n">{counts.get(key, 0)}</span></label>'
        for key, title in LAYERS.items() if counts.get(key))
    warn = sum(1 for f in findings if f.severity == "warning")
    check_rows = "".join(
        f'<li class="{f.severity}" data-f="{i}"><span class="kind">{html.escape(f.kind)}</span> {html.escape(f.text)}</li>' for i, f in enumerate(findings))
    shades = "".join(f'<span style="background: var(--h{i})"></span>' for i in range(SHADES))
    name = html.escape(spec.get("name", "Level"))
    return TEMPLATE.format(
        name=name, scene=html.escape(spec.get("scene", "")), parts=len(spec.get("parts", [])), pieces=pieces_total,
        low=f"{low:+g}", high=f"{high:+g}", shades=shades, layers=layer_rows, plan=plan, checks=check_rows,
        nchecks=len(findings), nwarn=warn, light=PALETTE_LIGHT, dark=PALETTE_DARK,
        info=json.dumps(info, ensure_ascii=False).replace("</", "<\\/"),
        found=json.dumps(found, ensure_ascii=False).replace("</", "<\\/"))


def write_schematic(spec, path, findings=None, envelope=None):
    """Writes a schematic: HTML, or SVG if path ends in .svg. Returns the findings drawn."""
    envelope = envelope or load_envelope()
    if findings is None:
        findings = check(spec, envelope)
    if path.lower().endswith(".svg"):
        text, _, _ = svg(spec, findings, envelope, standalone=True)
    else:
        text = html_page(spec, findings, envelope)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return findings


TEMPLATE = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{name} Schematic</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Barlow:wght@400;500;600&family=IBM+Plex+Mono:wght@400;500&display=swap">
<style>
:root {{{light}
  --display: "Barlow Condensed", "Arial Narrow", sans-serif; --body: "Barlow", "Segoe UI", Arial, sans-serif;
  --mono: "IBM Plex Mono", ui-monospace, Menlo, monospace;
}}
@media (prefers-color-scheme: dark) {{ :root:not([data-theme="light"]) {{{dark} }} }}
:root[data-theme="dark"] {{{dark} }}
* {{ box-sizing: border-box; }}
body {{ margin: 0; background: var(--paper); color: var(--ink); font: 15px/1.5 var(--body); }}
.app {{ display: grid; grid-template-columns: minmax(0, 1fr) 340px; height: 100vh; }}
.sheet {{ position: relative; background: var(--sheet); overflow: hidden; touch-action: none; cursor: grab; }}
.sheet.dragging {{ cursor: grabbing; }}
#plan {{ width: 100%; height: 100%; display: block; }}
.grid path {{ stroke: var(--grid); stroke-width: 0.1; vector-effect: non-scaling-stroke; }}
.grid .major {{ stroke: var(--rule); }}
.side {{ border-left: 1px solid var(--rule); padding: 20px 18px; overflow-y: auto; display: grid; gap: 20px; align-content: start; }}
.eyebrow {{ font: 600 12px/1 var(--display); letter-spacing: 0.16em; text-transform: uppercase; color: var(--accent); }}
h1 {{ font: 700 34px/1.05 var(--display); margin: 6px 0 4px; }}
h2 {{ font: 600 13px/1 var(--display); letter-spacing: 0.1em; text-transform: uppercase; color: var(--ink-soft); margin: 0 0 8px; }}
.meta {{ font: 12px/1.5 var(--mono); color: var(--ink-soft); word-break: break-all; }}
.stats {{ display: flex; gap: 16px; font: 600 22px/1 var(--display); }}
.stats small {{ display: block; font: 500 11px/1.4 var(--body); color: var(--ink-soft); text-transform: uppercase; letter-spacing: 0.06em; }}
.ramp {{ display: flex; height: 12px; border: 1px solid var(--rule); }} .ramp span {{ flex: 1; }}
.ramp-labels {{ display: flex; justify-content: space-between; font: 11px var(--mono); color: var(--ink-soft); margin-top: 3px; }}
.layer {{ display: flex; align-items: center; gap: 8px; font-size: 14px; padding: 2px 0; cursor: pointer; }}
.layer .n {{ margin-left: auto; font: 11px var(--mono); color: var(--ink-soft); }}
.checks {{ list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; font-size: 13px; }}
.checks li {{ padding: 5px 8px; border-left: 3px solid var(--good); background: var(--paper); cursor: default; }}
.checks li.warning {{ border-left-color: var(--warn); }}
.checks li.on {{ outline: 1px solid var(--accent); }}
.checks .kind {{ font: 600 10px/1 var(--mono); text-transform: uppercase; color: var(--ink-soft); margin-right: 4px; }}
.tools {{ position: absolute; left: 14px; top: 14px; display: flex; gap: 6px; }}
.tools button {{ font: 600 13px var(--display); letter-spacing: 0.06em; text-transform: uppercase; background: var(--sheet); color: var(--ink);
  border: 1px solid var(--rule); padding: 6px 10px; cursor: pointer; }}
.scale {{ position: absolute; left: 14px; bottom: 14px; font: 11px var(--mono); color: var(--ink-soft); }}
.tip {{ position: absolute; pointer-events: none; background: var(--paper); border: 1px solid var(--rule); padding: 8px 10px; font-size: 13px;
  max-width: 420px; box-shadow: 0 4px 14px rgba(0,0,0,.12); }}
.tip b {{ font: 600 16px/1.2 var(--display); display: block; }}
.tip code {{ font: 11px/1.45 var(--mono); color: var(--ink-soft); white-space: pre-wrap; word-break: break-word; display: block; margin-top: 4px; }}
[data-s].hot {{ stroke: var(--accent) !important; stroke-width: 0.6 !important; }}
.check.hot {{ stroke-width: 1.1; }}
@media (max-width: 820px) {{ .app {{ grid-template-columns: 1fr; grid-template-rows: 62vh auto; height: auto; }} .side {{ border-left: 0; border-top: 1px solid var(--rule); }} }}
</style>
</head>
<body>
<div class="app">
  <div class="sheet" id="sheet">
    {plan}
    <div class="tools"><button id="fit" type="button">Fit</button><button id="zin" type="button">+</button><button id="zout" type="button">−</button></div>
    <div class="scale" id="scale"></div>
    <div class="tip" id="tip" hidden></div>
  </div>
  <aside class="side">
    <div>
      <div class="eyebrow">Level schematic</div>
      <h1>{name}</h1>
      <div class="meta">{scene}</div>
    </div>
    <div class="stats"><div>{parts}<small>parts</small></div><div>{pieces}<small>pieces</small></div><div>{nchecks}<small>checks</small></div><div>{nwarn}<small>warnings</small></div></div>
    <div><h2>Height</h2><div class="ramp">{shades}</div><div class="ramp-labels"><span>{low} m</span><span>{high} m</span></div></div>
    <div><h2>Layers</h2>{layers}<label class="layer"><input type="checkbox" data-toggle="checks" checked> Movement checks <span class="n">{nchecks}</span></label></div>
    <div><h2>Movement checks</h2><ul class="checks" id="checks">{checks}</ul></div>
  </aside>
</div>
<script>
const INFO = {info};
const FOUND = {found};
const svg = document.getElementById("plan"), sheet = document.getElementById("sheet"), tip = document.getElementById("tip");
const home = svg.getAttribute("viewBox").split(" ").map(Number);
let view = home.slice();
function apply() {{ svg.setAttribute("viewBox", view.join(" ")); document.getElementById("scale").textContent = "View " + Math.round(view[2]) + " m across · +z up"; }}
function zoom(f, cx, cy) {{
  const r = svg.getBoundingClientRect();
  const px = cx === undefined ? 0.5 : (cx - r.left) / r.width, py = cy === undefined ? 0.5 : (cy - r.top) / r.height;
  const w = view[2] * f, h = view[3] * f;
  view = [view[0] + (view[2] - w) * px, view[1] + (view[3] - h) * py, w, h]; apply();
}}
sheet.addEventListener("wheel", e => {{ e.preventDefault(); zoom(Math.exp(e.deltaY * 0.0015), e.clientX, e.clientY); }}, {{ passive: false }});
let drag = null;
sheet.addEventListener("pointerdown", e => {{ if (e.target.closest(".tools")) return; drag = [e.clientX, e.clientY, view[0], view[1]]; sheet.classList.add("dragging"); sheet.setPointerCapture(e.pointerId); }});
sheet.addEventListener("pointermove", e => {{
  if (drag) {{ const r = svg.getBoundingClientRect(), s = Math.max(view[2] / r.width, view[3] / r.height);
    view[0] = drag[2] - (e.clientX - drag[0]) * s; view[1] = drag[3] - (e.clientY - drag[1]) * s; apply(); return; }}
  const t = e.target.closest("[data-s], [data-f]");
  if (!t) {{ tip.hidden = true; return; }}
  let body;
  if (t.dataset.s !== undefined) {{
    const s = INFO[+t.dataset.s], p = Object.assign({{}}, s.piece);
    body = `<b>${{esc(s.label || p.piece)}}</b>${{esc(p.piece)}} in ${{esc(s.part)}}, ${{s.y0}} to ${{s.y1}} m<code>${{esc(JSON.stringify(p))}}</code>`;
  }} else {{ const f = FOUND[+t.dataset.f]; body = `<b>${{esc(f.kind)}}</b>${{esc(f.text)}}`; }}
  tip.innerHTML = body; tip.hidden = false;
  const r = sheet.getBoundingClientRect();
  tip.style.left = Math.min(e.clientX - r.left + 14, r.width - 430) + "px"; tip.style.top = (e.clientY - r.top + 14) + "px";
}});
sheet.addEventListener("pointerup", () => {{ drag = null; sheet.classList.remove("dragging"); }});
sheet.addEventListener("pointerleave", () => {{ tip.hidden = true; }});
document.getElementById("fit").onclick = () => {{ view = home.slice(); apply(); }};
document.getElementById("zin").onclick = () => zoom(0.7);
document.getElementById("zout").onclick = () => zoom(1 / 0.7);
document.querySelectorAll("[data-toggle]").forEach(box => box.addEventListener("change", () => {{
  svg.querySelector(`[data-layer="${{box.dataset.toggle}}"]`).style.display = box.checked ? "" : "none"; }}));
document.querySelectorAll("#checks li").forEach(li => {{
  const marks = () => svg.querySelectorAll(`[data-f="${{li.dataset.f}}"]`);
  li.addEventListener("mouseenter", () => {{ li.classList.add("on"); marks().forEach(m => m.classList.add("hot")); FOUND[+li.dataset.f].pieces.forEach(i => svg.querySelectorAll(`[data-s="${{i}}"]`).forEach(m => m.classList.add("hot"))); }});
  li.addEventListener("mouseleave", () => {{ li.classList.remove("on"); svg.querySelectorAll(".hot").forEach(m => m.classList.remove("hot")); }});
  li.addEventListener("click", () => {{ const [x, z] = FOUND[+li.dataset.f].at; const w = Math.min(view[2], 80), h = w * view[3] / view[2]; view = [x - w / 2, -z - h / 2, w, h]; apply(); }});
}});
function esc(s) {{ return String(s).replace(/[&<>"]/g, c => ({{"&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;"}})[c]); }}
apply();
</script>
</body>
</html>
"""
