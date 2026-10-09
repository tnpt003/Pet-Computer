"""Subprocess fault injection used only by C# bridge integration tests."""
import json
from pathlib import Path
import sys
import time

marker = Path(sys.argv[sys.argv.index("--memory") + 1])
mode = marker.name
for line in sys.stdin:
    request = json.loads(line)
    if mode == "hang":
        time.sleep(30)
    elif mode == "exit":
        sys.exit(1)
    elif mode == "malformed":
        print("not json", flush=True)
    elif mode == "oversize":
        print("x" * 9000, flush=True)
    elif mode == "recover" and not marker.exists():
        marker.touch()
        print("broken", flush=True)
    else:
        if mode == "stderr":
            sys.stderr.write("diagnostic " * 50000)
            sys.stderr.flush()
        print(json.dumps({"version": 1, "type": "decision",
                          "id": request["id"] + (1 if mode == "wrong_id" else 0),
                          "command": "SIT", "duration_seconds": 2, "mood": "content"}), flush=True)
