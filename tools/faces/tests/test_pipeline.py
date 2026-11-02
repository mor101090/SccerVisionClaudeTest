"""Tests with a stand-in for ComfyUI (no GPU): the prompts, the workflow filling, the whole generation and the server."""
import io, json, os, sys, tempfile, threading, unittest, urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import prompts, comfy, imaging, generate_faces, server
from PIL import Image

QUEUED = []


class FakeComfy(BaseHTTPRequestHandler):
    def log_message(self, *a): pass

    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0)); body = self.rfile.read(n)
        if self.path == "/prompt":
            QUEUED.append(json.loads(body)["prompt"]); out = json.dumps({"prompt_id": "p%d" % len(QUEUED)}).encode()
        elif self.path == "/upload/image": out = json.dumps({"name": "uploaded.png"}).encode()
        else: self.send_response(404); self.end_headers(); return
        self.send_response(200); self.send_header("Content-Length", str(len(out))); self.end_headers(); self.wfile.write(out)

    def do_GET(self):
        if self.path.startswith("/history/"):
            out = json.dumps({self.path.split("/")[-1]: {"outputs": {"60": {"images": [{"filename": "x.png", "subfolder": "", "type": "output"}]}}}}).encode(); ct = "application/json"
        elif self.path.startswith("/view"):
            buf = io.BytesIO(); Image.new("RGB", (96, 128), (180, 150, 120)).save(buf, "PNG"); out = buf.getvalue(); ct = "image/png"
        else: self.send_response(404); self.end_headers(); return
        self.send_response(200); self.send_header("Content-Type", ct); self.send_header("Content-Length", str(len(out))); self.end_headers(); self.wfile.write(out)


LOOK = {"origin": "ethiopian", "look": "east-african", "skin": 5, "hair": "curly", "hairColour": "black", "facial": "short-beard", "build": "athletic", "seed": 4242}


class Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.srv = ThreadingHTTPServer(("127.0.0.1", 0), FakeComfy); cls.url = "http://127.0.0.1:%d" % cls.srv.server_address[1]
        threading.Thread(target=cls.srv.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls): cls.srv.shutdown()

    def test_prompt_follows_the_look_and_the_age(self):
        young, old = prompts.portrait_prompt(LOOK, 20), prompts.portrait_prompt(LOOK, 40)
        for word in ("East African", "dark brown skin", "curly hair", "short trimmed beard"): self.assertIn(word, young)
        self.assertIn("a few grey hairs", prompts.portrait_prompt(LOOK, 32)); self.assertIn("grey", old); self.assertIn("wrinkles", old); self.assertNotIn("wrinkles", young)
        self.assertIn("plain neckline", young)  # no shirt: the page draws it in the team's colour
        self.assertIn("bare", young)

    def test_workflow_placeholders_are_filled_with_the_right_types(self):
        wf = comfy.load_workflow("qwen_text2img.json", {"prompt": "p", "negative": "n", "seed": 7, "width": 640, "height": 832, "prefix": "x"})
        self.assertEqual(wf["3"]["inputs"]["seed"], 7); self.assertIsInstance(wf["3"]["inputs"]["seed"], int)
        self.assertEqual(wf["58"]["inputs"]["width"], 640); self.assertEqual(wf["6"]["inputs"]["text"], "p"); self.assertNotIn("_about", wf)
        self.assertNotIn("{{", json.dumps(wf))

    def test_generation_writes_a_portrait_per_age_and_a_manifest_and_resumes(self):
        with tempfile.TemporaryDirectory() as d:
            roster = os.path.join(d, "r.json")
            with open(roster, "w") as f: json.dump([{"key": "4242", "age": 24, "look": LOOK}], f)
            argv = ["x", "--roster", roster, "--out", os.path.join(d, "faces"), "--ages", "20,30", "--comfy", self.url, "--size", "64"]
            before = len(QUEUED); sys.argv = argv; generate_faces.main()
            self.assertEqual(len(QUEUED) - before, 2)
            with open(os.path.join(d, "faces", "manifest.json")) as f: man = json.load(f)
            self.assertEqual(man["4242"], [20, 30])
            self.assertTrue(Image.open(os.path.join(d, "faces", "4242", "20.webp")).size[0] <= 64)
            generate_faces.main(); self.assertEqual(len(QUEUED) - before, 2)  # nothing made twice
            self.assertIn("about 20", QUEUED[before]["6"]["inputs"]["text"])
            self.assertIn("about 30", QUEUED[before + 1]["6"]["inputs"]["text"]); self.assertEqual(QUEUED[before]["3"]["inputs"]["seed"], 4242)

    def test_server_makes_a_logo_from_the_players_words(self):
        with tempfile.TemporaryDirectory() as d:
            server.CFG.update(comfy=self.url, faces=d, cutout=False)
            srv = ThreadingHTTPServer(("127.0.0.1", 0), server.H); threading.Thread(target=srv.serve_forever, daemon=True).start()
            url = "http://127.0.0.1:%d" % srv.server_address[1]
            req = urllib.request.Request(url + "/logo", data=json.dumps({"prompt": "a red falcon on a blue shield", "name": "Blue Falcons"}).encode(), headers={"Content-Type": "application/json"})
            before = len(QUEUED)
            with urllib.request.urlopen(req) as r: self.assertEqual(r.headers["Content-Type"], "image/png"); Image.open(io.BytesIO(r.read()))
            text = QUEUED[before]["6"]["inputs"]["text"]; self.assertIn("red falcon", text); self.assertIn("Blue Falcons", text)
            req = urllib.request.Request(url + "/logo", data=b'{"prompt": ""}', headers={"Content-Type": "application/json"})
            with self.assertRaises(urllib.error.HTTPError) as e: urllib.request.urlopen(req)
            self.assertEqual(e.exception.code, 400)
            req = urllib.request.Request(url + "/face", data=json.dumps({"key": "9", "age": 27, "look": LOOK}).encode(), headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req) as r: self.assertEqual(r.headers["Content-Type"], "image/webp")
            with open(os.path.join(d, "manifest.json")) as f: self.assertEqual(json.load(f)["9"], [27])
            srv.shutdown(); srv.server_close()


if __name__ == "__main__":
    unittest.main()
