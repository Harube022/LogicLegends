"""Sync the Unity display fallback from the authoritative backend economy. No balances are changed."""
import argparse
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / "Backend/commerce/economy.json"
target = root / "Assets/Resources/SoloEconomy.json"
parser = argparse.ArgumentParser()
parser.add_argument("--check", action="store_true")
args = parser.parse_args()
data = json.loads(source.read_text(encoding="utf-8"))
assert len(data["trials"]) == 10 and data["maxRetries"] == 2
assert all(p["baseReward"] >= 0 and p["quizBonusLimit"] >= 0 for p in data["trials"])
if args.check:
    assert json.loads(target.read_text(encoding="utf-8")) == data, "Run Tools/sync_solo_economy.py before building."
else:
    target.write_text(source.read_text(encoding="utf-8"), encoding="utf-8")
print("Solo economy display configuration matches backend version", data["version"])
