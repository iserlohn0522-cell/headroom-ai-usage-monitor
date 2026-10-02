"""Headroom desktop widget for Windows, macOS and Linux (Python 3.10+ / Tk 8.6+)."""
from __future__ import annotations
import argparse
from concurrent.futures import ThreadPoolExecutor
from dataclasses import replace
from datetime import datetime, timedelta
import json
from pathlib import Path
import queue
import re
import sys
import tkinter as tk
from tkinter import filedialog, font as tkfont, messagebox
from .core import (UTC, Snapshot, Window, map_view, select, migrate_settings, settings_path,
                   read_json, read_manifest, parse_snapshot, parse_codex, parse_claude, timestamp, sphere_window, liquid_segment)

SKINS = {
    "midnight": dict(bg="#13192a", surface="#222b42", text="#edf3ff", muted="#a2b2ca", border="#415272", accent="#67bbff", track="#2b3950", radius=18, font="Segoe UI"),
    "paper": dict(bg="#f8f5ed", surface="#ebe5d7", text="#273230", muted="#59655e", border="#aab3a1", accent="#227156", track="#d9dece", radius=24, font="Segoe UI"),
    "terminal": dict(bg="#091612", surface="#11271d", text="#a6f8b5", muted="#77ac88", border="#3f764e", accent="#7eef9b", track="#1c3e28", radius=2, font="Consolas"),
}
ZH_STATES = {"Fresh":"剩余额度", "Exhausted":"已用尽", "Unsupported":"不提供", "Loading":"更新中", "Unknown":"无数据", "Stale":"已过期", "Error":"读取失败"}
EN_STATES = {"Fresh":"Remaining", "Exhausted":"Exhausted", "Unsupported":"Not offered", "Loading":"Loading", "Unknown":"No data", "Stale":"Stale", "Error":"Read error"}


def demo_snapshots():
    now = datetime.now(UTC)
    weekly = parse_codex({"plan_type":"pro", "primary_window":{"used_percent":32,"limit_window_seconds":604800,"reset_at":int((now+timedelta(days=4)).timestamp())}, "secondary_window":None}, now)
    claude = parse_claude({"five_hour":{"utilization":18,"resets_at":(now+timedelta(hours=3)).isoformat()}, "seven_day":{"utilization":41,"resets_at":(now+timedelta(days=4)).isoformat()}}, now)
    return [weekly, claude]


