"""Opt-in graphical Tk integration tests; fixtures only, retained test artifacts."""
import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import tempfile
import time
import unittest
import tkinter as tk
from portable.app import HeadroomApp
from portable.core import UTC, select

@unittest.skipUnless(os.environ.get("HEADROOM_UI_TESTS")=="1","set HEADROOM_UI_TESTS=1 on a graphical session or Xvfb")
class PortableUiTests(unittest.TestCase):
    def test_local_provider_recovery_and_controls(self):
        folder=Path(tempfile.mkdtemp(prefix="HeadroomUiTests-"))
        snapshot=folder/"mock.json"
        manifest=folder/"manifest.json"
        manifest.write_text(json.dumps({"schemaVersion":1,"providers":[{"id":"demo-plan","path":"mock.json"}]}))
        raw={"schemaVersion":1,"providerId":"demo-plan","displayName":"Demo Plan","observedAt":datetime.now(UTC).isoformat(),"status":"ok","windows":[{"id":"weekly","label":"7d","support":"supported","remainingPercent":68},{"id":"five-hour","label":"5h","support":"unsupported"}]}
        snapshot.write_text(json.dumps(raw))
        args=argparse.Namespace(settings=str(folder/"settings.json"),providers=str(manifest),fixture=None,codex_cli=False,smoke_test=None)
        root=tk.Tk();root.withdraw()
        app=HeadroomApp(root,args)
        def finish():
            deadline=time.monotonic()+3
            while app.busy and time.monotonic()<deadline:
                root.update();time.sleep(.01)
            self.assertFalse(app.busy,"provider refresh did not finish")
        try:
            finish()
            self.assertEqual(select(app.snapshots,app.settings).window.remaining,68)
            self.assertEqual(len(app.views()),1)
            app.set_pref("skin","paper"); app.set_pref("language","en"); app.choose_ball("demo-plan","five-hour")
            self.assertEqual(select(app.snapshots,app.settings).fallback,"preferred-unavailable")
            app.set_mode("ball");self.assertEqual(app.mode,"ball")
            from portable.app import demo_snapshots
            saved_snapshots=app.snapshots;app.snapshots=demo_snapshots();app.draw()
            self.assertEqual(len(app.views()),3)
            self.assertTrue(app.canvas.find_withtag("claude-five-hour-value"))
            self.assertTrue(app.canvas.find_withtag("claude-weekly-value"))
            self.assertTrue(app.canvas.find_withtag("liquid-fill"))
            self.assertFalse(app.canvas.find_withtag("codex-five-hour-track"))
            self.assertAlmostEqual(float(app.canvas.itemcget(app.canvas.find_withtag("claude-five-hour-value")[0],"extent")),-295.2)
            self.assertEqual(int(app.canvas.cget("height")),90)
            for expected in ("Claude 5h","Claude weekly","Codex weekly"):self.assertIn(expected,app.legend_text())
            app.schedule_legend();root.after(500,root.quit);root.mainloop()
            self.assertIsNotNone(app.legend_window)
            app.hide_legend();self.assertIsNone(app.legend_window)
            app.settings["ballSize"]=96;app.draw()
            self.assertEqual(int(app.canvas.cget("height")),122)
            app.settings["ballSize"]=64
            app.snapshots=saved_snapshots

            app.set_mode("edge");self.assertEqual(app.mode,"edge")
            snapshot.write_text("{partial")
            app.refresh();finish()
            selected=select(app.snapshots,app.settings)
            self.assertEqual((selected.condition,selected.window.remaining),("Error",68))
            raw["windows"][0]["remainingPercent"]=42
            snapshot.write_text(json.dumps(raw));app.refresh();finish()
            selected=select(app.snapshots,app.settings)
            self.assertEqual((selected.condition,selected.window.remaining),("Fresh",42))
            saved=json.loads((folder/"settings.json").read_text())
            self.assertEqual((saved["skin"],saved["language"]),("paper","en"))
            # With a real WM, decorations must not accumulate across drags.
            root.deiconify();root.geometry("+200+200");root.update()
            for _ in range(2):
                before=(root.winfo_rootx(),root.winfo_rooty())
                app.start_drag(argparse.Namespace(x_root=500,y_root=400))
                event=argparse.Namespace(x_root=520,y_root=420)
                app.move_drag(event);app.release_drag(event)
                deadline=time.monotonic()+2
                while time.monotonic()<deadline:
                    root.update()
                    after=(root.winfo_rootx(),root.winfo_rooty())
                    if after==(before[0]+20,before[1]+20):break
                    time.sleep(.01)
                self.assertEqual(after,(before[0]+20,before[1]+20))
        finally:
            app.close()

if __name__=="__main__":unittest.main()
