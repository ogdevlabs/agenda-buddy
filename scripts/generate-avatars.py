#!/usr/bin/env python3
"""Generates the avatar assets AvatarCatalog names.

Output: AgendaBuddy.MobileApp/Resources/Images/avatar_NN.svg, one per AvatarCatalog id.

These are a BUILD INPUT that happens to be committed, like docs/api/openapi/*.json: regenerable on
demand, not hand-edited. Re-run after changing a palette or a motif; keep AvatarCatalog.Count in step,
or the client asks for an asset that does not exist and draws an empty circle.

Abstract marks rather than illustrated characters, deliberately. An illustrated avatar assigned at
random puts a face on a real person that is not theirs, which reads worse than no picture -- and it
implies a gender, an age and an ethnicity nobody chose. A geometric mark is unmistakably a placeholder
while still being distinct at 40px, which is the whole job. It is also the convention a user already
recognises from Notion, Linear and Google.

MAUI converts each .svg to a raster at build time and it is referenced by the .png name, so
AvatarCatalog's id + ".png" is the Image.Source (see AvatarAsset on the client models).

Usage: python3 scripts/generate-avatars.py
"""

import pathlib

# 12 grounds, each with a motif colour that stays legible on it at 40px. Deliberately mid-saturation:
# a list of these sits next to text, so a fully saturated ground would fight the row it belongs to.
PALETTES = [
    ("#4F46E5", "#C7D2FE"),  # indigo
    ("#0EA5E9", "#BAE6FD"),  # sky
    ("#0D9488", "#99F6E4"),  # teal
    ("#16A34A", "#BBF7D0"),  # green
    ("#CA8A04", "#FEF08A"),  # amber
    ("#EA580C", "#FED7AA"),  # orange
    ("#DC2626", "#FECACA"),  # red
    ("#DB2777", "#FBCFE8"),  # pink
    ("#9333EA", "#E9D5FF"),  # purple
    ("#7C3AED", "#DDD6FE"),  # violet
    ("#475569", "#CBD5E1"),  # slate
    ("#0F766E", "#A7F3D0"),  # deep teal
]

# Each motif is drawn inside a 128x128 box on top of the ground. Two motifs per palette gives 24.
def motifs(fg):
    return [
        # A disc, offset low-right: the simplest mark, and the one that reads at the smallest size.
        f'<circle cx="82" cy="82" r="34" fill="{fg}"/>',
        # A ring, concentric: distinct from the disc even when both are tiny.
        f'<circle cx="64" cy="64" r="36" fill="none" stroke="{fg}" stroke-width="16"/>',
        # A half-disc on the baseline.
        f'<path d="M20 92a44 44 0 0 1 88 0z" fill="{fg}"/>',
        # A quarter, anchored to a corner.
        f'<path d="M128 0v64a64 64 0 0 0-64-64z" fill="{fg}"/>',
        # A soft-cornered triangle.
        f'<path d="M64 26 L110 100 H18 Z" fill="{fg}" stroke="{fg}" stroke-width="14" stroke-linejoin="round"/>',
        # A diamond.
        f'<path d="M64 22 106 64 64 106 22 64 Z" fill="{fg}"/>',
        # An arch.
        f'<path d="M28 104V64a36 36 0 0 1 72 0v40h-20V64a16 16 0 0 0-32 0v40z" fill="{fg}"/>',
        # A plus.
        f'<path d="M52 24h24v28h28v24H76v28H52V76H24V52h28z" fill="{fg}"/>',
        # Two chevrons.
        f'<path d="M34 44 64 74 94 44l14 14-44 44-44-44z" fill="{fg}"/>',
        # A 2x2 grid of discs.
        f'<g fill="{fg}"><circle cx="46" cy="46" r="17"/><circle cx="82" cy="46" r="17"/>'
        f'<circle cx="46" cy="82" r="17"/><circle cx="82" cy="82" r="17"/></g>',
        # A wave.
        f'<path d="M12 78c16-26 32-26 48 0s32 26 56 0" fill="none" stroke="{fg}" '
        f'stroke-width="17" stroke-linecap="round"/>',
        # Stacked bars of decreasing width.
        f'<g fill="{fg}"><rect x="26" y="36" width="76" height="16" rx="8"/>'
        f'<rect x="26" y="62" width="56" height="16" rx="8"/>'
        f'<rect x="26" y="88" width="36" height="16" rx="8"/></g>',
    ]


def main():
    root = pathlib.Path(__file__).resolve().parent.parent
    out = root / "AgendaBuddy.MobileApp" / "Resources" / "Images"
    out.mkdir(parents=True, exist_ok=True)

    written = []
    index = 0
    # Palette-major so consecutive ids differ in colour as well as motif -- a list of newly created
    # accounts is often consecutive, and two adjacent rows differing only by motif looks like a glitch.
    for palette_index, (ground, fg) in enumerate(PALETTES):
        shapes = motifs(fg)
        for motif_offset in (0, 1):
            index += 1
            if index > 24:
                break
            motif = shapes[(palette_index * 2 + motif_offset) % len(shapes)]
            name = f"avatar_{index:02d}.svg"
            # viewBox only, no width/height: MAUI rasterises to whatever the Image requests, so a fixed
            # pixel size here would just cap the resolution on a high-density screen.
            (out / name).write_text(
                '<?xml version="1.0" encoding="UTF-8"?>\n'
                '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">\n'
                f'  <rect width="128" height="128" fill="{ground}"/>\n'
                f'  {motif}\n'
                "</svg>\n"
            )
            written.append(name)

    print(f"wrote {len(written)} avatars to {out.relative_to(root)}")
    if len(written) != 24:
        raise SystemExit(f"expected 24 avatars, wrote {len(written)} — AvatarCatalog.Count will not match")


if __name__ == "__main__":
    main()
