"""Build a source-only portable ZIP from explicit non-secret paths. Never overwrite."""
import argparse
from pathlib import Path
import zipfile

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version",required=True)
    parser.add_argument("--output",required=True,type=Path)
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[1]
    files=set()
    for pattern in ("portable/*.py","portable/README.md","examples/providers/*.py","examples/providers/*.json","docs/providers/*.md","docs/providers/*.json","docs/previews/current/*","docs/fixtures/*/*.json","tests/test_portable*.py","tests/allowance-cases.json","LICENSE","README.md","README.zh-CN.md"):
        files.update(p for p in root.glob(pattern) if p.is_file())
    args.output.parent.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(args.output,"x",zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(files):archive.write(path,path.relative_to(root).as_posix())
        archive.writestr("START-HERE.txt",f"Headroom portable {args.version}\nPython 3.10+ and verified Tk 8.6 required; newer Tk needs visual font validation. See portable/README.md for setup, sphere mapping and platform coverage.\nLinux (Debian/Ubuntu): /usr/bin/python3 -m portable.app --settings preview-settings.json\nmacOS: python3 -m portable.app --settings preview-settings.json\nWindows: python -m portable.app --settings preview-settings.json\nLinux: prefer distro Python/Tk; bundled Tk 9.0.4 showed incorrect Chinese rendering in QA despite passing tests. Follow the README preflight.\nNo account required for the labeled demo.\n")
    with zipfile.ZipFile(args.output) as archive:
        assert archive.testzip() is None
    print(args.output)

if __name__=="__main__":main()
