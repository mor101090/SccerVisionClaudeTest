# Builds the standalone demo page: match-template.html + the audio and texture bundles + the logo. The matches themselves
# are played by the engine inside the page (WebAssembly, docs/engine, built by build.sh).
#   python3 build_page.py <work dir with audio.iife.js and textures.iife.js> <output html>
import base64, os, sys

# The staged match (a light injury, a yellow, a direct red, a serious injury with a substitution, a second yellow and
# red) is this seed, so it is identical every time and in every weather.
STAGED_SEED = 7274

here = os.path.dirname(os.path.abspath(__file__))
work, out = sys.argv[1], sys.argv[2]
t = open(os.path.join(here, 'match-template.html')).read()
t = t.replace('__AUDIO__', open(os.path.join(work, 'audio.iife.js')).read())
t = t.replace('__TEXTURES__', open(os.path.join(work, 'textures.iife.js')).read())
t = t.replace('__LOGO__', base64.b64encode(open(os.path.join(here, 'soccervision-logo.png'), 'rb').read()).decode())
t = t.replace('__STAGED_SEED__', str(STAGED_SEED)).replace('__ENGINE_DIR__', os.environ.get('ENGINE_DIR', 'engine/'))
open(out, 'w').write(t)
print(round(len(t) / 1e6, 2), 'MB')
