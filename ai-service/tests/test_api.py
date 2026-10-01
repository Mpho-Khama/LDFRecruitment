from fastapi.testclient import TestClient

from app import main

client = TestClient(main.app)


def test_extract_rejects_unsupported_type():
    r = client.post("/extract", files={"file": ("x.txt", b"hello", "text/plain")})
    assert r.status_code == 415


def test_extract_returns_subjects(monkeypatch):
    monkeypatch.setattr(main.ocr, "extract_text", lambda data, ct: "0178 ENGLISH LANGUAGE (C)\n0390 MATHEMATICS (B)")
    r = client.post("/extract", files={"file": ("c.png", b"\x89PNGfake", "image/png")})
    assert r.status_code == 200
    body = r.json()
    assert {s["subject"]: s["grade"] for s in body["subjects"]} == {"ENGLISH LANGUAGE": "C", "MATHEMATICS": "B"}
    assert "raw_line" in body["subjects"][0]
