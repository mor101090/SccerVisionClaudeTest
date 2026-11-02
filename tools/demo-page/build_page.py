# Builds the standalone demo page: template.html (the page and its parts: css/*.css, js/*.js, put in at the
# "@include" lines) + the audio and texture bundles + the logo. The matches themselves are played by the engine inside
# the page (WebAssembly, docs/engine, built by build.sh).
#   python3 build_page.py <work dir with audio.iife.js and textures.iife.js> <output html>
import base64, os, sys

# The staged match (a light injury, a yellow, a direct red, a serious injury with a substitution, a second yellow and
# red) is this seed, so it is identical every time and in every weather.
STAGED_SEED = 7274

here = os.path.dirname(os.path.abspath(__file__))


def assemble():
    """The template with every "/*@include file*/" or "//@include file" line replaced by that file."""
    out = []
    for line in open(os.path.join(here, 'template.html'), encoding='utf8').read().split('\n'):
        s = line.strip()
        if s.startswith(('/*@include ', '//@include ')):
            path = s.split('@include ', 1)[1].rstrip('*/').strip()
            out.append(open(os.path.join(here, path), encoding='utf8').read())
        else:
            out.append(line)
    return '\n'.join(out)


if __name__ == '__main__' and len(sys.argv) == 2 and sys.argv[1] == '--assemble':
    sys.stdout.write(assemble())
    sys.exit(0)

work, out = sys.argv[1], sys.argv[2]
t = assemble()
t = t.replace('__AUDIO__', open(os.path.join(work, 'audio.iife.js')).read())
t = t.replace('__TEXTURES__', open(os.path.join(work, 'textures.iife.js')).read())
t = t.replace('__LOGO__', base64.b64encode(open(os.path.join(here, 'soccervision-logo.png'), 'rb').read()).decode())
t = t.replace('__STAGED_SEED__', str(STAGED_SEED)).replace('__ENGINE_DIR__', os.environ.get('ENGINE_DIR', 'engine/'))
open(out, 'w').write(t)
print(round(len(t) / 1e6, 2), 'MB')
