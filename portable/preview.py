"""Visual smoke test using the actual Tk canvas and fixture data only."""
from datetime import datetime, timedelta
import json
from pathlib import Path
import sys
from .core import UTC, Snapshot, Window, parse_codex
from .app import demo_snapshots


def run_smoke(app, output):
    from PIL import ImageGrab
    output.mkdir(parents=True, exist_ok=True)
    app.root.geometry("+80+80")
    app.root.attributes("-topmost", True)
    cases=[]
    for skin in ("midnight", "paper", "terminal"):
        for mode in ("compact", "edge", "ball"):
            cases.append((skin,mode,"weekly-only","en"))
    for state in ("both","exhausted","unknown","unsupported","loading","stale","error","cached-error"):
        cases.append(("midnight","edge",state,"en"))
        cases.append(("midnight","ball",state,"en"))
    cases.extend([("paper","edge","weekly-only","zh-CN"),("paper","ball","weekly-only","zh-CN")])
    for skin in ("midnight","paper","terminal"):
        for percent in (0,1,25,50,75,99,100):cases.append((skin,"ball","area-"+str(percent),"en"))
    cases.extend([("midnight","ball","claude-error","en"),("paper","ball","large","en"),("paper","ball","hover-legend","en")])
    results=[]
    def step(index):
        if index >= len(cases):
            (output/"portable-smoke.json").write_text(json.dumps({"platform":"Windows", "cases":results},indent=2)+"\n",encoding="utf-8")
            app.close()
            return
        skin,mode,state,lang=cases[index]
        app.settings.update(skin=skin,language=lang,ballService="codex",ballWindow="auto")
        app.settings["ballSize"]=96 if state=="large" else 64
        app.hide_legend()
        app.mode=mode
        app.snapshots=demo_snapshots()
        app.busy=False
        codex=app.snapshots[0]
        if state.startswith("area-"):
            codex.windows[0].remaining=float(state.split("-")[1])
        elif state=="claude-error":
            app.snapshots[1].status="fetch_error"
        elif state=="both":
            app.snapshots[0]=parse_codex({"primary_window":{"used_percent":12,"limit_window_seconds":18000},"secondary_window":{"used_percent":32,"limit_window_seconds":604800}})
        elif state=="exhausted":
            codex.windows[0].remaining=0
        elif state in ("unknown","loading","error","unsupported"):
            codex.windows=[Window("unknown","?")]
            if state=="unsupported":
                codex.windows=[Window("weekly","7d","Unsupported")]
            codex.status={"loading":"starting","error":"fetch_error"}.get(state,"no_data")
        elif state=="stale":
            codex.observed_at=datetime.now(UTC)-timedelta(minutes=30)
        elif state=="cached-error":
            codex.status="fetch_error"
        app.draw()
        app.root.update_idletasks()
        app.root.geometry(f"{app.canvas.cget('width')}x{app.canvas.cget('height')}+80+80")
        app.root.update_idletasks()
        def capture():
            app.root.lift()
            app.root.update_idletasks()
            canvas=app.canvas
            box=(canvas.winfo_rootx(),canvas.winfo_rooty(),canvas.winfo_rootx()+canvas.winfo_width(),canvas.winfo_rooty()+canvas.winfo_height())
            name=f"portable-{skin}-{mode}-{state}-{lang}.png"
            if sys.platform != "win32":
                raise RuntimeError("This smoke capture has only been verified on Windows")
            # PrintWindow targets the canvas HWND directly; never capture neighboring desktop content.
            app.root.update()
            ImageGrab.grab(window=canvas.winfo_id()).save(output/name)
            if state=="hover-legend":
                app.show_legend();app.root.update();ImageGrab.grab(window=app.legend_window.winfo_id()).save(output/"portable-hover-legend.png")
            results.append({"skin":skin,"mode":mode,"state":state,"language":lang,"image":name,"canvasSize":[canvas.winfo_width(),canvas.winfo_height()]})
            app.root.after(60,lambda:step(index+1))
        app.root.after(150,capture)
    step(0)
