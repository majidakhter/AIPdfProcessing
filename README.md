# AIPdfProcessing — Hybrid C# / Python PDF RAG

Owned by **[majidakhter](https://github.com/majidakhter)** — https://github.com/majidakhter/AIPdfProcessing

A Retrieval-Augmented Generation (RAG) demo that combines:

- **Python / FastAPI** (repo root) — LangChain `POST /split`, plus optional Python `/ingest` and `/ask`
- **C# / ASP.NET Core** (`dotnet/AICSPdfProcessing`) — PDF load with PdfPig, calls Python `/split`, OpenAI embeddings, Chroma storage/query, `/ingest` and `/ask`

## Architecture

```text
                 C# INGEST (dotnet/AICSPdfProcessing)

┌─────────────────┐     POST /split      ┌──────────────────────────┐
│ Climate.pdf     │ ───────────────────► │ Python FastAPI :8001     │
│ PdfPig load     │                      │ RecursiveCharacterText   │
└────────┬────────┘ ◄─────────────────── │ Splitter → chunks[]      │
         │            chunks JSON        └──────────────────────────┘
         ▼
┌─────────────────┐
│ OpenAI embeddings│
└────────┬────────┘
         ▼
┌─────────────────┐
│ ChromaDB :8000  │
└─────────────────┘


                 C# ASK

Embed question → Chroma similarity search → gpt-4o-mini grounded answer
```

## Repository layout

```text
AIPdfProcessing/
├── README.md
├── main.py                 # FastAPI app
├── split.py                # POST /split (used by C#)
├── ingest.py               # POST /ingest (Python-only)
├── chat.py                 # POST /ask (Python-only)
├── requirements.txt
├── Climate.pdf
└── dotnet/
    └── AICSPdfProcessing/  # .NET 8 Web API
        ├── Program.cs
        ├── IngestEndpoints.cs
        ├── ChatEndpoints.cs
        ├── AskRequest.cs
        └── appsettings.json
```

## Prerequisites

- Python 3.10+
- .NET 8 SDK
- OpenAI API key
- ChromaDB on port **8000**

## Configuration

Python `.env` (do not commit):

```env
OPENAI_API_KEY=your_openai_api_key
```

C# — same key via environment variable. `dotnet/AICSPdfProcessing/appsettings.json`:

```json
"PythonSplitter": {
  "BaseUrl": "http://localhost:8001",
  "ChunkSize": 1000,
  "ChunkOverlap": 200
}
```

## How to run (hybrid)

### 1. Chroma (port 8000)

```bash
docker run --rm -p 8000:8000 chromadb/chroma
```

### 2. Python FastAPI (port 8001)

```bash
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8001
```

Docs: http://127.0.0.1:8001/docs

### 3. C# API

```bash
cd dotnet/AICSPdfProcessing
dotnet run
```

Then call C# `POST /ingest`, then `POST /ask`:

```json
{
  "prompt": "What are the main climate risks discussed?",
  "topK": 4
}
```

## Python-only endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/split` | Split text (used by C#) |
| POST | `/ingest` | Python ingest into `./chroma_db` |
| POST | `/ask` | Python Q&A |

## Notes

- `IngestEndpointsOriginal.cs` is a backup of the pure-C# splitter and is excluded from the build.
- Keep `.env` / API keys out of git.
- C# uses Chroma on **8000** and Python on **8001** so ports do not clash.
