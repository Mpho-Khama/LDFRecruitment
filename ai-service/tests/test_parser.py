from app.parser import parse_certificate_text

CLEAN = """
LESOTHO GENERAL CERTIFICATE OF SECONDARY EDUCATION
Candidate: XXXX
0178 ENGLISH LANGUAGE (C)
0390 MATHEMATICS (B)
0270 SESOTHO (A)
0556 BIOLOGY (D)
"""

NOISY = """
| 0178 ENGLlSH LANGUAGE   (C
| 039O MATHEMATICS  8)
| 0270 SESOTHO A
| 0999 UNKNOWN FANCY WORDS (A)
"""


def by_subject(rows):
    return {r.subject: r for r in rows}


def test_clean_certificate_extracts_all_rows_with_full_confidence():
    rows, warnings = parse_certificate_text(CLEAN)
    got = by_subject(rows)
    assert {k: v.grade for k, v in got.items()} == {
        "ENGLISH LANGUAGE": "C", "MATHEMATICS": "B", "SESOTHO": "A", "BIOLOGY": "D"}
    assert all(v.confidence == 1.0 for v in got.values())
    assert warnings == []


def test_noisy_ocr_is_corrected_but_gets_lower_confidence():
    rows, _ = parse_certificate_text(NOISY)
    got = by_subject(rows)
    assert got["ENGLISH LANGUAGE"].grade == "C"
    assert got["ENGLISH LANGUAGE"].confidence < 1.0      # fuzzy match + damaged bracket
    assert got["MATHEMATICS"].grade == "8"               # recovered from the trailing character
    assert got["MATHEMATICS"].confidence < 0.75          # would be flagged for officer review
    assert got["SESOTHO"].grade == "A"
    assert "UNKNOWN FANCY WORDS" not in got


def test_text_without_rows_returns_warning():
    rows, warnings = parse_certificate_text("random text\nnothing useful here")
    assert rows == []
    assert warnings


def test_duplicate_subject_keeps_best_reading():
    text = "0178 ENGLISH LANGUAGE (C)\n0178 ENGLISH LANGUAGE C"
    rows, _ = parse_certificate_text(text)
    assert len(rows) == 1
    assert rows[0].confidence == 1.0
