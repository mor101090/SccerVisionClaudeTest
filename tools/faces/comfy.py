"""A small client for ComfyUI's HTTP API (https://docs.comfy.org): load a workflow saved in API format, fill in the
{{placeholders}}, queue it, wait, and download the picture. Nothing but the standard library."""
import json, time, urllib.request, urllib.parse, uuid, os

HERE = os.path.dirname(os.path.abspath(__file__))


def load_workflow(name, values):
    """name: a file in workflows/. Every "{{key}}" in a string is replaced; a string that is only "{{key}}" takes the value's own type."""
    with open(os.path.join(HERE, "workflows", name), encoding="utf8") as f:
        wf = json.load(f)

    def fill(x):
        if isinstance(x, dict): return {k: fill(v) for k, v in x.items()}
        if isinstance(x, list): return [fill(v) for v in x]
        if isinstance(x, str) and "{{" in x:
            for k, v in values.items():
                if x.strip() == "{{" + k + "}}": return v
            for k, v in values.items(): x = x.replace("{{" + k + "}}", str(v))
        return x
    return fill({k: v for k, v in wf.items() if not k.startswith("_")})


class Comfy:
    def __init__(self, url="http://127.0.0.1:8188", timeout=600):
        self.url, self.timeout, self.client = url.rstrip("/"), timeout, uuid.uuid4().hex

    def _get(self, path):
        with urllib.request.urlopen(self.url + path, timeout=60) as r: return r.read()

    def upload(self, png_bytes, name):
        """Puts a picture into ComfyUI's input folder (for the editing workflow)."""
        boundary = uuid.uuid4().hex
        body = (("--%s\r\nContent-Disposition: form-data; name=\"image\"; filename=\"%s\"\r\nContent-Type: image/png\r\n\r\n" % (boundary, name)).encode()
                + png_bytes + ("\r\n--%s\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--%s--\r\n" % (boundary, boundary)).encode())
        req = urllib.request.Request(self.url + "/upload/image", data=body, headers={"Content-Type": "multipart/form-data; boundary=" + boundary})
        with urllib.request.urlopen(req, timeout=60) as r: return json.loads(r.read())["name"]

    def run(self, workflow):
        """Queues the workflow and returns the first picture it saves, as PNG bytes."""
        req = urllib.request.Request(self.url + "/prompt", data=json.dumps({"prompt": workflow, "client_id": self.client}).encode(),
                                     headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r: pid = json.loads(r.read())["prompt_id"]
        t0 = time.time()
        while time.time() - t0 < self.timeout:
            hist = json.loads(self._get("/history/" + pid))
            if pid in hist and hist[pid].get("outputs"):
                for out in hist[pid]["outputs"].values():
                    for im in out.get("images", []):
                        q = urllib.parse.urlencode({"filename": im["filename"], "subfolder": im.get("subfolder", ""), "type": im.get("type", "output")})
                        return self._get("/view?" + q)
            time.sleep(1.0)
        raise TimeoutError("ComfyUI did not finish in %d s" % self.timeout)
