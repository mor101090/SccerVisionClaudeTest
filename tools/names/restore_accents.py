#!/usr/bin/env python3
"""Puts back the accents the Onomaverse tables lack (Jose -> José, Garcia -> García, Goncalves -> Gonçalves), checked against Wikipedia.

The dataset writes many Spanish, Portuguese and French names without their accents. A name is only changed if Wikipedia says so:

  A. English Wikipedia redirects the plain spelling to the accented one (Garcia -> García, Nunez -> Núñez, Sebastien -> Sébastien), or
  B. an accented spelling (the word with ONE diacritic added) has a Wikipedia article in a name category of the language -
     "Spanish masculine given names", "Spanish-language surnames", "Portuguese ...", "French ...", "Italian ..." - and the plain
     spelling is not itself in a name category of that language (Mario stays Mario in Spain, becomes Mário in Portugal).

  and in every case the language's own Wikipedia (es / pt / fr / it.wikipedia.org) must agree: the plain spelling redirects to
     the accented one there, or the accented one has a page and the plain one is not an article of its own.

Reads the words of the name table (as the dataset spells them) (run build_onomaverse_names.py first), writes accents.json (which the builder then applies,
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

GROUPS = {"es": ["ES", "AR", "MX", "CO", "UY", "CL", "PE", "BO", "CR", "PA"], "pt": ["PT"], "br": ["BR"], "fr": ["FR"], "it": ["IT"]}
ADD = {  # the single diacritics that can be put on a letter, by language
    "es": {"a": "á", "e": "é", "i": "í", "o": "ó", "u": "ú", "n": "ñ"},
    "pt": {"a": "áâã", "e": "éê", "i": "í", "o": "óôõ", "u": "ú", "c": "ç"},
    "br": {"a": "áâã", "e": "éê", "i": "í", "o": "óôõ", "u": "ú", "c": "ç"},
    "fr": {"a": "àâ", "e": "éèêë", "i": "îï", "o": "ô", "u": "ùû", "c": "ç"},
    "it": {"a": "à", "e": "èé", "i": "ì", "o": "ò", "u": "ù"},
}
CATEGORY = {  # a name category of the language
    "es": re.compile(r"^Category:(Spanish|Latin American|Mexican|Argentine|Chilean|Colombian)[- ](language )?(masculine )?(given names|surnames)|^Category:Spanish[- ]language surnames"),
    "pt": re.compile(r"^Category:(Portuguese|Brazilian)[- ](language )?(masculine |patronymic )?(given names|surnames)"),
    "br": re.compile(r"^Category:(Portuguese|Brazilian)[- ](language )?(masculine |patronymic )?(given names|surnames)"),
    "fr": re.compile(r"^Category:French[- ](language )?(masculine )?(given names|surnames)"),
    "it": re.compile(r"^Category:Italian[- ](language )?(masculine )?(given names|surnames)"),
}
NAME_CATEGORIES = "|".join("Category:" + c for c in (
    "Spanish masculine given names", "Spanish given names", "Spanish-language surnames", "Portuguese masculine given names", "Portuguese given names",
    "Portuguese-language surnames", "Portuguese patronymic surnames", "French masculine given names", "French given names", "French-language surnames",
    "Italian masculine given names", "Italian given names", "Italian-language surnames"))   # (asking for these only keeps the answers small)
LETTERS = {  # the accented letters each language writes: anything else is a spelling of another language (Cantù, Ossès, Ionuț, Säntis)
    "es": set("áéíóúñüÁÉÍÓÚÑÜ"), "pt": set("áéíóúâêôãõçàÁÉÍÓÚÂÊÔÃÕÇÀ"), "br": set("áéíóúâêôãõçàÁÉÍÓÚÂÊÔÃÕÇÀ"), "fr": set("àâçéèêëîïôùûüÿœÀÂÇÉÈÊËÎÏÔÙÛÜŸŒ"), "it": set("àèéìòùÀÈÉÌÒÙ"),
}
VOWELS, STRONG = set("aeiouáéíóúü"), set("aeoáéó")


def spanish_ok(word):
    """Is the written accent right by the Spanish rules? A word ending in a vowel, n or s is stressed on the next-to-last syllable,
    any other on the last one, and the accent is written only where the stress breaks that rule - or on a weak vowel (i, u) that is
    not run together with the vowel next to it (Díaz, Raúl, Macías). So Hernándo and Márcio are wrong, Hernán and Ramón right."""
    w = word.lower()
    acc = [i for i, ch in enumerate(w) if ch in "áéíóú"]
    if len(acc) != 1: return len(acc) == 0
    i = acc[0]
    if w[i] in "íú" and ((i > 0 and w[i - 1] in STRONG) or (i + 1 < len(w) and w[i + 1] in STRONG)): return True   # (the hiatus)
    # the syllable nuclei: runs of vowels, split where two strong vowels meet (a-e, e-o) - the run that holds the accent is "stressed"
    nuclei, k = [], 0
    while k < len(w):
        if w[k] in VOWELS:
            j = k
            while j + 1 < len(w) and w[j + 1] in VOWELS and not (w[j] in STRONG and w[j + 1] in STRONG): j += 1
            nuclei.append((k, j)); k = j + 1
        else: k += 1
    if len(nuclei) < 2: return False                                       # (one syllable: no accent, apart from a few words)
    stressed = next(n for n, (a, b) in enumerate(nuclei) if a <= i <= b)
    plain = w.translate(str.maketrans("áéíóú", "aeiou"))
    default = len(nuclei) - 2 if plain[-1] in "aeiouns" else len(nuclei) - 1
    return stressed != default


def portuguese_ok(word):
    """A Portuguese accent where the rules want one: before a final -io/-ia/-eo/-ea/-ua/-uo (Mário, Flávio, Antônio), on the next-to-last
    syllable of a word ending in l, r, n, x, ps or um (Hélder, Cléber, Édson), a final stressed -í/-ú after a vowel (Luís, Raúl is
    Spanish), a stressed final syllable of a word ending in a, e, o, as, es, os, em (José, Kauê, Simão has a tilde) - or a tilde."""
    w = word.lower()
    if re.search("[ãõ]", w) or "ç" in w: return True
    acc = [i for i, ch in enumerate(w) if ch in "áéíóúâêô"]
    if len(acc) != 1: return False
    i = acc[0]; tail = w[i + 1:]
    if re.fullmatch(r"[^aeiou]*(io|ia|eo|ea|ua|uo)s?", tail): return True                  # Mário, Antônio, Flávio
    if re.fullmatch(r"[^aeiou]*[aeiou][^aeiou]*", tail) and w[-1] in "lrnx": return True   # Hélder, Cléber, Édson, Nílton
    if tail == "" or re.fullmatch(r"[sz]?", tail): return i > 0                              # José, Kauê, Luís, Vinícius? (no)
    if re.fullmatch(r"[^aeiou]*[aeiou][^aeiou]*[aeiou][^aeiou]*", tail): return True       # proparoxytone: Vinícius, Cândido, Otávio
    return False


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


# read through by hand and left as the dataset has them: Baldé is a West African surname, Florin a Romanian name, Gallego the usual
# Spanish spelling (Gállego is a river), Chicó a character's name
NOT_CHANGED = {"es": {"Balde", "Florin", "Gallego"}, "br": {"Chico"}, "pt": set(), "fr": set(), "it": set()}
WIKI = {"es": "es", "pt": "pt", "br": "pt", "fr": "fr", "it": "it"}


def api_on(host, params):
    url = "https://%s.wikipedia.org/w/api.php?" % host + urllib.parse.urlencode(params)
    for k in range(10):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=60, context=ctx) as r: return json.loads(r.read())
        except urllib.error.HTTPError as e:
            time.sleep((int(e.headers.get("Retry-After", "8")) if e.code == 429 else 4) + 2)
        except Exception: time.sleep(4)
    raise RuntimeError("Wikipedia did not answer")


def lookup_on(host, titles, cache):
    """The language's own Wikipedia: for each title {"redirect", "exists", "disambig"} (cached under "@host|title")."""
    todo = [t for t in dict.fromkeys(titles) if "@%s|%s" % (host, t) not in cache]
    for i in range(0, len(todo), 50):
        batch = todo[i:i + 50]
        r = api_on(host, {"action": "query", "titles": "|".join(batch), "redirects": 1, "prop": "pageprops", "ppprop": "disambiguation", "format": "json", "formatversion": 2})
        q = r.get("query", {})
        norm = {n["from"]: n["to"] for n in q.get("normalized", [])}
        redir = {n["from"]: n["to"] for n in q.get("redirects", [])}
        pages = {p["title"]: p for p in q.get("pages", [])}
        for t in batch:
            name = norm.get(t, t); target = redir.get(name); p = pages.get(target or name)
            cache["@%s|%s" % (host, t)] = {"redirect": target, "exists": bool(p and not p.get("missing")), "disambig": bool(p and "disambiguation" in (p.get("pageprops") or {}))}
        json.dump(cache, open(CACHE, "w", encoding="utf-8"))
        time.sleep(1.5)
        print("  %s.wikipedia: %d/%d" % (host, min(i + 50, len(todo)), len(todo)), file=sys.stderr, flush=True)