class HeadroomApp:
    def __init__(self, root, args):
        self.root, self.args = root, args
        self.path = Path(args.settings) if args.settings else settings_path()
        try:
            raw = read_json(self.path) if self.path.exists() else {}
        except (OSError, ValueError):
            raw = {}
        self.settings = migrate_settings(raw)
        self.manifest = args.providers or self.settings.get("providerManifest")
        self.sources = {}
        self.fixture = Path(args.fixture) if args.fixture else None
        self.snapshots = []
        self.demo = not (self.manifest or self.fixture or args.codex_cli)
        self.mode = self.settings.get("widgetMode", "compact")
        if self.mode not in ("compact", "edge", "ball"):
            self.mode = "compact"
        self.busy, self.closed = False, False
        self.jobs = ThreadPoolExecutor(max_workers=1, thread_name_prefix="headroom-provider")
        self.results = queue.Queue()
        self.last_fetch = None
        self.page = 0
        self.drag = None
        self.error_banner = None
        self.legend_window = None
        self.legend_job = None
        self.root.title("Headroom")
        self.root.resizable(False, False)
        self.root.attributes("-topmost", bool(self.settings.get("alwaysOnTop", True)))
        self.canvas = tk.Canvas(root, highlightthickness=0, takefocus=True)
        self.canvas.pack(fill="both", expand=True)
        self.canvas.bind("<ButtonPress-1>", self.start_drag)
        self.canvas.bind("<B1-Motion>", self.move_drag)
        self.canvas.bind("<ButtonRelease-1>", self.release_drag)
        self.canvas.bind("<Double-Button-1>", lambda event: self.set_mode("edge" if self.mode == "ball" else "ball"))
        self.canvas.bind("<Button-3>", self.popup)
        self.canvas.bind("<Button-2>", self.popup)
        self.canvas.bind("<Enter>", self.schedule_legend)
        self.canvas.bind("<Leave>", lambda event:self.hide_legend())
        self.root.bind("<F5>", lambda event: self.refresh())
        self.root.bind("<Escape>", lambda event: self.set_mode("compact"))
        self.root.bind("<space>", lambda event: self.set_mode("edge" if self.mode == "ball" else "ball"))
        self.root.protocol("WM_DELETE_WINDOW", self.close)
        self.refresh()
        self.tick()

    def t(self, zh, en):
        return en if self.settings["language"] == "en" else zh

    def save(self):
        if self.args.smoke_test:
            return
        try:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            # Keep a recoverable previous preference file. No credentials are stored.
            if self.path.exists():
                backup = self.path.with_name(self.path.stem + "-previous.json")
                backup.write_bytes(self.path.read_bytes())
            self.path.write_text(json.dumps(self.settings, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        except OSError:
            self.error_banner = self.t("设置保存失败", "Could not save settings")

    def set_pref(self, key, value):
        self.settings[key] = value
        if key == "alwaysOnTop":
            self.root.attributes("-topmost", bool(value))
        self.save()
        self.draw()

    def set_mode(self, value):
        self.hide_legend()
        self.mode = value
        self.settings["widgetMode"] = value
        self.save()
        self.draw()

    def choose_manifest(self):
        path = filedialog.askopenfilename(parent=self.root, title=self.t("选择提供方清单", "Choose provider manifest"), filetypes=[("JSON", "*.json")])
        if not path:
            return
        try:
            read_manifest(path)
        except (OSError, ValueError):
            messagebox.showerror("Headroom", self.t("无效的本地提供方清单", "Invalid local provider manifest"), parent=self.root)
            return
        self.manifest = path
        self.settings["providerManifest"] = path
        self.demo = False
        self.save()
        self.refresh()

    def popup(self, event):
        menu = tk.Menu(self.root, tearoff=False)
        skins = tk.Menu(menu, tearoff=False)
        for key, label in (("midnight", self.t("午夜 · 柔和", "Midnight · soft")), ("paper", self.t("纸张 · 明亮", "Paper · light")), ("terminal", self.t("终端 · 分段", "Terminal · segmented"))):
            skins.add_command(label=("✓ " if self.settings["skin"] == key else "")+label, command=lambda k=key: self.set_pref("skin",k))
        menu.add_cascade(label=self.t("皮肤", "Skin"), menu=skins)
        modes = tk.Menu(menu, tearoff=False)
        for mode, label in (("compact",self.t("紧凑面板","Compact")),("edge",self.t("详细面板","Detailed")),("ball",self.t("悬浮球","Ball"))):
            modes.add_command(label=label,command=lambda m=mode:self.set_mode(m))
        menu.add_cascade(label=self.t("显示模式", "Display"),menu=modes)
        if len(self.views())>6:
            menu.add_command(label=self.t("下一页额度","Next allowance page"),command=self.next_page)
        menu.add_command(label="简体中文", command=lambda:self.set_pref("language","zh-CN"))
        menu.add_command(label="English", command=lambda:self.set_pref("language","en"))
        menu.add_command(label=self.t("切换置顶","Toggle always on top"),command=lambda:self.set_pref("alwaysOnTop",not self.settings.get("alwaysOnTop",True)))
        menu.add_command(label=self.t("打开提供方清单…","Open provider manifest…"),command=self.choose_manifest)
        menu.add_command(label=self.t("刷新 · F5","Refresh · F5"),command=self.refresh)
        menu.add_command(label=self.t("额度说明…","Allowance details…"),command=self.explain)
        menu.add_separator()
        menu.add_command(label=self.t("退出","Quit"),command=self.close)
        try:
            menu.tk_popup(event.x_root,event.y_root)
        finally:
            menu.grab_release()

    def next_page(self):
        self.page=(self.page+1)%max(1,(len(self.views())+5)//6)
        self.draw()

    def choose_ball(self, provider_id, window_id):
        self.settings.update(ballService=provider_id,ballWindow=window_id)
        self.save()
        self.draw()

    def refresh(self):
        if self.busy or self.closed:
            return
        if self.demo:
            if not self.snapshots:
                self.snapshots = demo_snapshots()
            self.last_fetch = datetime.now(UTC)
            self.draw()
            return
        self.busy = True
        self.draw()
        previous = {s.provider_id:s for s in self.snapshots}
        def work():
            snapshots, banner = [], None
            try:
                if self.fixture:
                    for provider, parser in (("codex",parse_codex),("claude",parse_claude)):
                        try:
                            raw=read_json(self.fixture / (provider + ".json"))
                            snap=parser(raw)
                            if isinstance(raw,dict) and raw.get("status") == "login_required":
                                snap.status="login_required"
                            metadata=raw.get("_headroom",{}) if isinstance(raw,dict) else {}
                            if isinstance(metadata,dict):
                                if metadata.get("status") in ("fetch_error","updating","starting"):
                                    snap.status=metadata["status"]
                                if metadata.get("observedAt"):
                                    snap.observed_at=timestamp(metadata["observedAt"])
                            snapshots.append(snap)
                        except (OSError,ValueError):
                            snapshots.append(replace(previous.get(provider,Snapshot(provider,provider.title(),datetime.now(UTC))),status="fetch_error"))
                if self.manifest:
                    for provider,path in read_manifest(self.manifest).items():
                        try:
                            snap=parse_snapshot(read_json(path))
                            if snap.provider_id!=provider:
                                raise ValueError("Provider identity mismatch")
                            snapshots.append(snap)
                        except (OSError,ValueError):
                            snapshots.append(replace(previous.get(provider,Snapshot(provider,provider,datetime.now(UTC))),status="fetch_error"))
                if self.args.codex_cli:
                    from .codex_bridge import fetch_codex
                    try:
                        snapshots=[s for s in snapshots if s.provider_id!="codex"]+[fetch_codex()]
                    except (OSError,ValueError,RuntimeError,TimeoutError):
                        snapshots.append(replace(previous.get("codex",Snapshot("codex","Codex",datetime.now(UTC))),status="fetch_error"))
            except (OSError,ValueError):
                banner="provider-error"
                snapshots=[replace(s,status="fetch_error") for s in previous.values()]
            self.results.put((snapshots,banner))
        self.jobs.submit(work)

    def tick(self):
        if self.closed:
            return
        try:
            self.snapshots,self.error_banner=self.results.get_nowait()
            self.last_fetch=datetime.now(UTC)
            self.busy=False
        except queue.Empty:
            pass
        interval=max(60,int(self.settings.get("normalIntervalMinutes",5))*60) if self.args.codex_cli else 10
        if self.last_fetch and (datetime.now(UTC)-self.last_fetch).total_seconds()>=interval:
            self.refresh()
        self.draw()
        self.root.after(500,self.tick)

    def views(self):
        result=[]
        for snap in self.snapshots:
            windows=[w for w in snap.windows if w.support!="Unsupported"]
            if not windows:
                windows=[Window("none",self.t("额度","Allowance"),"Unsupported" if snap.windows else "Unknown")]
            elif not any(w.support=="Supported" for w in windows):
                windows=[Window("unknown",self.t("额度","Allowance"))]
            for window in windows:
                view=map_view(snap,window)
                if self.busy and view.condition not in ("Unsupported","Error"):
                    view.condition="Loading"
                result.append(view)
        return result

    def text(self,x,y,text,size=11,color=None,bold=False,anchor="w",width=None,max_width=None):
        skin=SKINS[self.settings["skin"]]
        families=tkfont.families(self.root)
        family=skin["font"]
        if self.settings["language"]!="en":
            family=next((f for f in ("Microsoft YaHei UI","PingFang SC","Noto Sans CJK SC","WenQuanYi Zen Hei") if f in families),"TkDefaultFont")
        elif family not in families:
            family="TkFixedFont" if self.settings["skin"]=="terminal" else "TkDefaultFont"
        if max_width:
            measure=tkfont.Font(root=self.root,family=family,size=size,weight="bold" if bold else "normal")
            original=text
            while text and measure.measure(text)>max_width:
                text=text[:-1]
            if text!=original:
                text=text[:-1]+"…"
        options=dict(text=text,fill=color or skin["text"],font=(family,size,"bold" if bold else "normal"),anchor=anchor)
        if width:
            options["width"]=width
        return self.canvas.create_text(x,y,**options)

    def color(self,view):
        skin=SKINS[self.settings["skin"]]
        if not view.current or not view.has_value:
            return skin["muted"]
        if view.window.remaining<=30:
            return "#c94747" if self.settings["skin"]=="paper" else "#ff7377"
        if view.window.remaining<=50:
            return "#95600d" if self.settings["skin"]=="paper" else "#efb65b"
        return "#d79a65" if self.settings["skin"]=="midnight" and view.snapshot.provider_id=="claude" else skin["accent"]

    def value(self,view):
        if not view.has_value:
            return "—"
        remaining=view.window.remaining
        value=f"{remaining:.1f}%" if 0<remaining<1 or 99<remaining<100 else f"{remaining:.0f}%"
        return ("" if view.current else "~")+value

    def state(self,view):
        return (EN_STATES if self.settings["language"]=="en" else ZH_STATES)[view.condition]

    def window_name(self,window):
        return self.t("每周","Weekly") if window.id=="weekly" else window.label

    def selected_window_name(self,view):
        return self.window_name(view.window) if view.window.support=="Supported" else self.t("无支持窗口","No supported window") if view.window.support=="Unsupported" else self.t("窗口未知","Window unknown")

    def track(self,x,y,width,view):
        skin=SKINS[self.settings["skin"]]
        self.canvas.create_rectangle(x,y,x+width,y+7,fill=skin["track"],outline="")
        fraction=view.window.remaining/100
        if skin is SKINS["terminal"]:
            for index in range(20):
                if (index+1)/20<=fraction:
                    self.canvas.create_rectangle(x+index*width/20,y,x+(index+1)*width/20-3,y+7,fill=self.color(view),outline="")
        elif fraction>0:
            self.canvas.create_rectangle(x,y,x+width*fraction,y+7,fill=self.color(view),outline="")
        if not view.current:
            for offset in range(0,width,8):
                self.canvas.create_line(x+offset,y+7,x+offset+6,y,fill=skin["bg"])

    def draw(self):
        skin=SKINS[self.settings["skin"]]
        self.canvas.delete("all")
        self.canvas.configure(bg=skin["bg"])
        if not self.snapshots:
            self.snapshots=[Snapshot("codex","Codex",datetime.now(UTC),status="starting" if self.busy else "no_data")]
        selected=select(self.snapshots,self.settings)
        if self.busy and selected.condition not in ("Unsupported","Error"):
            selected.condition="Loading"
        if self.mode=="ball":
            self.draw_sphere()
            return
        detail=self.mode=="edge"
        all_rows=self.views()
        pages=max(1,(len(all_rows)+5)//6)
        self.page=min(self.page,pages-1)
        rows=all_rows[self.page*6:(self.page+1)*6]
        width=460 if detail else 370
        row_h=81 if detail else 57
        height=70+len(rows)*row_h+66
        self.canvas.configure(width=width,height=height)
        title="> HEADROOM" if self.settings["skin"]=="terminal" else "HEADROOM"
        self.text(20,25,title,13,bold=True)
        self.text(width-20,25,self.t("示例数据","DEMO DATA") if self.demo else self.t("剩余额度","REMAINING"),9,skin["muted"],anchor="e")
        self.text(20,49,self.t("右键：皮肤、额度与语言","Right-click: skins, allowance & language"),9,skin["muted"])
        y=68
        for view in rows:
            if self.settings["skin"]=="paper":
                self.canvas.create_rectangle(12,y-2,width-12,y+row_h-7,fill=skin["surface"],outline="")
                self.canvas.create_rectangle(12,y-2,15,y+row_h-7,fill=self.color(view),outline="")
            self.text(22,y+12,view.snapshot.name+" · "+self.window_name(view.window),11,bold=True,max_width=width-130)
            self.text(width-22,y+12,self.value(view),14,self.color(view),True,"e")
            if view.has_value:
                self.track(22,y+31,width-44,view)
            else:
                self.text(22,y+34,self.state(view),10,skin["muted"])
            if detail:
                reset=self.t("重置时间未提供","Reset not supplied")
                if view.window.reset:
                    try:
                        reset=self.t("重置 ","Reset ")+timestamp(view.window.reset).astimezone().strftime("%m-%d %H:%M %z")
                    except ValueError:
                        reset=self.t("提供方重置时间：","Provider reset: ")+view.window.reset
                note=(self.state(view)+" · " if view.condition=="Exhausted" else "")+reset if view.current else self.state(view)+(self.t(" · 上次读数"," · last reading") if view.has_value else "")
                if not view.has_value:
                    note=self.t("刷新以重试","Refresh to retry") if view.condition=="Error" else self.t("提供方未提供数值额度","Provider offers no numeric allowance") if view.condition=="Unsupported" else self.t("等待读取提供方数据","Waiting for provider data")
                self.text(22,y+56,note,9,skin["muted"],max_width=width-44)
            elif not view.current and view.has_value:
                self.text(width-22,y+46,self.state(view)+self.t(" · 上次读数"," · last reading"),8,skin["muted"],anchor="e")
            y+=row_h
        note=self.t("浮窗同时显示所有可用额度","Sphere: arcs + liquid / remaining")
        if selected.fallback=="weekly-only":
            note+=self.t(" · 无 5h 窗口"," · no 5h window")
        elif selected.fallback:
            note+=self.t(" · 自动替代"," · fallback")
        if pages>1:
            note+=f" · {self.page+1}/{pages}"
        if self.error_banner:
            note=self.t("提供方清单读取失败","Could not read provider manifest")
        self.text(20,y+10,note,9,skin["muted"])
        self.text(20,y+39,self.t("F5 刷新 · 空格：悬浮球","F5 refresh · Space: ball"),9,skin["muted"])
        self.text(width-20,y+39,(self.t("紧凑","Compact") if detail else self.t("详细","Details")),9,skin["accent"],anchor="e")
        self.toggle_zone=(width-150,y+26,width,y+55)

    def legend_text(self):
        lines=[self.t("外环 Claude 5h / 内环 Claude 每周 / 液体 Codex 每周", "Outer: Claude 5h / inner: Claude weekly / liquid: Codex weekly"),
               self.t("细蓝环 Codex 5h（仅提供时）/ 全部表示剩余", "Thin blue: Codex 5h (only if supplied) / all remaining"),
               self.t("! 读取失败 / ~ 旧读数 / ... 加载 / ? 未知 / - 不提供 / 0 用尽", "! error / ~ old / ... loading / ? unknown / - not offered / 0 exhausted")]
        for view in self.views():
            lines.append(view.snapshot.name+" / "+self.selected_window_name(view)+" / "+self.value(view)+" / "+self.state(view))
            if view.window.reset:lines.append(self.t("重置：","Reset: ")+view.window.reset)
        if self.demo:lines.append(self.t("示例数据","DEMO DATA"))
        return "\n".join(lines)

    def schedule_legend(self,event=None):
        self.hide_legend()
        if self.mode=="ball":self.legend_job=self.root.after(450,self.show_legend)

    def show_legend(self):
        self.legend_job=None
        if self.closed or self.mode!="ball" or self.legend_window:return
        tip=tk.Toplevel(self.root);self.legend_window=tip
        tip.overrideredirect(True)
        try:tip.attributes("-topmost",True)
        except tk.TclError:pass
        skin=SKINS[self.settings["skin"]]
        tk.Label(tip,text=self.legend_text(),justify="left",background=skin["surface"],foreground=skin["text"],padx=10,pady=8,wraplength=430).pack()
        tip.update_idletasks()
        x=min(self.root.winfo_rootx()+self.root.winfo_width()+8,self.root.winfo_screenwidth()-tip.winfo_reqwidth())
        y=min(self.root.winfo_rooty(),self.root.winfo_screenheight()-tip.winfo_reqheight())
        tip.geometry(f"+{max(0,x)}+{max(0,y)}")

    def hide_legend(self):
        if self.legend_job:
            self.root.after_cancel(self.legend_job);self.legend_job=None
        if self.legend_window:
            self.legend_window.destroy();self.legend_window=None

    def explain(self):
        messagebox.showinfo("Headroom",self.legend_text(),parent=self.root)

    def draw_sphere(self):
        skin=SKINS[self.settings["skin"]]
        # Keep the existing normal OS window; Windows caption controls require 120px.
        diameter=max(64,min(96,int(self.settings.get("ballSize",64))))
        scale=diameter/64; width=max(120,diameter+12);height=diameter+26
        self.canvas.configure(width=width,height=height)
        cx,cy=width/2,diameter/2+4
        def box(radius):return (cx-radius*scale,cy-radius*scale,cx+radius*scale,cy+radius*scale)
        self.canvas.create_oval(*box(31),fill=skin["bg"],outline=skin["border"],width=scale,tags="sphere-outline")
        colors=("#a45c1b","#227d5e","#3986b4") if skin is SKINS["paper"] else ("#bef375","#65dea7","#3c9a6c") if skin is SKINS["terminal"] else ("#ebb168","#65dea7","#53a4da")
        interval=self.settings.get("normalIntervalMinutes",5)
        def quota(provider,window):
            view=sphere_window(self.snapshots,provider,window,interval=interval)
            if view and self.busy and view.condition!="Error":view.condition="Loading"
            return view
        for provider,window,radius,color,weight in [("claude","five-hour",29,colors[0],3),("claude","weekly",24,colors[1],3),("codex","five-hour",19.5,colors[2],2)]:
            view=quota(provider,window)
            if view is None:continue
            tag=provider+"-"+window
            self.canvas.create_oval(*box(radius),outline=skin["track"],width=weight*scale,tags=tag+"-track")
            if not view.has_value:
                self.canvas.create_oval(*box(radius),outline=skin["muted"],width=weight*scale,dash=(1,3),tags=tag+"-unknown");continue
            if view.window.remaining==100:
                self.canvas.create_oval(*box(radius),outline=color if view.current else skin["muted"],width=weight*scale,dash=(1,2) if not view.current or skin is SKINS["terminal"] else (),tags=tag+"-value")
            elif view.window.remaining>0:
                self.canvas.create_arc(*box(radius),start=90,extent=-min(view.window.remaining*3.6,359.999),style="arc",outline=color if view.current else skin["muted"],width=weight*scale,dash=(1,2) if not view.current or skin is SKINS["terminal"] else (),tags=tag+"-value")
        self.canvas.create_oval(*box(17),fill=skin["surface"],outline="",tags="liquid-disk")
        liquid=quota("codex","weekly")
        if liquid and liquid.has_value:
            points=liquid_segment(liquid.window.remaining/100)
            if points:self.canvas.create_polygon(*[v for x,y in points for v in (cx+17*scale*x,cy+17*scale*y)],fill=colors[2] if liquid.current else skin["muted"],outline="",stipple="" if liquid.current else "gray50",tags="liquid-fill")
        if liquid is None:
            snap=next((v for v in self.snapshots if v.provider_id=="codex"),None)
            if snap:
                window=next((w for w in snap.windows if w.id=="weekly"),Window("weekly","7d"))
                liquid=map_view(snap,window,interval=interval)
        marks={"Error":"!","Stale":"~","Loading":"...","Unknown":"?","Unsupported":"-"}
        value=self.value(liquid) if liquid and liquid.has_value else marks.get(liquid.condition,"-") if liquid else "-"
        def text(text,y,size,color):
            font=("Consolas" if skin is SKINS["terminal"] else "Segoe UI",-max(1,round(size*scale)),"bold")
            for dx,dy in [(-1,0),(1,0),(0,-1),(0,1)]:self.canvas.create_text(cx+dx,cy+(y-32)*scale+dy,text=text,font=font,fill=skin["bg"],tags="sphere-text-halo")
            self.canvas.create_text(cx,cy+(y-32)*scale,text=text,font=font,fill=color,tags="sphere-text")
        text("CX7",27,7,skin["text"]);text(value,37,10,"#f47070" if liquid and liquid.condition=="Exhausted" else skin["text"])
        views=self.views();states={v.condition for v in views}
        marker=next((mark for state,mark in [("Error","!"),("Stale","~"),("Loading","..."),("Unknown","?"),("Exhausted","0")] if state in states),"")
        if self.busy and not marker:marker="..."
        if not marker and any(v.window.support=="Supported" and not(v.snapshot.provider_id in ("claude","codex") and v.window.id in ("weekly","five-hour")) for v in views):marker="+"
        text(marker,54,8,skin["text"])
        self.text(width/2,height-9,self.t("示例","DEMO") if self.demo else self.t("悬停图例","Hover: legend"),8,skin["muted"],anchor="center")

    def start_drag(self,event):
        self.hide_legend()
        # Query and set WM geometry in the same coordinate system. winfo_x/y
        # can include client decoration offsets on X11, causing a jump per drag.
        geometry=re.fullmatch(r"\d+x\d+([+-])(\d+)([+-])(\d+)",self.root.geometry())
        if geometry is None:
            self.drag=None
            return
        sx,left,sy,top=geometry.groups()
        self.drag=(event.x_root,event.y_root,int(left),int(top),sx,sy)

    def move_drag(self,event):
        if self.drag:
            x,y,left,top,sx,sy=self.drag
            # Negative WM offsets are distances from the right/bottom edges.
            left=max(0,left+(event.x_root-x)*(1 if sx=="+" else -1))
            top=max(0,top+(event.y_root-y)*(1 if sy=="+" else -1))
            self.root.geometry(f"{sx}{left}{sy}{top}")

    def release_drag(self,event):
        if self.drag and abs(event.x_root-self.drag[0])+abs(event.y_root-self.drag[1])<4 and self.mode!="ball":
            x1,y1,x2,y2=getattr(self,"toggle_zone",(0,0,0,0))
            if x1<=event.x<=x2 and y1<=event.y<=y2:
                self.set_mode("compact" if self.mode=="edge" else "edge")
        self.drag=None

    def close(self):
        self.hide_legend()
        self.closed=True
        self.jobs.shutdown(wait=False,cancel_futures=True)
        self.root.destroy()


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--providers",help="Local provider manifest JSON")
    parser.add_argument("--fixture",help="Directory with synthetic codex.json and claude.json")
    parser.add_argument("--settings",help="Isolated portable settings JSON")
    parser.add_argument("--codex-cli",action="store_true",help="Opt in to read-only account/rateLimits/read through an existing signed-in Codex CLI")
    parser.add_argument("--smoke-test",metavar="OUTPUT_DIR",help="Capture synthetic UI states and exit (Pillow required)")
    args=parser.parse_args(argv)
    if args.smoke_test:
        args.codex_cli=False
        args.providers=None
        args.fixture=None
        args.settings=str(Path(args.smoke_test)/"isolated-portable-settings.json")
    if sys.platform == "win32":
        # Keep Tk geometry and screenshot/device pixels in the same coordinate space.
        import ctypes
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except (AttributeError, OSError):
            pass
    root=tk.Tk()
    app=HeadroomApp(root,args)
    if args.smoke_test:
        from .preview import run_smoke
        root.after(200,lambda:run_smoke(app,Path(args.smoke_test)))
    root.mainloop()

if __name__=="__main__":
    main()
