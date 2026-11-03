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
import argparse, csv, json, os, re, ssl, sys, unicodedata, urllib.request
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
    ("NG", "Nigeria"), ("GH", "Ghana"), ("EG", "Egypt"), ("RU", "Russia"),
    ("PE", "Peru"), ("BO", "Bolivia"), ("CR", "Costa Rica"), ("PA", "Panama"), ("FI", "Finland"), ("GR", "Greece"), ("CM", "Cameroon"),
    ("ZA", "South Africa"), ("CA", "Canada"), ("HU", "Hungary"), ("BG", "Bulgaria"), ("EC", "Ecuador"), ("RS", "Serbia"),
]
# Tried and left out because the release's surnames for them are mostly not surnames: Morocco and Algeria (Ell, Female, Ssa, Setif, Madrid,
# Annaba...) and Japan (a handful of real surnames among Leroux, Zhao, Hassan...). They can be added back above when a later release is better.
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
MIN_FIRST, MIN_LAST = 30, 12        # a country with fewer is left out (too few to make varied names)
MIN_KEEP = 80                      # ...but never fewer than this many
MIN_P_MALE = 0.35
SURNAME_COVER = 0.985               # surnames: the names that make up 93% of a country's weight (at least MIN_LAST of them)
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


# Cultural clusters (countries that share naming habits) and the clusters each target country belongs to. A name is dropped from a
# country if some OTHER cluster has it much more strongly (share in the country's records) than the country's own clusters - that is
# how a Mohamed in Spain, a Singh in England or a Gashi in Switzerland (immigrant names) are told from the local ones. Only the countries
# listed here are compared, so the tiny samples of other countries (whose shares are noise) cannot decide.
CLUSTERS = {
    "spanish": "ES MX CO AR UY CL PE BO EC CR PA VE PY GT HN SV NI DO CU", "portuguese": "PT BR AO MZ", "italian": "IT", "french": "FR LU",
    "german": "DE AT LI CH", "dutch": "NL BE", "english": "GB IE US CA AU NZ", "nordic": "DK SE NO FI IS", "west-slavic": "PL CZ SK",
    "south-slavic": "HR RS SI BA BG MK ME", "east-slavic": "RU UA BY", "turkish": "TR AZ", "arabic": "EG DZ MA TN SA IQ SY JO LB LY SD KW AE QA YE OM BH PS",
    "african": "NG GH CM SN CI ML GN KE TZ UG ET", "malay": "MY ID", "east-asian": "CN JP KR TW VN", "iranian": "IR AF", "albanian": "AL XK",
    "greek": "GR CY", "hungarian": "HU", "romanian": "RO MD", "south-asian": "IN PK BD LK NP",
}
HOME = {  # target country -> the clusters that are its own
    "BE": ["french", "dutch", "german"], "CH": ["german", "french", "italian"], "AT": ["german"], "NG": ["african", "english", "arabic"],
    "GH": ["african", "english"], "TR": ["turkish", "arabic"], "EG": ["arabic"], "IE": ["english"], "GB": ["english"], "US": ["english", "spanish"],
    "ZA": ["african", "english"],
}
# European clusters share many names (Antonio, Jan, Kevin, David, Ivan...), so between them a name is dropped only if it is extreme
# (25 times as strong in the other cluster; the Portuguese Silva and Ferreira stay in Switzerland - they really are among its commonest surnames); a name that belongs to a non-European cluster
# (Mohamed, Singh, Yilmaz, Nguyen...) is dropped from a European country as soon as that cluster has it 3 times as strongly.
EUROPEAN = {"spanish", "portuguese", "italian", "french", "german", "dutch", "english", "nordic", "west-slavic", "south-slavic", "east-slavic", "greek", "hungarian", "romanian"}
FOREIGN_RATIO, FOREIGN_RATIO_EUROPE = 3.0, 25.0


def cluster_of():
    return {c: k for k, members in CLUSTERS.items() for c in members.split()}


def foreign(strength, cc, cl):
    """strength: {country: share} of one name. True if the name is far more at home in a cluster that is not the country's own."""
    home_clusters = HOME.get(cc) or [cl[cc]]
    by = defaultdict(float)
    for c, v in strength.items():
        if c in cl: by[cl[c]] = max(by[cl[c]], v)
    mine = max([by.get(h, 0.0) for h in home_clusters] + [strength.get(cc, 0.0)])
    for k, v in by.items():
        if k in home_clusters: continue
        if v >= (FOREIGN_RATIO_EUROPE if k in EUROPEAN else FOREIGN_RATIO) * mine: return True
    return False


PARTICLES = {"van", "von", "der", "den", "de", "het", "ter", "ten", "te", "zu", "da", "do", "dos", "das", "del", "la", "los", "las"}
PARTICLE_COUNTRIES = {"NL", "BE", "DE", "AT", "CH", "PT", "BR", "ES", "AR", "MX", "CO", "UY", "CL"}


def particles(name):
    """"Van Der Linden" -> "van der Linden", "Da Silva" -> "da Silva": the small words of a surname are lower case (not the name's last word)."""
    words = name.split(" ")
    return " ".join(w.lower() if i < len(words) - 1 and w.lower() in PARTICLES else w for i, w in enumerate(words))


