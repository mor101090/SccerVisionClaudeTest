#!/usr/bin/env python3
"""Builds the player-name tables from the Onomaverse names datasets.

Data: Names data from Onomaverse (https://onomaverse.com/datasets), licensed CC BY 4.0
      (https://creativecommons.org/licenses/by/4.0/). Release v2026.10, https://github.com/onomaverse/datasets
Cite: The Onomaverse Team. Onomaverse Names Datasets (v2026.10). https://onomaverse.com/datasets. Licensed CC BY 4.0.

It reads three files of the release - given-name-frequency.csv, surname-frequency.csv and name-gender-inference.csv - and writes
unity/SoccerVision/Assets/SoccerVision/Simulation/Names/OnomaverseNameData.cs: for every country in COUNTRIES the most common MALE
first names and the most common surnames with their frequency weights. The two lists are kept apart; nothing else of the dataset
is stored. Names are copied exactly as the dataset has them (accents and special characters are kept); a name is dropped only if it
is not written in the Latin script, or holds a character the table format uses ("|" or ";").

To add a country: add a line to COUNTRIES (ISO 3166-1 alpha-2 code, the nationality as the game writes it) and run

    python3 tools/names/build_onomaverse_names.py            # downloads the release files
    python3 tools/names/build_onomaverse_names.py --dir /path/with/the/csv/files

Male first names: a name the dataset records as "M" counts in full; a name recorded as both ("M;F" / "F;M", which holds both Andrea
in Italy and Ana in Portugal) counts with the share of its bearers that are male (name-gender-inference.csv, p_male), and not at
all below 0.35. So Andrea is a possible name for a man, Ana almost never, Maria never.
"""
import argparse, csv, os, ssl, sys, unicodedata, urllib.request
from collections import defaultdict

RELEASE = "v2026.10"
BASE = "https://github.com/onomaverse/datasets/releases/download/%s/" % RELEASE
FILES = ("given-name-frequency.csv", "surname-frequency.csv", "name-gender-inference.csv")

# (country code, nationality as the game writes it). The first ten are the ones the game is built around.
COUNTRIES = [
    ("ES", "Spain"), ("DE", "Germany"), ("FR", "France"), ("IT", "Italy"), ("GB", "England"),
    ("BR", "Brazil"), ("AR", "Argentina"), ("PT", "Portugal"), ("NL", "Netherlands"), ("HR", "Croatia"),
    ("BE", "Belgium"), ("CH", "Switzerland"), ("AT", "Austria"), ("DK", "Denmark"), ("SE", "Sweden"), ("NO", "Norway"),
    ("PL", "Poland"), ("CZ", "Czechia"), ("IE", "Ireland"), ("US", "USA"), ("TR", "Turkey"),
    ("MX", "Mexico"), ("CO", "Colombia"), ("UY", "Uruguay"), ("CL", "Chile"),
    ("NG", "Nigeria"), ("GH", "Ghana"), ("MA", "Morocco"), ("DZ", "Algeria"), ("EG", "Egypt"), ("JP", "Japan"), ("RU", "Russia"),
]
# Where the dataset cannot give a country's names, a supplement stands in - clearly NOT Onomaverse data, and said so in the generated
# file and in the notices. The Onomaverse release has only 29 "Croatian" surnames and nearly all of them are not Croatian (Zheng, Vasile,
# Zara, Santo...), so Croatian surnames come from this list of the most common Croatian surnames, in rank order (weights 1/rank).
SUPPLEMENT_LAST = {
    "HR": ["Horvat", "Kovačević", "Babić", "Marić", "Jurić", "Novak", "Kovačić", "Knežević", "Vuković", "Marković", "Petrović", "Matić",
           "Tomić", "Pavlović", "Božić", "Blažević", "Perić", "Grgić", "Lovrić", "Pavić", "Radić", "Filipović", "Šimić", "Bošnjak",
           "Kovač", "Vidović", "Lukić", "Jukić", "Barišić", "Mikulić", "Županić", "Ivanović", "Burić", "Mandić", "Vlahović", "Perišić",
           "Katić", "Sertić", "Modrić", "Rakitić"],
}
TOP_FIRST, TOP_LAST = 300, 500      # names kept per country
MIN_FIRST, MIN_LAST = 30, 25        # a country with fewer is left out (too few to make varied names)
MIN_P_MALE = 0.35
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "unity", "SoccerVision", "Assets", "SoccerVision", "Simulation", "Names", "OnomaverseNameData.cs")


def context():
    ca = os.environ.get("SSL_CERT_FILE") or os.environ.get("REQUESTS_CA_BUNDLE")
    if not ca and os.path.exists("/root/.ccr/ca-bundle.crt"): ca = "/root/.ccr/ca-bundle.crt"
    return ssl.create_default_context(cafile=ca) if ca else ssl.create_default_context()


