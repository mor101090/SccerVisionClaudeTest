#!/usr/bin/env python3
"""Makes the portraits of a roster with Qwen-Image in ComfyUI, one per age, and a manifest the page reads.

  python3 generate_faces.py --roster roster.json --out ../../docs/faces --ages 18,22,26,30,34,38
  roster.json: [{"key": "123456", "age": 24, "look": {...the "look" object of the replay header...}}, ...]
  (SimRunner --export-roster 20 > roster.json  makes one from the demo's squads)

Resumable: a portrait that exists is not made again. --method regenerate: the same seed and look with the age in the prompt (works with the text-to-image
workflow alone; the face resembles, it is not identical). --method edit: the first portrait is aged with Qwen-Image-Edit (the same man, older).
"""
import argparse, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prompts, comfy, imaging, age_chain


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--roster", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--ages", default=",".join(str(x) for x in range(19, 36))); ap.add_argument("--comfy", default="http://127.0.0.1:8188")
    ap.add_argument("--method", choices=["chain", "regenerate", "edit"], default="chain",
                    help="chain: one ComfyUI queue per player - a base portrait and an edit of it per age (age_chain.py)")
    ap.add_argument("--width", type=int, default=1024); ap.add_argument("--height", type=int, default=1024)
    ap.add_argument("--size", type=int, default=512, help="longest side of the saved picture"); ap.add_argument("--cutout", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    a = ap.parse_args()
    ages = [int(x) for x in a.ages.split(",")]
    with open(a.roster, encoding="utf8") as f: roster = json.load(f)
    if a.limit: roster = roster[:a.limit]
    os.makedirs(a.out, exist_ok=True)
    mpath = os.path.join(a.out, "manifest.json")
    manifest = {}
    if os.path.exists(mpath):
        with open(mpath) as f: manifest = json.load(f)
    c = comfy.Comfy(a.comfy)
    for n, p in enumerate(roster, 1):
        key, look = str(p["key"]), p["look"]
        d = os.path.join(a.out, key); os.makedirs(d, exist_ok=True)
        base_png = None
        def keep(png, path, age):
            img = imaging.to_image(png)
            img, shirt = imaging.split_shirt(img)
            if a.cutout: img = imaging.cutout(img)
            imaging.save(img, path, a.size)
            if shirt is not None: imaging.save(shirt, path[:-5] + ".shirt.png", a.size, fmt="PNG")
            print("[%d/%d] %s age %d -> %s" % (n, len(roster), key, age, path), flush=True)
        missing = [age for age in ages if not os.path.exists(os.path.join(d, "%d.webp" % age))]
        if a.method == "chain" and missing:
            base_age = 26 if 26 in ages else ages[len(ages) // 2]
            pics = c.run_all(age_chain.build(look, ages, base_age, width=a.width, height=a.height, prefix="face_" + key))
            for age in missing:
                png = pics.get("face_%s_%d" % (key, age))
                if png: keep(png, os.path.join(d, "%d.webp" % age), age)
        for age in ([] if a.method == "chain" else ages):
            path = os.path.join(d, "%d.webp" % age)
            if os.path.exists(path):
                continue
            if a.method == "edit" and base_png is not None:
                name = c.upload(base_png, "base_%s.png" % key)
                wf = comfy.load_workflow("qwen_edit.json", {"prompt": prompts.age_edit_prompt(look, age), "image": name, "seed": look.get("seed", 1), "prefix": "face_" + key})
            else:
                wf = comfy.load_workflow("qwen_text2img.json", {"prompt": prompts.portrait_prompt(look, age), "negative": prompts.NEGATIVE, "seed": look.get("seed", 1),
                                                                "width": a.width, "height": a.height, "prefix": "face_" + key})
            png = c.run(wf)
            if base_png is None: base_png = png
            keep(png, path, age)
        manifest[key] = sorted(int(f[:-5]) for f in os.listdir(d) if f.endswith(".webp"))  # (an age with <age>.shirt.png has its shirt coloured by the page)
        with open(mpath, "w") as f: json.dump(manifest, f, indent=1)
    print("done:", len(manifest), "players in", mpath)


if __name__ == "__main__":
    main()
