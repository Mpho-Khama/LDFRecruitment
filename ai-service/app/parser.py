"""NLP information extraction from raw OCR text (Methodology 3.12.3 and 3.12.4).

Pipeline per line of OCR text:
  1. spaCy tokenises the line and a rule-based Matcher finds rows that start with a syllabus-code-like token.
  2. The leading alphabetic tokens after the code form the (noisy) subject name, which is corrected against
     the known subject list with fuzzy string matching (rapidfuzz).
  3. The grade is recovered from the remaining characters. OCR often corrupts the parenthesised grade symbol,
     so several recovery strategies are tried, each with a different reliability.
  4. confidence = subject-match closeness x grade-recovery reliability.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, asdict

import spacy
from rapidfuzz import fuzz, process
from spacy.matcher import Matcher

from .subjects import KNOWN_SUBJECTS

MIN_SUBJECT_SCORE = 60  # below this the row is not treated as a known subject

_nlp = spacy.blank("en")
_matcher = Matcher(_nlp.vocab)
# Row start: a syllabus-code-like token (3-5 characters of digits, tolerating OCR confusion of 0/O and 1/I/l).
_matcher.add("ROW_START", [[{"TEXT": {"REGEX": r"^[0-9OIl]{3,5}[A-Z]?$"}, "IS_SENT_START": True}]])

_GRADE = r"(A\*|[A-GU]|[1-9])"
_RE_PAREN = re.compile(rf"[\(\[\{{]\s*{_GRADE}\s*[\)\]\}}]", re.IGNORECASE)
_RE_STANDALONE = re.compile(rf"(?:^|\s){_GRADE}\s*$", re.IGNORECASE)
_RE_TRAILING = re.compile(rf"{_GRADE}\W*$", re.IGNORECASE)

# Reliability of the position from which the grade was recovered.
RELIABILITY_PAREN = 1.0
RELIABILITY_STANDALONE = 0.85
RELIABILITY_TRAILING = 0.6


@dataclass
class Extraction:
    subject: str
    grade: str
    confidence: float
    raw_line: str

    def to_dict(self) -> dict:
        return asdict(self)


def _clean_line(line: str) -> str:
    # Drop leading table borders / bullets that OCR commonly invents.
    return re.sub(r"^[^0-9A-Za-z]+", "", line).strip()


def _split_row(line: str):
    """Return (code, subject_text, remainder) if the line is a candidate result row, else None."""
    doc = _nlp(line)
    if not _matcher(doc):
        return None

    tokens = list(doc)
    i = 1  # tokens[0] is the code
    subject_tokens: list[str] = []
    while i < len(tokens):
        t = tokens[i]
        if t.is_space:
            i += 1
            continue
        # Subject words are alphabetic tokens of 2+ letters, plus '&' and a small set of joiners.
        if (t.is_alpha and len(t.text) >= 2) or t.text in {"&", "-"}:
            subject_tokens.append(t.text)
            i += 1
        else:
            break
    remainder = doc[i:].text if i < len(tokens) else ""
    # A single-letter grade right after the subject was stopped by the length rule, so it is in `remainder`.
    return tokens[0].text, " ".join(subject_tokens), remainder.strip()


def _recover_grade(remainder: str):
    """Return (grade, reliability) or None."""
    if not remainder:
        return None
    m = _RE_PAREN.search(remainder)
    if m:
        return m.group(1).upper(), RELIABILITY_PAREN
    m = _RE_STANDALONE.search(remainder)
    if m:
        return m.group(1).upper(), RELIABILITY_STANDALONE
    m = _RE_TRAILING.search(remainder)
    if m:
        return m.group(1).upper(), RELIABILITY_TRAILING
    return None


def _match_subject(text: str):
    if not text:
        return None
    best = process.extractOne(text.upper(), KNOWN_SUBJECTS, scorer=fuzz.ratio)
    if best is None:
        return None
    name, score, _ = best
    if score < MIN_SUBJECT_SCORE:
        return None
    return name, score / 100.0


def parse_certificate_text(text: str) -> tuple[list[Extraction], list[str]]:
    """Parse raw OCR text into subject/grade pairs with confidence scores."""
    results: dict[str, Extraction] = {}
    warnings: list[str] = []

    for raw in text.splitlines():
        line = _clean_line(raw)
        if len(line) < 6:
            continue
        row = _split_row(line)
        if row is None:
            continue
        _code, subject_text, remainder = row

        subject = _match_subject(subject_text)
        if subject is None:
            continue
        grade = _recover_grade(remainder)
        if grade is None:
            warnings.append(f"Grade not found for '{subject[0]}'.")
            continue

        name, subject_score = subject
        confidence = round(subject_score * grade[1], 3)
        candidate = Extraction(name, grade[0], confidence, raw.strip())
        # Keep the best reading if a subject appears twice (e.g. a header repeated on page 2).
        if name not in results or candidate.confidence > results[name].confidence:
            results[name] = candidate

    if not results:
        warnings.append("No subject/grade rows were recognised in the certificate text.")
    return list(results.values()), warnings
