# AIProject — Hybrid C# / Python PDF RAG

A Retrieval-Augmented Generation (RAG) demo that combines:

- **C# / ASP.NET Core** (`AICSPdfProcessing`) — PDF load, OpenAI embeddings, Chroma storage/query, `/ingest` and `/ask` APIs  
- **Python / FastAPI** (`firstpythonproject/AIPdfProcessing`) — LangChain text splitting via `POST /split` (and a full Python ingest/ask stack for standalone use)

This repository belongs to **[majidakhter](https://github.com/majidakhter)**.

## Architecture

```text
                 C# INGEST (AICSPdfProcessing)

┌─────────────────┐     POST /split      ┌──────────────────────────┐
│ Climate.pdf     │ ───────────────────► │ Python FastAPI :8001     │
│ PdfPig load     │                      │ RecursiveCharacterText   │
└────────┬────────┘ ◄─────────────────── │ Splitter → chunks[]      │
         │            chunks JSON        └──────────────────────────┘
         ▼
┌─────────────────┐
│ OpenAI embeddings│
│ text-embedding-  │
│ 3-small          │
└────────┬────────┘
         ▼
┌─────────────────┐
│ ChromaDB :8000  │
│ collection:     │
│ climate_documents│
└─────────────────┘


                 C# ASK (AICSPdfProcessing)

┌─────────────────┐
│ POST /ask       │
│ prompt + top_k  │
└────────┬────────┘
         ▼
┌─────────────────┐     similarity      ┌─────────────────┐
│ Embed question  │ ──────────────────► │ ChromaDB        │
└────────┬────────┘                     └────────┬────────┘
         │                                       │
         └────────────── context ────────────────┘
                         ▼
                ┌─────────────────┐
                │ gpt-4o-mini     │
                │ grounded answer │
                └─────────────────┘
```

## Repository layout

```text
AIProject/
├── README.md
├── RunReadme.txt
├── AICSPdfProcessing/                 # C# /.NET 8 Web API
│   ├── Program.cs
│   ├── IngestEndpoints.cs             # PDF → Python /split → embed → Chroma
│   ├── IngestEndpointsOriginal.cs     # Backup (local C# splitter; not compiled)
│   ├── ChatEndpoints.cs               # POST /ask
│   ├── AskRequest.cs
│   ├── appsettings.json               # PythonSplitter:BaseUrl, etc.
│   └── AICSPdfProcessing.csproj
└── firstpythonproject/
    └── AIPdfProcessing/               # Python FastAPI
        ├── main.py                    # App + routers
        ├── split.py                   # POST /split (used by C#)
        ├── ingest.py                  # POST /ingest (Python-only path)
        ├── chat.py                    # POST /ask (Python-only path)
        ├── requirements.txt
        └── Climate.pdf
```

## Prerequisites

- .NET 8 SDK  
- Python 3.10+  
- OpenAI API key  
- ChromaDB server on port **8000** (C# client expects `http://localhost:8000/api/v1/`)

## Configuration

### Python

In `firstpythonproject/AIPdfProcessing/.env` (do not commit):

```env
OPENAI_API_KEY=your_openai_api_key
```

### C#

Set the same key via environment variable or user secrets / `appsettings` (prefer env var):

```env
OPENAI_API_KEY=your_openai_api_key
```

`AICSPdfProcessing/appsettings.json`:

```json
"PythonSplitter": {
  "BaseUrl": "http://localhost:8001",
  "ChunkSize": 1000,
  "ChunkOverlap": 200
}
```

## How to run (hybrid path)

### 1. Start Chroma (port 8000)

```bash
docker run --rm -p 8000:8000 chromadb/chroma
```

(Or your usual local Chroma process.)

### 2. Start Python FastAPI (port 8001)

```bash
cd firstpythonproject/AIPdfProcessing
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8001
```

Docs: http://127.0.0.1:8001/docs  

### 3. Start C# API

```bash
cd AICSPdfProcessing
dotnet run
```

Then:

1. `POST /ingest` on the C# app — loads `Climate.pdf`, calls Python `POST /split`, embeds chunks, writes to Chroma  
2. `POST /ask` on the C# app — retrieves context from Chroma and answers with `gpt-4o-mini`

Example ask body:

```json
{
  "prompt": "What are the main climate risks discussed?",
  "topK": 4
}
```

## Python-only path (optional)

The FastAPI app also exposes its own ingest/ask endpoints if you want to run without C#:

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/split` | Split plain text (used by C#) |
| POST | `/ingest` | Load `Climate.pdf`, embed, store in local `./chroma_db` |
| POST | `/ask` | Ask against the Python Chroma store |

## Technology stack

| Layer | Tech |
|-------|------|
| C# API | ASP.NET Core minimal APIs, PdfPig, OpenAI .NET SDK, ChromaDB.Client, Swashbuckle |
| Python API | FastAPI, LangChain text splitters, LangChain/OpenAI, Chroma |
| Models | `text-embedding-3-small`, `gpt-4o-mini` |
| Vector DB | ChromaDB |

## Notes

- `IngestEndpointsOriginal.cs` is a backup of the pure-C# splitter path and is excluded from the build.  
- Keep `.env` and API keys out of git.  
- C# talks to **Chroma on 8000** and **Python on 8001** so the ports do not clash.
