#!/usr/bin/env python3
"""A club crest from a text prompt (Qwen-Image draws text well, so the club's name can be on it).

  python3 generate_logo.py "a red falcon over a blue shield, bold" --name "Blue Falcons" --out logo.png
"""
import argparse, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prompts, comfy, imaging


def make_logo(prompt, name=None, comfy_url="http://127.0.0.1:8188", seed=None, size=1024, cutout=False):
    c = comfy.Comfy(comfy_url)
    wf = comfy.load_workflow("qwen_text2img.json", {"prompt": prompts.logo_prompt(prompt, name), "negative": "photo, realistic, blurry, low quality, watermark, extra text, misspelled text",
                                                    "seed": seed if seed is not None else random.randint(1, 2 ** 31), "width": size, "height": size, "prefix": "logo"})
    img = imaging.to_image(c.run(wf))
    return imaging.cutout(img) if cutout else img


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("prompt"); ap.add_argument("--name"); ap.add_argument("--out", default="logo.png"); ap.add_argument("--comfy", default="http://127.0.0.1:8188")
    ap.add_argument("--seed", type=int); ap.add_argument("--size", type=int, default=1024); ap.add_argument("--cutout", action="store_true")
    a = ap.parse_args()
    imaging.save(make_logo(a.prompt, a.name, a.comfy, a.seed, a.size, a.cutout), a.out, 1024, "PNG")
    print("saved", a.out)
