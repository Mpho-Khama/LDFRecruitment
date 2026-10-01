"""Text extraction from an uploaded certificate image or PDF using Tesseract (Methodology 3.12.2)."""
from __future__ import annotations

import io

import pytesseract
from PIL import Image, ImageOps

MAX_PDF_PAGES = 3
TARGET_MIN_WIDTH = 1600


def _preprocess(img: Image.Image) -> Image.Image:
    img = ImageOps.exif_transpose(img).convert("L")
    img = ImageOps.autocontrast(img)
    if img.width < TARGET_MIN_WIDTH:
        scale = TARGET_MIN_WIDTH / img.width
        img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
    return img


def _pdf_to_images(data: bytes) -> list[Image.Image]:
    import fitz  # PyMuPDF

    images = []
    with fitz.open(stream=data, filetype="pdf") as doc:
        for page in list(doc)[:MAX_PDF_PAGES]:
            pix = page.get_pixmap(matrix=fitz.Matrix(300 / 72, 300 / 72))
            images.append(Image.open(io.BytesIO(pix.tobytes("png"))))
    return images


def extract_text(data: bytes, content_type: str) -> str:
    if content_type == "application/pdf":
        images = _pdf_to_images(data)
    else:
        images = [Image.open(io.BytesIO(data))]

    pages = [pytesseract.image_to_string(_preprocess(im), config="--psm 6") for im in images]
    return "\n".join(pages)
