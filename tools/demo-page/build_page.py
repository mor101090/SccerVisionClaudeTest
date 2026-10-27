# Builds the standalone demo page: match-template.html + the audio bundle + packed replays, and the condition variants.
#   python3 build_page.py <work dir with e<seed>.json, v<seed>-<code>.json and audio.iife.js> <output html>
# A replay is packed as gzip + base64 JSON whose frames are stored column by column as differences from the previous
# frame ("cols"); that compresses about 2.7 times better than plain frames. The page rebuilds the frames.
import base64, glob, gzip, json, os, sys

# Demo matches. The FIRST one (7274) is the one the page opens with: it is staged to contain exactly one direct red card,
# one light injury (plays on) and one serious injury (automatic substitution), so they can be examined. The others are
# shown at random after "New match".
SEEDS = (7274, 7014, 7052, 7070, 7099, 7117)
# Matches also recorded in rain, snow, wind and on worse pitches (loaded on demand from conditions/<seed>-<code>.txt).
VARIANT_SEEDS = (7274, 7014, 7052)


def pack(path):
    r = json.load(open(path))
    frames = r.pop('frames')
    n = len(frames[0])
    assert all(len(f) == n for f in frames)
    cols = []
    for c in range(n):
        prev, col = 0, []
        for f in frames:
            col.append(f[c] - prev)
            prev = f[c]
        cols.append(col)
    r['cols'] = cols
    return base64.b64encode(gzip.compress(json.dumps(r, separators=(',', ':')).encode(), 9)).decode()


here = os.path.dirname(os.path.abspath(__file__))
work, out = sys.argv[1], sys.argv[2]
t = open(os.path.join(here, 'match-template.html')).read()
t = t.replace('__AUDIO__', open(os.path.join(work, 'audio.iife.js')).read())
t = t.replace('__LOGO__', base64.b64encode(open(os.path.join(here, 'soccervision-logo.png'), 'rb').read()).decode())
packed = [pack(os.path.join(work, f'e{s}.json')) for s in SEEDS]
t = t.replace('__VARIANT_SEEDS__', json.dumps(list(VARIANT_SEEDS))).replace('__SEEDS__', json.dumps(list(SEEDS)))
open(out, 'w').write(t.replace('__REPLAYS__', json.dumps(packed)))
print(round(len(open(out).read()) / 1e6, 2), 'MB')
cond = os.path.join(os.path.dirname(out), 'conditions')
os.makedirs(cond, exist_ok=True)
for old in glob.glob(os.path.join(cond, '*.txt')):
    os.remove(old)
size = 0
for v in sorted(glob.glob(os.path.join(work, 'v*.json'))):
    name = os.path.basename(v)[1:-5] + '.txt'
    data = pack(v)
    open(os.path.join(cond, name), 'w').write(data)
    size += len(data)
print(len(glob.glob(os.path.join(cond, '*.txt'))), 'condition variants,', round(size / 1e6, 1), 'MB')
