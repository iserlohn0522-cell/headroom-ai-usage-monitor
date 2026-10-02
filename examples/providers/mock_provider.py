"""Write a synthetic v1 snapshot and manifest. No network or credential access."""
import argparse
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import uuid


def atomic_json(path, payload):
    temporary=path.with_name(path.name+"."+uuid.uuid4().hex+".tmp")
    temporary.write_text(json.dumps(payload,indent=2)+"\n",encoding="utf-8")
    os.replace(temporary,path)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out",required=True,type=Path)
    parser.add_argument("--scenario",choices=("weekly-only","two-window"),default="weekly-only")
    parser.add_argument("--state",choices=("fresh","exhausted","loading","stale","error","unsupported"),default="fresh")
    args=parser.parse_args()
    args.out.mkdir(parents=True,exist_ok=True)
    now=datetime.now(timezone.utc)
    observed=now-timedelta(minutes=30) if args.state=="stale" else now
    weekly={"id":"weekly","label":"7d","support":"supported","remainingPercent":0 if args.state=="exhausted" else 68,"state":"fresh","reset":{"kind":"rolling","at":(now+timedelta(days=4)).isoformat()}}
    five={"id":"five-hour","label":"5h","support":"unsupported"}
    if args.scenario=="two-window":
        five.update(support="supported",remainingPercent=82,reset={"kind":"fixed","at":(now+timedelta(hours=3)).isoformat()})
    if args.state=="unsupported":
        weekly={"id":"weekly","label":"7d","support":"unsupported"}
    payload={"schemaVersion":1,"providerId":"demo-plan","displayName":"Demo Plan (mock)","observedAt":observed.isoformat(),"status":{"loading":"loading","error":"error"}.get(args.state,"ok"),"windows":[weekly,five]}
    atomic_json(args.out/"mock.json",payload)
    atomic_json(args.out/"manifest.json",{"schemaVersion":1,"providers":[{"id":"demo-plan","path":"mock.json"}]})
    print(args.out/"manifest.json")

if __name__=="__main__":main()
