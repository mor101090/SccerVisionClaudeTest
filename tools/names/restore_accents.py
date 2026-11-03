#!/usr/bin/env python3
"""Puts back the accents the Onomaverse tables lack (Jose -> José, Garcia -> García, Goncalves -> Gonçalves), checked against Wikipedia.

The dataset writes many Spanish, Portuguese and French names without their accents. A name is only changed if Wikipedia says so:

  A. Wikipedia redirects the plain spelling to the accented one (Garcia -> García, Nunez -> Núñez, Sebastien -> Sébastien), or
  B. an accented spelling (the word with ONE diacritic added) has a Wikipedia article in a name category of the language -
     "Spanish masculine given names", "Spanish-language surnames", "Portuguese ...", "French ...", "Italian ..." - and the plain
     spelling is not itself in a name category of that language (Mario stays Mario in Spain, becomes Mário in Portugal).

Reads the words of the generated name table (run build_onomaverse_names.py first), writes accents.json (which the builder then applies,
word by word, to the countries of the language), and prints every change so it can be read through. Responses are cached in
.accent_cache.json so a re-run is quick. The facts used are spellings; Wikipedia's text is not copied (Wikipedia: CC BY-SA 4.0).

    python3 tools/names/build_onomaverse_names.py && python3 tools/names/restore_accents.py && python3 tools/names/build_onomaverse_names.py
"""
import json, os, re, ssl, sys, time, unicodedata, urllib.error, urllib.parse, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "..", "unity", "SoccerVision", "Assets", "SoccerVision", "Simulation", "Names", "OnomaverseNameData.cs")
OUT = os.path.join(HERE, "accents.json")
CACHE = os.path.join(HERE, ".accent_cache.json")
UA = "SoccerVisionNames/1.0 (football game name tables; https://github.com/mor101090/SccerVisionClaudeTest)"

GROUPS = {"es": ["ES", "AR", "MX", "CO", "UY", "CL"], "pt": ["PT", "BR"], "fr": ["FR"], "it": ["IT"]}
ADD = {  # the single diacritics that can be put on a letter, by language
    "es": {"a": "á", "e": "é", "i": "í", "o": "ó", "u": "ú", "n": "ñ"},
    "pt": {"a": "áâã", "e": "éê", "i": "í", "o": "óôõ", "u": "ú", "c": "ç"},
    "fr": {"a": "àâ", "e": "éèêë", "i": "îï", "o": "ô", "u": "ùû", "c": "ç"},
    "it": {"a": "à", "e": "èé", "i": "ì", "o": "ò", "u": "ù"},
}
CATEGORY = {  # a name category of the language
    "es": re.compile(r"^Category:(Spanish|Latin American|Mexican|Argentine|Chilean|Colombian)[- ](language )?(masculine )?(given names|surnames)|^Category:Spanish[- ]language surnames"),
    "pt": re.compile(r"^Category:(Portuguese|Brazilian)[- ](language )?(masculine |patronymic )?(given names|surnames)"),
    "fr": re.compile(r"^Category:French[- ](language )?(masculine )?(given names|surnames)"),
    "it": re.compile(r"^Category:Italian[- ](language )?(masculine )?(given names|surnames)"),
}
NAME_CATEGORIES = "|".join("Category:" + c for c in (
    "Spanish masculine given names", "Spanish given names", "Spanish-language surnames", "Portuguese masculine given names", "Portuguese given names",
    "Portuguese-language surnames", "Portuguese patronymic surnames", "French masculine given names", "French given names", "French-language surnames",
    "Italian masculine given names", "Italian given names", "Italian-language surnames"))   # (asking for these only keeps the answers small)
ctx = ssl.create_default_context(cafile="/root/.ccr/ca-bundle.crt") if os.path.exists("/root/.ccr/ca-bundle.crt") else ssl.create_default_context()


def strip(s): return "".join(c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn").replace("ß", "ss")


def api(params):
    url = "https://en.wikipedia.org/w/api.php?" + urllib.parse.urlencode(params)
    for k in range(10):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=60, context=ctx) as r: return json.loads(r.read())
        except urllib.error.HTTPError as e:
            time.sleep((int(e.headers.get("Retry-After", "8")) if e.code == 429 else 4) + 2)
        except Exception: time.sleep(4)
    raise RuntimeError("Wikipedia did not answer")


