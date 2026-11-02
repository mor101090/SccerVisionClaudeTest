"""Picture handling after generation: crop, shrink, an optional cut-out of the background, WebP/PNG."""
import io


def to_image(png_bytes):
    from PIL import Image
    return Image.open(io.BytesIO(png_bytes)).convert("RGBA")


def cutout(img):
    """Removes the background (needs `pip install rembg`); without it the picture keeps its plain background."""
    try:
        from rembg import remove
    except ImportError:
        return img
    return remove(img)


def save(img, path, max_side=512, fmt="WEBP", quality=88):
    from PIL import Image
    w, h = img.size
    k = max_side / max(w, h)
    if k < 1: img = img.resize((round(w * k), round(h * k)), Image.LANCZOS)
    img.save(path, fmt, quality=quality) if fmt == "WEBP" else img.save(path, fmt)
    return path


def split_shirt(img, top=0.42):
    """Lifts the chroma-green shirt out of a portrait: returns (portrait with the shirt in grey shading, mask) or (img, None).

    The shading is the shirt's brightness scaled so that its typical tone is light grey: the page multiplies it by the kit colour
    inside the mask (a white kit stays white with real folds, a navy kit becomes navy). The mask is an RGBA picture, alpha = shirt."""
    from PIL import Image, ImageFilter
    rgb = img.convert("RGB"); w, h = rgb.size
    px = rgb.load()
    mask = Image.new("L", (w, h), 0); m = mask.load()
    lum = []
    for y in range(int(h * top), h):
        for x in range(w):
            r, g, b = px[x, y]
            if g > 70 and g > r * 1.3 and g > b * 1.25:
                m[x, y] = 255
                lum.append(g)
    if len(lum) < w * h * 0.02:
        return img, None
    mask = mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    lum.sort(); typical = lum[len(lum) // 2]
    out = img.convert("RGBA"); o = out.load(); m = mask.load()
    for y in range(int(h * top) - 2, h):
        for x in range(w):
            k = m[x, y]
            if not k: continue
            r, g, b, a = o[x, y]
            shade = min(255, int(235 * g / typical))
            t = k / 255.0
            o[x, y] = (int(r + (shade - r) * t), int(g + (shade - g) * t), int(b + (shade - b) * t), a)
    alpha = Image.new("RGBA", (w, h), (0, 0, 0, 0)); alpha.putalpha(mask)
    return out, alpha
