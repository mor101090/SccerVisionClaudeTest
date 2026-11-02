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