def masculine(cc, name, known):
    """The surnames of Poland, Czechia and Russia come in a woman's form (Kowalska, Nováková, Ivanova): a man's name is the man's form.
    Returns the man's form, or None if the name is a woman's form with no man's form among the known names."""
    low = name
    if cc == "PL":
        for f, m in (("ska", "ski"), ("cka", "cki"), ("zka", "zki")):
            if low.endswith(f): return low[:-len(f)] + m
        return name
    if cc == "RU":
        for f in ("ova", "eva", "ina", "yna", "skaya", "ova"):
            if low.endswith(f) and len(low) > len(f) + 2: return low[:-1]
        return name
    if cc == "CZ":
        if low.endswith("ová") or (low.endswith("á") and len(low) > 4):
            stem = low[:-3] if low.endswith("ová") else low[:-1]
            for cand in (stem, stem + "a", stem + "ý"):
                if cand in known: return cand
            return None
    return name


ACCENT_LANGUAGE = {"ES": "es", "AR": "es", "MX": "es", "CO": "es", "UY": "es", "CL": "es", "PT": "pt", "BR": "pt", "FR": "fr", "IT": "it"}


def load_accents():
    """accents.json, made by restore_accents.py (checked against Wikipedia): {language: {plain word: accented word}}."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "accents.json")
    return json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}


def accentuate(cc, name, accents):
    table = accents.get(ACCENT_LANGUAGE.get(cc, ""), {})
    return "".join(table.get(part, part) for part in re.split(r"([ \-'’.])", name)) if table else name


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
    cl = cluster_of()
    strength_f, strength_l = defaultdict(dict), defaultdict(dict)       # name -> {country: share}, over the clustered countries
    dropped = defaultdict(lambda: [0, 0])
    for r in csv.DictReader(open(paths["given-name-frequency.csv"], encoding="utf-8")):
        if r["country_code"] in cl: strength_f[r["name"]][r["country_code"]] = strength_f[r["name"]].get(r["country_code"], 0.0) + float(r["pct_in_country"])
    for r in csv.DictReader(open(paths["surname-frequency.csv"], encoding="utf-8")):
        if r["country_code"] in cl: strength_l[r["name"]][r["country_code"]] = strength_l[r["name"]].get(r["country_code"], 0.0) + float(r["pct_in_country"])
    for r in csv.DictReader(open(paths["given-name-frequency.csv"], encoding="utf-8")):
        cc, n = r["country_code"], r["name"]
        if cc not in codes or not usable(n) or len(n) < 2: continue
        if cc in cl and foreign(strength_f[n], cc, cl): dropped[cc][0] += 1; continue
        g = r["gender"].split(";")
        if "M" not in g: continue
        share = 1.0 if g == ["M"] else p_male.get(n, 0.0)
        if share < (1.0 if g == ["M"] else MIN_P_MALE): continue
        first[cc][n] += float(r["pct_in_country"]) * share
    raw_last = defaultdict(dict)
    for r in csv.DictReader(open(paths["surname-frequency.csv"], encoding="utf-8")):
        cc, n = r["country_code"], r["name"]
        if cc in codes and usable(n) and len(n) >= 3: raw_last[cc][n] = raw_last[cc].get(n, 0.0) + float(r["pct_in_country"])  # (2-letter "surnames" like Ma, Le are fragments)
    for cc, d in raw_last.items():
        known = set(d)
        for n, w in d.items():
            if p_male.get(n, 1.0) < 0.2: continue                 # a woman's given name in the surname list (Eduarda, Fernanda, Sarah)
            if cc in cl and foreign(strength_l[n], cc, cl): dropped[cc][1] += 1; continue
            m = masculine(cc, n, known)
            if m is None: continue
            if cc in PARTICLE_COUNTRIES: m = particles(m)
            last[cc][m] += w

    accents = load_accents()
    for cc in list(first):
        merged = defaultdict(float)
        for n, w in first[cc].items(): merged[accentuate(cc, n, accents)] += w
        first[cc] = merged
    for cc in list(last):
        merged = defaultdict(float)
        for n, w in last[cc].items(): merged[accentuate(cc, n, accents)] += w
        last[cc] = merged

    def table(d, top, cover=1.0):
        items = sorted(d.items(), key=lambda kv: (-kv[1], kv[0]))
        if cover < 1.0:                                             # the long tail of rare "names" is mostly noise: keep the names that make up `cover` of the weight
            total, run, keep = sum(w for _, w in items), 0.0, 0
            for _, w in items:
                run += w; keep += 1
                if run >= cover * total: break
            items = items[:max(keep, MIN_KEEP)]
        items = items[:top]
        return ";".join("%s|%d" % (n, max(1, round(w * 1_000_000))) for n, w in items), len(items)

    lines = []
    kept, skipped = [], []
    print("dropped as foreign (first names, surnames):", ", ".join("%s %d/%d" % (c, v[0], v[1]) for c, v in sorted(dropped.items())), file=sys.stderr)
    for cc, nation in COUNTRIES:
        f, nf = table(first[cc], TOP_FIRST)
        note = ""
        if cc in SUPPLEMENT_LAST:
            sup = SUPPLEMENT_LAST[cc]
            l, nl = ";".join("%s|%d" % (n, round(1_000_000 / (i + 1))) for i, n in enumerate(sup)), len(sup)
            note = "Surnames: a supplementary list of the most common surnames (the Onomaverse release has too few usable ones for this country); first names: Onomaverse."
        else:
            l, nl = table(last[cc], TOP_LAST, SURNAME_COVER)
            if nf < 80 or nl < 30: note = "Thin: the dataset has few names for this country (%d male first names, %d surnames) - expect the same names to come up often." % (nf, nl)
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
