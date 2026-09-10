"""POST a masked edit to an OpenAI-compatible /images/edits endpoint."""
import json, os, sys, uuid, urllib.request, urllib.error, ssl
from pathlib import Path

src, mask, out, prompt = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
model = sys.argv[5]
size = sys.argv[6] if len(sys.argv) > 6 else None

cfg_path = Path(os.environ.get("CODEX_HOME") or (Path.home() / ".codex")) / "imagegen-compat.json"
cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
prov = cfg["providers"]["ofoxai"]
key, base = prov["api_key"], prov["base_url"].rstrip("/")
proxy = cfg.get("proxy")
if proxy:
    urllib.request.install_opener(urllib.request.build_opener(
        urllib.request.ProxyHandler({"http": proxy, "https": proxy})))
    print("proxy:", proxy)
url = base + "/images/edits"
print("POST", url, "model:", model)

fields = {"model": model, "prompt": prompt}
if size:
    fields["size"] = size
boundary = "----codex" + uuid.uuid4().hex
body = b""
for k, v in fields.items():
    body += (f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n').encode("utf-8")
for k, path in (("image", src), ("mask", mask)):
    data = Path(path).read_bytes()
    body += (f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"; '
             f'filename="{Path(path).name}"\r\nContent-Type: image/png\r\n\r\n').encode("utf-8")
    body += data + b"\r\n"
body += f"--{boundary}--\r\n".encode("utf-8")

req = urllib.request.Request(url, data=body, method="POST", headers={
    "Authorization": f"Bearer {key}",
    "Content-Type": f"multipart/form-data; boundary={boundary}",
    "Accept": "application/json",
})
try:
    with urllib.request.urlopen(req, timeout=300) as r:
        raw = r.read()
except urllib.error.HTTPError as e:
    print("HTTP", e.code, e.read().decode("utf-8", "replace")[:1500]); raise SystemExit(3)
except Exception as e:
    print("ERR", type(e).__name__, e); raise SystemExit(3)

payload = json.loads(raw.decode("utf-8"))
items = payload.get("data") or payload.get("images") or payload.get("artifacts") or []
if not items:
    print("no image; keys:", sorted(payload)[:12]); print(json.dumps(payload)[:800]); raise SystemExit(3)
import base64
item = items[0]
if isinstance(item, dict):
    b64 = item.get("b64_json") or item.get("base64") or item.get("image_base64")
    if b64:
        Path(out).write_bytes(base64.b64decode(b64))
    else:
        u = item.get("url") or item.get("image_url")
        print("downloading", u)
        with urllib.request.urlopen(u, timeout=300) as r:
            Path(out).write_bytes(r.read())
else:
    s = str(item)
    Path(out).write_bytes(base64.b64decode(s) if not s.startswith("http") else urllib.request.urlopen(s).read())
from PIL import Image
im = Image.open(out)
print("saved", out, im.size, im.mode, os.path.getsize(out), "B")