def lookup(titles, cache):
    """For each title: {"redirect": target or None, "exists": bool, "cats": [categories]}. Fifty titles to a request."""
    todo = [t for t in dict.fromkeys(titles) if t not in cache]
    for i in range(0, len(todo), 50):
        batch = todo[i:i + 50]
        res = {t: {"redirect": None, "exists": False, "cats": []} for t in batch}
        params = {"action": "query", "titles": "|".join(batch), "redirects": 1, "prop": "categories", "cllimit": "max", "clcategories": NAME_CATEGORIES, "format": "json", "formatversion": 2}
        while True:
            r = api(params)
            q = r.get("query", {})
            norm = {n["from"]: n["to"] for n in q.get("normalized", [])}
            redir = {n["from"]: n["to"] for n in q.get("redirects", [])}
            pages = {p["title"]: p for p in q.get("pages", [])}
            for t in batch:
                name = norm.get(t, t)
                target = redir.get(name)
                if target: res[t]["redirect"] = target
                p = pages.get(target or name)
                if p and not p.get("missing"):
                    res[t]["exists"] = True
                    if not target: res[t]["cats"] += [c["title"] for c in p.get("categories", [])]
            if "continue" in r: params.update(r["continue"])
            else: break
        cache.update(res)
        json.dump(cache, open(CACHE, "w", encoding="utf-8"))
        time.sleep(1.5)
        print("  looked up %d/%d" % (min(i + 50, len(todo)), len(todo)), file=sys.stderr, flush=True)


def words_of(text):
    return {w for name in text.split(";") for w in re.split(r"[ \-'’.]", name.split("|")[0]) if len(w) >= 3 and re.fullmatch(r"[A-Za-zñÑçÇ]+", w)}


def main():
    src = open(DATA, encoding="utf-8").read()
    by_country = {}
    for m in re.finditer(r'new OnomaverseCountry\("(\w+)", "[^"]*",\s*"([^"]*)",\s*"([^"]*)"', src):
        by_country[m.group(1)] = words_of(m.group(2)) | words_of(m.group(3))
    cache = json.load(open(CACHE, encoding="utf-8")) if os.path.exists(CACHE) else {}
    result, report = {"_source": "Wikipedia redirects and name categories, https://en.wikipedia.org (CC BY-SA 4.0); checked " + time.strftime("%Y-%m-%d")}, []
    for lang, countries in GROUPS.items():
        words = sorted(set().union(*[by_country.get(c, set()) for c in countries]))
        print(lang, len(words), "words", file=sys.stderr)
        lookup(words, cache)
        cands = {}
        for w in words:
            info = cache[w]
            if info["redirect"] and strip(info["redirect"]).lower() == w.lower() and info["redirect"] != w and " " not in info["redirect"] and "(" not in info["redirect"]:
                continue                                                     # (rule A below)
            if any(CATEGORY[lang].match(c) for c in info["cats"]): continue   # the plain spelling is itself a name of this language: keep it
            out = []
            for i, ch in enumerate(w):
                for rep in ADD[lang].get(ch.lower(), ""):
                    out.append(w[:i] + (rep.upper() if ch.isupper() else rep) + w[i + 1:])
            cands[w] = out
        lookup([c for v in cands.values() for c in v], cache)
        table = {}
        for w in words:
            info = cache[w]
            r = info["redirect"]
            if r and strip(r).lower() == w.lower() and r != w and " " not in r and "(" not in r:
                table[w] = (r, "redirect"); continue
            good = [c for c in cands.get(w, []) if cache[c]["exists"] and not cache[c]["redirect"] and any(CATEGORY[lang].match(x) for x in cache[c]["cats"])]
            if len(good) == 1: table[w] = (good[0], "category")
            elif len(good) > 1:
                pick = [g for g in good if (lang == "pt" and re.search("[âêôãõ]", g))] if "BR" in countries and False else []
                report.append("%s: %s -> several (%s): left as it is" % (lang, w, ", ".join(good)))
        result[lang] = {w: a for w, (a, _) in table.items()}
        for w, (a, how) in sorted(table.items()): print("%s  %-14s -> %-14s (%s)" % (lang, w, a, how))
    for r in report: print(r)
    json.dump(result, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)
    print("wrote", OUT, {k: len(v) for k, v in result.items() if k != "_source"}, file=sys.stderr)


if __name__ == "__main__":
    main()
