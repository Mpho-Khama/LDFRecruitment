"""FastAPI service: certificate image/PDF -> OCR (Tesseract) -> NLP extraction (spaCy + fuzzy matching).

Run:  uvicorn app.main:app --port 8001
"""
from __future__ import annotations

from fastapi import FastAPI, File, HTTPException, UploadFile
from pydantic import BaseModel

from . import ocr
from .parser import parse_certificate_text

app = FastAPI(title="LDF Certificate Extraction Service", version="1.0")

MAX_BYTES = 10 * 1024 * 1024
ALLOWED = {"image/jpeg", "image/png", "application/pdf"}


class SubjectOut(BaseModel):
    subject: str
    grade: str
    confidence: float
    raw_line: str | None = None


class ExtractionOut(BaseModel):
    subjects: list[SubjectOut]
    warnings: list[str] = []


@app.get("/health")
def health():
    try:
        import pytesseract

        version = str(pytesseract.get_tesseract_version())
        return {"status": "ok", "tesseract": version}
    except Exception as exc:  # TesseractNotFoundError and friends
        return {"status": "degraded", "tesseract": None, "detail": str(exc)}


@app.post("/extract", response_model=ExtractionOut)
def extract(file: UploadFile = File(...)):
    if file.content_type not in ALLOWED:
        raise HTTPException(status_code=415, detail="Only JPEG, PNG or PDF files are supported.")
    data = file.file.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise HTTPException(status_code=413, detail="File too large.")

    try:
        text = ocr.extract_text(data, file.content_type)
    except Exception as exc:
        name = type(exc).__name__
        if name == "TesseractNotFoundError":
            raise HTTPException(status_code=503, detail="Tesseract OCR is not installed on the server.")
        raise HTTPException(status_code=422, detail=f"Could not read the document: {name}")

    extractions, warnings = parse_certificate_text(text)
    return ExtractionOut(subjects=[SubjectOut(**e.to_dict()) for e in extractions], warnings=warnings)