def fetch(directory, name):
    path = os.path.join(directory, name)
    if not os.path.exists(path):
        os.makedirs(directory, exist_ok=True)
        print("downloading", name, file=sys.stderr)
        with urllib.request.urlopen(BASE + name, timeout=180, context=context()) as r, open(path, "wb") as f: f.write(r.read())
    return path


def latin(name):
    """True if every letter of the name is a Latin letter (accents and special characters like ø, ß, ł are Latin letters)."""
    has_letter = False
    for ch in name:
        if ch.isalpha():
            has_letter = True
            if not unicodedata.name(ch, "").startswith("LATIN"): return False
        elif ch not in " '-.’": return False
    return has_letter


def usable(name): return latin(name) and "|" not in name and ";" not in name and name == name.strip()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", default=os.path.join(os.environ.get("TMPDIR", "/tmp"), "onomaverse-" + RELEASE), help="where the CSV files are (downloaded there if missing)")
    ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    paths = {f: fetch(a.dir, f) for f in FILES}
    codes = {c for c, _ in COUNTRIES}

    p_male = {r["name"]: float(r["p_male"]) for r in csv.DictReader(open(paths["name-gender-inference.csv"], encoding="utf-8"))}
    first, last = defaultdict(lambda: defaultdict(float)), defaultdict(lambda: defaultdict(float))
    for r in csv.DictReader(open(paths["given-name-frequency.csv"], encoding="utf-8")):
        cc, n = r["country_code"], r["name"]
        if cc not in codes or not usable(n) or len(n) < 2: continue
        g = r["gender"].split(";")
        if "M" not in g: continue
        share = 1.0 if g == ["M"] else p_male.get(n, 0.0)
        if share < (1.0 if g == ["M"] else MIN_P_MALE): continue
        first[cc][n] += float(r["pct_in_country"]) * share
    for r in csv.DictReader(open(paths["surname-frequency.csv"], encoding="utf-8")):
        cc, n = r["country_code"], r["name"]
        if cc in codes and usable(n) and len(n) >= 3: last[cc][n] += float(r["pct_in_country"])  # (2-letter "surnames" like Ma, Le are fragments)

    def table(d, top):
        items = sorted(d.items(), key=lambda kv: (-kv[1], kv[0]))[:top]
        return ";".join("%s|%d" % (n, max(1, round(w * 1_000_000))) for n, w in items), len(items)

    lines = []
    kept, skipped = [], []
    for cc, nation in COUNTRIES:
        f, nf = table(first[cc], TOP_FIRST)
        note = ""
        if cc in SUPPLEMENT_LAST:
            sup = SUPPLEMENT_LAST[cc]
            l, nl = ";".join("%s|%d" % (n, round(1_000_000 / (i + 1))) for i, n in enumerate(sup)), len(sup)
            note = "Surnames: a supplementary list of the most common surnames (the Onomaverse release has too few usable ones for this country); first names: Onomaverse."
        else:
            l, nl = table(last[cc], TOP_LAST)
        if nf < MIN_FIRST or nl < MIN_LAST:
            skipped.append("%s (%d male first names, %d surnames)" % (cc, nf, nl)); continue
        kept.append((cc, nation, nf, nl))
        lines.append('            new OnomaverseCountry("%s", "%s",\n                "%s",\n                "%s",\n                "%s"),' % (cc, nation, f.replace('"', '\\"'), l.replace('"', '\\"'), note))
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8", newline="\n") as o:
        o.write("// <auto-generated> by tools/names/build_onomaverse_names.py from the Onomaverse names datasets (release %s). Do not edit by hand. </auto-generated>\n" % RELEASE)
        o.write("// Names data from Onomaverse (https://onomaverse.com/datasets), licensed CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/).\n")
        o.write("// Each country: its most common male first names and its most common surnames as \"name|weight;name|weight\", weight = share of the\n")
        o.write("// country's records x 1,000,000 (for a name recorded for both sexes: x the share of its bearers that are male).\n")
        o.write("namespace SoccerVision.Simulation\n{\n    public static class OnomaverseNameData\n    {\n")
        o.write("        public const string Release = \"%s\";\n\n" % RELEASE)
        o.write("        public static readonly OnomaverseCountry[] Countries =\n        {\n")
        o.write("\n".join(lines))
        o.write("\n        };\n    }\n}\n")
    for cc, nation, nf, nl in kept: print("%s %-12s %3d first  %3d last" % (cc, nation, nf, nl))
    if skipped: print("left out (too few names):", ", ".join(skipped))
    print("wrote", os.path.normpath(a.out), "-", os.path.getsize(a.out) // 1024, "KB")


if __name__ == "__main__":
    main()
