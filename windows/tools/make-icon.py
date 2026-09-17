#!/usr/bin/env python3
"""Generates windows/src/OnScreenChat/Assets/tray.ico — a speech-bubble glyph.

No image library is available in this environment, so the icon is rasterised
here and written as 32-bpp BMP entries, which is the format LoadImage handles
most reliably for a tray icon. Re-run after changing the shape:

    python3 windows/tools/make-icon.py
"""

from pathlib import Path
import struct
import sys

OUT = Path(__file__).resolve().parents[1] / "src" / "OnScreenChat" / "Assets" / "tray.ico"

SIZES = (16, 20, 24, 32, 48, 64)
SUPERSAMPLE = 4

# A mid-tone blue stays legible on both light and dark taskbars.
COLOR = (0x2F, 0x7D, 0xE1)  # R, G, B


def inside_bubble(x: float, y: float, size: float) -> bool:
    """A rounded speech bubble with a tail, in a unit-ish coordinate space."""
    u, v = x / size, y / size

    # Body: rounded rectangle spanning most of the width, upper ~70%.
    left, right, top, bottom, radius = 0.08, 0.92, 0.12, 0.70, 0.16

    if left <= u <= right and top <= v <= bottom:
        # Round the corners by distance from the inset corner centres.
        cx = min(max(u, left + radius), right - radius)
        cy = min(max(v, top + radius), bottom - radius)
        if (u - cx) ** 2 + (v - cy) ** 2 <= radius ** 2:
            return True

    # Tail: a triangle hanging from the lower-left of the body.
    if 0.22 <= u <= 0.50 and 0.62 <= v <= 0.92:
        # Slopes down-left from the body to a point.
        return (v - 0.62) <= (0.50 - u) * 2.2

    return False


def render(size: int) -> bytes:
    """Bottom-up BGRA rows, as a DIB expects."""
    hi = size * SUPERSAMPLE
    rows = []

    for y in range(size - 1, -1, -1):  # bottom-up
        row = bytearray()
        for x in range(size):
            hits = 0
            for sy in range(SUPERSAMPLE):
                for sx in range(SUPERSAMPLE):
                    px = x * SUPERSAMPLE + sx + 0.5
                    py = y * SUPERSAMPLE + sy + 0.5
                    if inside_bubble(px, py, hi):
                        hits += 1
            alpha = round(255 * hits / (SUPERSAMPLE * SUPERSAMPLE))
            # Premultiplied-looking straight BGRA; alpha carries the shape.
            row += bytes((COLOR[2], COLOR[1], COLOR[0], alpha))
        rows.append(bytes(row))

    return b"".join(rows)


def bmp_entry(size: int) -> bytes:
    """BITMAPINFOHEADER + BGRA pixels + an (unused, zeroed) AND mask."""
    header = struct.pack(
        "<IiiHHIIiiII",
        40,            # biSize
        size,          # biWidth
        size * 2,      # biHeight — doubled: colour plus mask
        1,             # biPlanes
        32,            # biBitCount
        0,             # biCompression = BI_RGB
        0,             # biSizeImage
        0, 0,          # resolution
        0, 0,          # palette
    )

    pixels = render(size)

    # 1-bpp AND mask, padded to 4-byte rows. The alpha channel does the real
    # work, but the mask must still be present and correctly sized.
    mask_row = ((size + 31) // 32) * 4
    mask = b"\x00" * (mask_row * size)

    return header + pixels + mask


def main() -> int:
    images = [bmp_entry(size) for size in SIZES]

    header = struct.pack("<HHH", 0, 1, len(images))  # reserved, type=icon, count
    offset = 6 + 16 * len(images)

    directory = b""
    for size, data in zip(SIZES, images):
        directory += struct.pack(
            "<BBBBHHII",
            size if size < 256 else 0,
            size if size < 256 else 0,
            0, 0,          # colours, reserved
            1, 32,         # planes, bit count
            len(data), offset,
        )
        offset += len(data)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(header + directory + b"".join(images))
    print(f"wrote {OUT.relative_to(OUT.parents[4])}  "
          f"({OUT.stat().st_size:,} bytes, sizes: {', '.join(map(str, SIZES))})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
