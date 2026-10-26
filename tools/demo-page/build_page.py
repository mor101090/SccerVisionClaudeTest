# Builds the standalone demo page: match-template.html + the audio bundle + gzip/base64 replays.
#   python3 build_page.py <work dir with e<seed>.json and audio.iife.js> <output html>
import base64, gzip, json, os, sys

SEEDS = (7014, 7052, 7070, 7099, 7117, 7130)  # demo matches: each has a penalty, a free-kick shot and offsides

here = os.path.dirname(os.path.abspath(__file__))
work, out = sys.argv[1], sys.argv[2]
t = open(os.path.join(here, 'match-template.html')).read()
t = t.replace('__AUDIO__', open(os.path.join(work, 'audio.iife.js')).read())
t = t.replace('__LOGO__', base64.b64encode(open(os.path.join(here, 'soccervision-logo.png'), 'rb').read()).decode())
packed = []
for s in SEEDS:
    r = json.load(open(os.path.join(work, f'e{s}.json')))
    packed.append(base64.b64encode(gzip.compress(json.dumps(r, separators=(',', ':')).encode(), 9)).decode())
open(out, 'w').write(t.replace('__REPLAYS__', json.dumps(packed)))
print(round(len(open(out).read()) / 1e6, 2), 'MB')
