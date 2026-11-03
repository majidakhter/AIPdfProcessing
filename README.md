# AIPdfProcessing (Python)

FastAPI service used by the hybrid **AIProject** (C# + Python).

## Role in the hybrid app

The C# project `AICSPdfProcessing` calls:

```http
POST /split
```

to chunk PDF text with LangChain’s `RecursiveCharacterTextSplitter`, then embeds and stores vectors in Chroma from C#.

Run this service on **port 8001**:

```bash
uvicorn main:app --reload --port 8001
```

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/split` | Split text into chunks (used by C#) |
| POST | `/ingest` | Full Python ingest of `Climate.pdf` into `./chroma_db` |
| POST | `/ask` | Python-side Q&A against local Chroma |

## Setup

```bash
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Create `.env` (not committed):

```env
OPENAI_API_KEY=your_openai_api_key
```

See the root [AIProject README](../../README.md) for the full C# + Python run guide.

## Author

[majidakhter](https://github.com/majidakhter)
