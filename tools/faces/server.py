#!/usr/bin/env python3
"""A small local server in front of ComfyUI for the page: the player types what his club's crest should look like, the page POSTs it here.

  python3 server.py --comfy http://127.0.0.1:8188 --port 8765
  POST /logo  {"prompt": "...", "name": "Club name"}  -> a PNG
  POST /face  {"key": "123", "age": 24, "look": {...}} -> a WebP (also saved in --faces and added to its manifest.json)
  GET  /health -> {"ok": true}
The page is told where it is with  #faces=http://127.0.0.1:8765  or the setting in the page. One job at a time.
"""
import argparse, hmac, io, json, os, re, secrets, sys, threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prompts, comfy, imaging
from generate_logo import make_logo

LOCK = threading.Lock()
CFG = {}
KEY_RE = re.compile(r"^[0-9]{1,6}$")


def origin_allowed(origin):
    if origin in ("https://soccer.dev-spirit.com", "http://127.0.0.1", "http://localhost"):
        return True
    return origin.startswith("http://127.0.0.1:") or origin.startswith("http://localhost:")


def face_dir(key):
    if not KEY_RE.fullmatch(key):
        raise ValueError("key must be the player's number")
    root = os.path.realpath(CFG["faces"])
    dest = os.path.realpath(os.path.join(root, key))
    if os.path.commonpath([root, dest]) != root:
        raise ValueError("key must be the player's number")
    return dest


class H(BaseHTTPRequestHandler):
    def _send(self, code, body, ctype="application/json"):
        self.send_response(code)
        self.send_header("Content-Type", ctype); self.send_header("Content-Length", str(len(body)))
        origin = self.headers.get("Origin")
        if origin and origin_allowed(origin):
            self.send_header("Access-Control-Allow-Origin", origin)
            self.send_header("Vary", "Origin")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, X-Faces-Token")
        self.end_headers(); self.wfile.write(body)

    def _authorized(self):
        given = self.headers.get("X-Faces-Token", "")
        if not hmac.compare_digest(given, CFG["token"]):
            self._send(401, b'{"error": "the faces token does not match"}')
            return False
        origin = self.headers.get("Origin")
        if origin and not origin_allowed(origin):
            self._send(403, b'{"error": "this page cannot use the faces server"}')
            return False
        return True

    def do_OPTIONS(self):
        origin = self.headers.get("Origin")
        if origin and not origin_allowed(origin):
            self._send(403, b'{"error": "this page cannot use the faces server"}')
            return
        self._send(204, b"")

    def do_GET(self):
        if self.path == "/health": self._send(200, b'{"ok": true}')
        else: self._send(404, b'{"error": "not found"}')

    def do_POST(self):
        if not self._authorized(): return
        try:
            data = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0) or b"{}")
            with LOCK:
                if self.path == "/logo":
                    prompt = str(data.get("prompt", "")).strip()[:300]
                    if not prompt: return self._send(400, b'{"error": "write what the crest should look like"}')
                    img = make_logo(prompt, (data.get("name") or "")[:40] or None, CFG["comfy"], cutout=CFG["cutout"])
                    buf = io.BytesIO(); img.save(buf, "PNG"); self._send(200, buf.getvalue(), "image/png")
                elif self.path == "/face":
                    look, age, key = data["look"], int(data["age"]), str(data["key"])
                    d = face_dir(key)
                    wf = comfy.load_workflow("qwen_text2img.json", {"prompt": prompts.portrait_prompt(look, age), "negative": prompts.NEGATIVE, "seed": look.get("seed", 1),
                                                                    "width": 768, "height": 1024, "prefix": "face_" + key})
                    img = imaging.to_image(comfy.Comfy(CFG["comfy"]).run(wf))
                    if CFG["cutout"]: img = imaging.cutout(img)
                    os.makedirs(d, exist_ok=True)
                    path = imaging.save(img, os.path.join(d, "%d.webp" % age), 512)
                    mp = os.path.join(CFG["faces"], "manifest.json")
                    man = json.load(open(mp)) if os.path.exists(mp) else {}
                    man[key] = sorted(set(man.get(key, []) + [age]))
                    with open(mp, "w") as f: json.dump(man, f, indent=1)
                    with open(path, "rb") as f: self._send(200, f.read(), "image/webp")
                else:
                    self._send(404, b'{"error": "not found"}')
        except Exception as e:  # (the page shows the message)
            self._send(500, json.dumps({"error": str(e)}).encode())

    def log_message(self, *a): pass


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--comfy", default="http://127.0.0.1:8188"); ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--faces", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "faces")); ap.add_argument("--cutout", action="store_true")
    ap.add_argument("--token", default=os.environ.get("FACES_TOKEN") or secrets.token_urlsafe(18))
    a = ap.parse_args(); CFG.update(comfy=a.comfy, faces=os.path.realpath(a.faces), cutout=a.cutout, token=a.token)
    print("faces and crests server on http://127.0.0.1:%d (ComfyUI: %s)" % (a.port, a.comfy))
    print("send header X-Faces-Token, or open the page with #faces-token=%s" % a.token)
    ThreadingHTTPServer(("127.0.0.1", a.port), H).serve_forever()
