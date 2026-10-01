# LDF Recruitment – Web-Based Recruitment and AI-Assisted Preliminary Eligibility Assessment

Final-year project (Botho University, Mpho Khama). Implements the design in the project proposal,
literature review and Chapter 3 (Methodology).

```
Browser ──► ASP.NET Core MVC (C#, .NET 8) ──► SQL Server LocalDB
               │  Identity + roles, rules engine, written test, officer review
               └── HTTP ──► Python FastAPI service (/ai-service)
                              Tesseract OCR → spaCy NLP + fuzzy matching → subject/grade + confidence
```

| Folder | What it is |
|---|---|
| `LDFRecruitment/` | ASP.NET Core MVC web app (presentation + application + data layers) |
| `LDFRecruitment.Tests/` | xUnit tests for the rule-based eligibility engine |
| `ai-service/` | Python FastAPI OCR/NLP certificate-extraction API |

## Requirements covered

| Requirement | Where |
|---|---|
| FR1 register / login, roles | `AccountController`, ASP.NET Core Identity (Applicant, RecruitmentOfficer, Administrator) |
| FR2–FR5 application, details, uploads, validation | `ApplicationController`, `FileStorageService` |
| FR6 rule-based checks | `Services/RuleBasedEligibilityService.cs` (+ unit tests) |
| FR6a/b OCR + NLP extraction, confidence, low-confidence flag | `ai-service/app/parser.py`, `CertificateExtractionService`, `SubjectGrade.NeedsReview` |
| FR7/7a/7b written test, auto-grading, one attempt | `TestController`, unique index on `TestResult.ApplicationId` |
| FR9–FR11 officer review | `OfficerController` (verify/edit grades, decide status) |
| FR12 status tracking | `Application/Index` + `ApplicationStatusHistory` |
| FR13 admin | `AdminController` (users, question bank) |

## Run it (Windows, Visual Studio 2022)

**1. Prerequisites:** Visual Studio 2022 (ASP.NET workload), .NET 8 SDK, SQL Server LocalDB, Python 3.11+,
[Tesseract for Windows](https://github.com/UB-Mannheim/tesseract/wiki) (add it to PATH).

**2. Start the AI service** (PowerShell):
```powershell
cd ai-service
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --port 8001
```
Check http://localhost:8001/health – it should show `"status":"ok"`. Run its tests with `pytest`.

**3. Start the web app:** open `LDFRecruitment.sln`, set `LDFRecruitment` as startup project, press F5.
The database is created automatically on first run (`EnsureCreated`) and seeded with:

| Account | Password |
|---|---|
| `officer@ldf.test` | `Officer@1234` |
| `admin@ldf.test` | `Admin@1234` |

Applicants register themselves. **Change the seeded passwords** (appsettings / user-secrets) outside a demo.

**4. Run unit tests:** Test → Run All Tests.

If the AI service is not running the app still works: the officer simply sees a notice and enters grades manually.

## Configuration (`LDFRecruitment/appsettings.json`)

* `Eligibility` – age range, citizenship, minimum credits. **These are placeholders: confirm the real LDF
  requirements from your interviews / the official recruitment notice and update them.**
* `Extraction:ConfidenceThreshold` – grades below this confidence are flagged for officer verification.
* `WrittenTest:QuestionCount` – fixed test length.

## Known limitations / next steps

* Uses `EnsureCreated`; for versioned schema changes switch to EF migrations (`Add-Migration Initial`).
* The certificate parser is tuned to the row layout `CODE SUBJECT (GRADE)`. Adjust `ai-service/app/parser.py`
  against your real certificate samples (Methodology 3.13.3) and record the accuracy figures for section 3.17.
* The sample question bank is small; extend and review it (Methodology 3.7.5).
* No email notifications and no test timer yet.
