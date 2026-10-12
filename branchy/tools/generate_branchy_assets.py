from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from PIL import Image


TARGET_SIZES = (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256)
ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def fitted_mark(source: Image.Image, canvas_size: tuple[int, int], fill: float = 0.82) -> Image.Image:
    alpha_bounds = source.getchannel("A").getbbox()
    if alpha_bounds is None:
        raise ValueError("The source logo has no visible pixels.")

    mark = source.crop(alpha_bounds)
    max_width = max(1, round(canvas_size[0] * fill))
    max_height = max(1, round(canvas_size[1] * fill))
    scale = min(max_width / mark.width, max_height / mark.height)
    resized = mark.resize(
        (max(1, round(mark.width * scale)), max(1, round(mark.height * scale))),
        Image.Resampling.LANCZOS,
    )

    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    position = ((canvas_size[0] - resized.width) // 2, (canvas_size[1] - resized.height) // 2)
    canvas.alpha_composite(resized, position)
    return canvas


def monochrome_mark(source: Image.Image, size: int, color: tuple[int, int, int]) -> Image.Image:
    mark = fitted_mark(source, (size, size), fill=0.86)
    alpha = mark.getchannel("A")
    result = Image.new("RGBA", mark.size, (*color, 0))
    result.putalpha(alpha)
    return result


def save_png(source: Image.Image, destination: Path, size: tuple[int, int], fill: float = 0.82) -> None:
    fitted_mark(source, size, fill).save(destination, optimize=True)


def save_ico(source: Image.Image, destination: Path, monochrome: tuple[int, int, int] | None = None) -> None:
    base = (
        monochrome_mark(source, 256, monochrome)
        if monochrome is not None
        else fitted_mark(source, (256, 256), fill=0.86)
    )
    base.save(destination, format="ICO", sizes=[(size, size) for size in ICON_SIZES])


def generate(source_path: Path, svg_path: Path, assets: Path) -> None:
    assets.mkdir(parents=True, exist_ok=True)
    source = Image.open(source_path).convert("RGBA")

    shutil.copyfile(source_path, assets / "BranchySymbol.png")
    shutil.copyfile(svg_path, assets / "BranchySymbol.svg")

    save_ico(source, assets / "AppIcon.ico")
    save_ico(source, assets / "ContextMenuIcon.Dark.ico", (32, 32, 32))
    save_ico(source, assets / "ContextMenuIcon.Light.ico", (255, 255, 255))

    save_png(source, assets / "Square44x44Logo.scale-200.png", (88, 88))
    save_png(source, assets / "Square150x150Logo.scale-200.png", (300, 300))
    save_png(source, assets / "StoreLogo.png", (50, 50))
    save_png(source, assets / "LockScreenLogo.scale-200.png", (48, 48))
    save_png(source, assets / "Wide310x150Logo.scale-200.png", (620, 300), fill=0.72)
    save_png(source, assets / "SplashScreen.scale-200.png", (1240, 600), fill=0.68)

    for size in TARGET_SIZES:
        colored = fitted_mark(source, (size, size), fill=0.86)
        for suffix in ("", "_altform-unplated", "_altform-lightunplated"):
            colored.save(
                assets / f"Square44x44Logo.targetsize-{size}{suffix}.png",
                optimize=True,
            )


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate BRANCHY Windows package assets.")
    parser.add_argument("png", type=Path, help="Colored BRANCHY symbol PNG")
    parser.add_argument("svg", type=Path, help="Monochrome BRANCHY symbol SVG")
    parser.add_argument("assets", type=Path, help="Destination Assets directory")
    arguments = parser.parse_args()
    generate(arguments.png, arguments.svg, arguments.assets)


if __name__ == "__main__":
    main()
