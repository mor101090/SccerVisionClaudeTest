"""One ComfyUI graph (one queue, one prompt) that makes a player's whole life of portraits: a base portrait from the text, then a
Qwen-Image-Edit branch per age (19, 20 ... 35). Every branch edits the BASE picture, never the one before it, and all use the same
seed: the face stays the same man and does not drift from age to age; only what age changes (skin, lines, greying, the hairline).

    python3 age_chain.py --look look.json --out workflows/my_chain.json      # write the graph to load in ComfyUI (Load / drag the file)
    generate_faces.py --method chain ...                                       # or let the generator queue it per player

The node names are those of workflows/qwen_text2img.json and qwen_edit.json (check them once against your ComfyUI, see the README)."""
import argparse, json, os
import prompts

HERE = os.path.dirname(os.path.abspath(__file__))
AGES = list(range(19, 36))


def _load(name):
    with open(os.path.join(HERE, "workflows", name), encoding="utf8") as f:
        return {k: v for k, v in json.load(f).items() if not k.startswith("_")}


def build(look, ages=AGES, base_age=26, seed=None, width=1024, height=1024, prefix="face"):
    """Returns the graph (ComfyUI API format). The base is saved as <prefix>_<base_age>, every other age as <prefix>_<age>."""
    seed = int(look.get("seed", 1) if seed is None else seed)
    t2i, edit = _load("qwen_text2img.json"), _load("qwen_edit.json")
    g = {}
    # the base: text to image (nodes t*)
    ids = {k: "t" + k for k in t2i}
    def remap(node, table):
        node = json.loads(json.dumps(node))
        for k, v in node["inputs"].items():
            if isinstance(v, list) and len(v) == 2 and isinstance(v[0], str) and v[0] in table: node["inputs"][k] = [table[v[0]], v[1]]
        return node
    for k, n in t2i.items(): g[ids[k]] = remap(n, ids)
    g["t6"]["inputs"]["text"] = prompts.portrait_prompt(look, base_age)
    g["t7"]["inputs"]["text"] = prompts.NEGATIVE
    g["t3"]["inputs"]["seed"] = seed
    g["t58"]["inputs"].update({"width": width, "height": height})
    g["t60"]["inputs"]["filename_prefix"] = "%s_%d" % (prefix, base_age)
    base_image = ["t8", 0]
    # the shared part of the edits: the edit model, its sampling, the base picture scaled and encoded once
    shared = {"37": "e37", "38": "t38", "39": "t39", "66": "e66", "93": "e93", "88": "e88", "77": "e77"}
    g["e37"] = edit["37"]; g["e66"] = remap(edit["66"], shared)
    g["e93"] = remap(edit["93"], shared); g["e93"]["inputs"]["image"] = base_image
    g["e88"] = remap(edit["88"], shared); g["e77"] = remap(edit["77"], shared)
    for age in ages:
        if age == base_age: continue
        a = "a%d_" % age
        table = dict(shared, **{"76": a + "76", "3": a + "3", "8": a + "8"})
        g[a + "76"] = remap(edit["76"], table); g[a + "76"]["inputs"]["prompt"] = prompts.age_edit_prompt(look, age)
        g[a + "3"] = remap(edit["3"], table); g[a + "3"]["inputs"]["seed"] = seed
        g[a + "8"] = remap(edit["8"], table)
        g[a + "60"] = {"class_type": "SaveImage", "inputs": {"filename_prefix": "%s_%d" % (prefix, age), "images": [a + "8", 0]}}
    return g


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--look", help="a JSON file with one player's look (an entry of --export-roster, or its 'look')")
    ap.add_argument("--out", required=True); ap.add_argument("--base-age", type=int, default=26)
    ap.add_argument("--from-age", type=int, default=19); ap.add_argument("--to-age", type=int, default=35)
    a = ap.parse_args()
    look = {"look": "east-african", "skin": 5, "hair": "curly", "hairColour": "black", "facial": "short-beard", "build": "athletic", "seed": 7}
    if a.look:
        with open(a.look) as f: d = json.load(f)
        look = d.get("look", d) if isinstance(d.get("look"), dict) else d
    g = build(look, list(range(a.from_age, a.to_age + 1)), a.base_age)
    with open(a.out, "w") as f: json.dump(g, f, indent=1)
    print("wrote", a.out, "-", sum(1 for n in g.values() if n["class_type"] == "SaveImage"), "portraits in one queue")


if __name__ == "__main__":
    main()