def confirmed(host, w, a, cache):
    """The language's Wikipedia agrees: the plain spelling leads to the accented one, or the accented one is there and the plain one
    is not an article of its own (missing, or only a list of meanings)."""
    pw, pa = cache["@%s|%s" % (host, w)], cache["@%s|%s" % (host, a)]
    if pw["redirect"] and (pw["redirect"] == a or pw["redirect"].startswith(a + " (")): return True    # Garcia -> García
    if not pa["exists"]: return False                                                                   # (Dániel, Pepé: no such page)
    if pa["redirect"]:
        # both lead to one page that is written with the accent (Velazquez, Velázquez -> Diego Velázquez); one that leads back
        # to the plain spelling means the Wikipedia of the language writes it without (Óscar -> Oscar, Kévin -> Kevin)
        # - unless it is a list of meanings (González -> Gonzalez, a disambiguation page): that says nothing either way
        return pa["disambig"] or bool(re.search(r"(^|[ (])" + re.escape(a) + r"($|[ ),])", pa["redirect"]))
    return True                                                                                         # its own page (Durán, Hervé, Adrián)


def main():
    import subprocess, tempfile
    raw = os.path.join(tempfile.gettempdir(), "onomaverse-raw-names.cs")   # the names as the dataset spells them, accents not yet put back
    subprocess.run([sys.executable, os.path.join(HERE, "build_onomaverse_names.py"), "--no-accents", "--out", raw], check=True, stdout=subprocess.DEVNULL)
    src = open(raw, encoding="utf-8").read()
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
            if lang in ("fr", "it") and any(CATEGORY[lang].match(c) for c in info["cats"]): continue   # the plain spelling is itself a name there
            out = []
            for i, ch in enumerate(w):
                for rep in ADD[lang].get(ch.lower(), ""):
                    out.append(w[:i] + (rep.upper() if ch.isupper() else rep) + w[i + 1:])
            cands[w] = out
        lookup([c for v in cands.values() for c in v], cache)
        targets = [cache[w]["redirect"] for w in words if cache[w]["redirect"]]
        lookup(targets, cache)                                                # (the categories of the pages the plain spellings lead to)
        other = [CATEGORY[k] for k in CATEGORY if k != lang]

        def fits(word):
            if any(ch not in LETTERS[lang] for ch in word if ord(ch) > 127): return False
            if lang == "es": return spanish_ok(word)
            if lang in ("pt", "br"): return portuguese_ok(word)
            return True

        table = {}
        for w in words:
            info = cache[w]
            r = info["redirect"]
            if r and strip(r) == w and r != w and " " not in r and "(" not in r and fits(r):
                cats = cache.get(r, {}).get("cats", [])
                mine, theirs = any(CATEGORY[lang].match(c) for c in cats), any(o.match(c) for o in other for c in cats)
                spanish_form = lang != "es" and r in result.get("es", {}).values()
                if mine or not (theirs or spanish_form):
                    table[w] = (r, "redirect"); continue
            def evidence(c):
                if not (fits(c) and cache[c]["exists"]): return False
                rr = cache[c]["redirect"]
                if not rr: return any(CATEGORY[lang].match(x) for x in cache[c]["cats"])            # B: an article in a name category
                # C: Wikipedia keeps the article under the plain spelling and sends the accented one to it (Álvarez -> Alvarez,
                # Héctor -> Hector (given name)) - for Spanish and Portuguese, where the spelling rules above also have to agree
                return lang in ("es", "pt", "br") and re.sub(r" \((given )?name\)| \(surname\)", "", rr) == w
            good = [c for c in cands.get(w, []) if evidence(c)]
            if len(good) > 1 and lang in ("pt", "br"):   # (Portugal writes António, Brazil Antônio)
                pref = [g for g in good if re.search("[âêô]" if lang == "br" else "[áéó]", g)]
                if len(pref) == 1: good = pref
            if len(good) == 1: table[w] = (good[0], "category")
            elif len(good) > 1:
                pick = [g for g in good if (lang == "pt" and re.search("[âêôãõ]", g))] if "BR" in countries and False else []
                report.append("%s: %s -> several (%s): left as it is" % (lang, w, ", ".join(good)))
        host = WIKI[lang]
        lookup_on(host, [x for w, (a, _) in table.items() for x in (w, a)], cache)
        for w in list(table):
            if not confirmed(host, w, table[w][0], cache):
                report.append("%s: %s -> %s not confirmed by %s.wikipedia: left as it is" % (lang, w, table[w][0], host)); del table[w]
        result[lang] = {w: a for w, (a, _) in table.items() if w not in NOT_CHANGED[lang]}
        for w, (a, how) in sorted(table.items()): print("%s  %-14s -> %-14s (%s)" % (lang, w, a, how))
    for r in report: print(r)
    json.dump(result, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)
    print("wrote", OUT, {k: len(v) for k, v in result.items() if k != "_source"}, file=sys.stderr)


if __name__ == "__main__":
    main()
