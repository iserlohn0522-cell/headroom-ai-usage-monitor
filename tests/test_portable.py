import io
import json
from datetime import datetime, timedelta
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from portable import core
from portable.codex_bridge import from_rate_limits, fetch_codex

ROOT=Path(__file__).resolve().parents[1]

class PortableTests(unittest.TestCase):
    def test_shared_conformance(self):
        for case in json.loads((ROOT/"tests/allowance-cases.json").read_text()):
            with self.subTest(case=case["name"]):
                now=datetime.now(core.UTC)
                snap=core.parse_codex(case["payload"],now-timedelta(minutes=case.get("ageMinutes",0)))
                snap.status=case.get("serviceStatus",snap.status)
                view=core.select([snap],{"ballWindow":case.get("preference","auto")},now)
                self.assertEqual((view.window.id,view.condition,view.window.remaining),(case["selected"],case["state"],case["remaining"]))
                self.assertEqual(next(w for w in snap.windows if w.id=="five-hour").support,case["fiveSupport"])

    def test_language_and_migration(self):
        for value,expected in ((None,"zh-CN"),("ja","zh-CN"),("en","en"),("EN","en"),("de","zh-CN")):
            self.assertEqual(core.language(value),expected)
        prefs=core.migrate_settings({"settingsVersion":2,"language":"en","ballWindow":"five-hour","showClaude":False,"futureField":"keep"})
        self.assertEqual((prefs["settingsVersion"],prefs["ballWindow"],prefs["language"],prefs["futureField"]),(3,"five-hour","en","keep"))
        self.assertFalse(prefs["showClaude"])
        self.assertEqual(core.migrate_settings({"skin":"invalid"})["skin"],"midnight")

    def test_settings_paths(self):
        home=Path("/test-home")
        self.assertEqual(core.settings_path("darwin",{},home),home/"Library/Application Support/Headroom/settings.json")
        self.assertEqual(core.settings_path("linux",{},home),home/".config/headroom/settings.json")
        self.assertEqual(core.settings_path("linux",{"XDG_CONFIG_HOME":"/config"},home),Path("/config/headroom/settings.json"))
        self.assertEqual(core.settings_path("win32",{"LOCALAPPDATA":"/local"},home),Path("/local/Headroom/portable-settings.json"))
        self.assertEqual(core.settings_path("darwin",{"HEADROOM_SETTINGS_PATH":"/override.json"},home),Path("/override.json"))

    def snapshot(self):
        return json.loads((ROOT/"examples/providers/mock.json").read_text())

    def test_valid_contract_and_resets(self):
        snap=core.parse_snapshot(self.snapshot())
        self.assertEqual(snap.windows[0].reset_kind,"rolling")
        self.assertEqual(snap.windows[0].reset,"2026-10-06T12:00:00Z")
        self.assertEqual(snap.windows[1].support,"Unsupported")

    def test_invalid_contract(self):
        for invalid in (-1,101,"68",float("nan"),float("inf"),True):
            with self.subTest(value=invalid):
                raw=self.snapshot();raw["windows"][0]["remainingPercent"]=invalid
                with self.assertRaises(ValueError):core.parse_snapshot(raw)
        for key,value in (("schemaVersion",2),("schemaVersion",True),("observedAt","2026-10-02T12:00:00"),("status","bad")):
            raw=self.snapshot();raw[key]=value
            with self.assertRaises(ValueError):core.parse_snapshot(raw)
        raw=self.snapshot();raw["windows"][0]["support"]="unsupported"
        with self.assertRaises(ValueError):core.parse_snapshot(raw)
        raw=self.snapshot();raw["windows"].append(raw["windows"][0])
        with self.assertRaises(ValueError):core.parse_snapshot(raw)
        raw=self.snapshot();raw["windows"][0]["reset"]="tomorrow"
        with self.assertRaises(ValueError):core.parse_snapshot(raw)

    def test_manifest_and_limits(self):
        self.assertIn("demo-plan",core.read_manifest(ROOT/"examples/providers/manifest.json"))
        # Kept as evidence: this suite does not delete files.
        folder=Path(tempfile.mkdtemp(prefix="HeadroomPortableTests-"))
        path=folder/"manifest.json"
        for relative in ("../outside.json","snapshot.txt"):
            path.write_text(json.dumps({"schemaVersion":1,"providers":[{"id":"demo","path":relative}]}))
            with self.assertRaises(ValueError):core.read_manifest(path)
        path.write_text("x"*(core.MAX_BYTES+1))
        with self.assertRaises(ValueError):core.read_json(path)

    def test_claude_reset_and_missing(self):
        at="2026-10-06T09:00:00-05:00"
        snap=core.parse_claude({"five_hour":None,"seven_day":{"utilization":100,"resets_at":at}})
        self.assertEqual(snap.windows[1].reset,at)
        self.assertEqual(core.select([snap],{}).condition,"Exhausted")
        self.assertEqual(snap.windows[0].support,"Unsupported")

    def test_selection_does_not_average_services(self):
        first=core.parse_codex({"primary_window":{"used_percent":30,"limit_window_seconds":604800}})
        other=core.parse_claude({"five_hour":{"utilization":100}})
        self.assertEqual(core.select([other,first],{"ballService":"codex"}).window.remaining,70)
        self.assertEqual(core.select([other,first],{"ballService":"claude"}).window.remaining,0)

    def test_error_age_is_observation_age(self):
        now=datetime.now(core.UTC)
        snap=core.parse_codex({"primary_window":{"used_percent":32,"limit_window_seconds":604800}},now-timedelta(minutes=30))
        observed=snap.observed_at
        snap.status="fetch_error"
        self.assertEqual(core.select([snap],{},now).condition,"Error")
        snap.status=None
        self.assertEqual(core.select([snap],{},now).condition,"Stale")
        self.assertEqual(snap.observed_at,observed)

    def test_bridge_maps_actual_duration_and_bucket(self):
        result={"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":32,"windowDurationMins":10080,"resetsAt":1791288000},"secondary":None},"other":{"primary":{"usedPercent":99}}},"credits":{"balance":"62500"}}
        snap=from_rate_limits(result)
        self.assertEqual(core.select([snap],{}).window.id,"weekly")
        self.assertEqual(core.select([snap],{}).window.remaining,68)
        result["rateLimitsByLimitId"]["codex"]["primary"]["windowDurationMins"]=15
        self.assertEqual(core.select([from_rate_limits(result)],{}).window.label,"15m")

    def test_error_visible_without_supported_windows(self):
        snap=core.Snapshot("test","Test",datetime.now(core.UTC),status="fetch_error")
        window=core.Window("weekly","7d","Unsupported")
        view=core.map_view(snap,window)
        self.assertEqual(view.condition,"Error")
        self.assertFalse(view.has_value)

    def test_bridge_read_only_protocol(self):
        class Process:
            def __init__(self):
                self.stdin=io.StringIO()
                self.stdout=io.StringIO(json.dumps({"id":1,"method":"account/chatgptAuthTokens/refresh","params":{}})+"\n"+json.dumps({"id":1,"result":{}})+"\n"+json.dumps({"id":2,"result":{"rateLimits":{"primary":{"usedPercent":32,"windowDurationMins":10080},"secondary":None}}})+"\n")
                self.sent=""
            def terminate(self):self.sent=self.stdin.getvalue()
            def wait(self,timeout):return 0
        process=Process()
        captured=[]
        def popen(args,**kwargs):captured.append(args);return process
        snap=fetch_codex(executable="fake-codex",popen=popen)
        messages=[json.loads(line) for line in process.sent.splitlines()]
        methods=[message["method"] for message in messages if "method" in message]
        self.assertTrue(any("error" in message for message in messages))
        self.assertEqual(methods,["initialize","initialized","account/rateLimits/read"])
        self.assertEqual(captured,[["fake-codex","app-server"]])
        self.assertEqual(core.select([snap],{}).window.remaining,68)

if __name__=="__main__":unittest.main()
