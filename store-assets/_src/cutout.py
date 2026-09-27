"""Cuts the food out of the bundled sample photo, for the "food breaking out of the frame" look.

    python3 store-assets/_src/cutout.py        (build.mjs runs this for you)

Input : src/PurePrep/Resources/Raw/sample-pierogi.jpg  (Unsplash licence, ships in the app)
Output: store-assets/_src/assets/pierogi-cutout.png   (RGBA, same 1280x960 canvas as the photo)

The output keeps the photo's exact pixel grid, so the HTML can lay the cutout directly over the
framed photo and only the parts outside the frame read as "popping out". Needs numpy + Pillow.

How: the plate is white and the table is pale wood, both low-saturation, while the pierogi, onion
and chives are all warm and saturated. A saturation threshold separates them cleanly; the largest
connected region is the food (drops the plate-rim and table specks), holes are filled, and the edge
is pulled in a few pixels and softened so no white plate fringe shows against the dark background.
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
SRC = HERE.parents[1] / "src" / "PurePrep" / "Resources" / "Raw" / "sample-pierogi.jpg"
OUT = HERE / "assets" / "pierogi-cutout.png"


def dilate(m: np.ndarray) -> np.ndarray:
    d = m.copy()
    d[1:, :] |= m[:-1, :]
    d[:-1, :] |= m[1:, :]
    d[:, 1:] |= m[:, :-1]
    d[:, :-1] |= m[:, 1:]
    return d


def flood(mask: np.ndarray, seed: np.ndarray) -> np.ndarray:
    """Region of `mask` 4-connected to `seed`."""
    region = seed & mask
    while True:
        grown = dilate(region) & mask
        # grow several steps per equality check
        for _ in range(15):
            grown = dilate(grown) & mask
        if np.array_equal(grown, region):
            return region
        region = grown


def main() -> None:
    img = Image.open(SRC).convert("RGB")
    px = np.asarray(img).astype(np.float32)
    mx, mn = px.max(-1), px.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1)
    food = sat > 0.18

    # The food cluster: flood from a point in the middle of the top pierogi.
    h, w = food.shape
    seed = np.zeros_like(food)
    seed[200, 640] = True
    food = flood(food, seed)

    # Fill holes (chive-shadow specks, glossy highlights): background not reachable from the border.
    bg = ~food
    border = np.zeros_like(bg)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    food = ~flood(bg, border & bg)

    alpha = Image.fromarray((food * 255).astype(np.uint8))
    alpha = alpha.filter(ImageFilter.MinFilter(9))           # pull the edge in ~4 px: no white fringe
    alpha = alpha.filter(ImageFilter.GaussianBlur(1.3))      # soft, photographic edge

    out = img.convert("RGBA")
    out.putalpha(alpha)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    out.save(OUT)
    print(f"wrote {OUT.relative_to(HERE.parents[1])}  ({w}x{h})")


if __name__ == "__main__":
    main()
