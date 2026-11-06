"""Read-only pre-merge checks. Does not modify Git, connect to accounts, or deploy."""
import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
subprocess.run(["python3" if __import__("os").name != "nt" else __import__("sys").executable,
                str(root / "Tools/sync_solo_economy.py"), "--check"], check=True)
files = [
    "Assets/Resources/SoloEconomy.json",
    "Assets/Scripts/Scripts/GameManager/SoloEconomy.cs",
    "Assets/Scripts/Scripts/GameManager/SoloEconomyConnection.cs",
    "Assets/Scripts/Scripts/GameManager/TrialRunFlow.cs",
    "Assets/Editor/SoloEconomyAuthoring.cs",
    "Assets/Editor/SoloEconomyPlayChecks.cs",
    "Assets/Editor/SoloShopPlayChecks.cs",
]
new_guids = []
for name in files:
    source = root / name
    assert source.is_file(), f"Missing integration file: {name}"
    meta = source.with_name(source.name + ".meta")
    assert meta.is_file(), f"Commit this Unity metadata with its asset: {meta.relative_to(root)}"
    match = re.search(r"^guid: ([a-f0-9]{32})$", meta.read_text(encoding="utf-8-sig"), re.M)
    assert match, f"Invalid metadata GUID: {meta}"
    new_guids.append(match.group(1))
assert len(set(new_guids)) == len(new_guids), "Duplicate economy asset GUIDs"
for directory in ["Backend/commerce", "Assets/Scripts", "Assets/Editor"]:
    for file in (root / directory).rglob("*"):
        if not file.is_file() or file.suffix not in {".cs", ".js", ".json"} or "node_modules" in file.parts:
            continue
        text = file.read_text(encoding="utf-8-sig", errors="replace")
        assert not re.search(r"^(?:<<<<<<< |=======$|>>>>>>> )", text, re.M), f"Unresolved merge marker in {file}"
bank = json.loads((root / "Backend/commerce/quiz-questions.json").read_text(encoding="utf-8"))
for category, prefix in [("truthTable", "tt-"), ("inference", "roi-")]:
    questions = bank[category]
    assert len({q["id"] for q in questions}) == len(questions)
    assert all(q["id"].startswith(prefix) and len(q["choices"]) == 4 and 0 <= q["correctChoice"] < 4 for q in questions)
assert len(bank["inference"]) == 20
print("PASS: economy policy, quiz contract, Unity metadata and merge markers. No Git or account data changed.")
