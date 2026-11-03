# AIPdfProcessing

FastAPI app that turns a PDF into a searchable vector store, then answers questions using retrieved chunks and OpenAI.

It currently ingests `Climate.pdf`, embeds the text with OpenAI, stores vectors in a local Chroma database, and exposes a simple ask endpoint for document-grounded Q&A.

## Features

- PDF ingest with LangChain (`PyPDFLoader` + chunking)
- OpenAI embeddings stored in Chroma (`./chroma_db`)
- `/ask` endpoint that retrieves top matching chunks and answers with `gpt-4o-mini`
- Responses stay grounded in the retrieved document context

## Project layout

| File | Purpose |
|------|---------|
| `main.py` | FastAPI app entrypoint |
| `ingest.py` | `POST /ingest` — load, split, embed, and persist the PDF |
| `chat.py` | `POST /ask` — retrieve context and generate an answer |
| `Climate.pdf` | Sample source document |
| `requirements.txt` | Python dependencies |

## Prerequisites

- Python 3.10+
- An OpenAI API key

## Setup

```bash
python -m venv .venv
# Windows
.venv\Scripts\activate
# macOS / Linux
source .venv/bin/activate

pip install -r requirements.txt
```

Create a local `.env` file (do not commit it):

```env
OPENAI_API_KEY=your_openai_api_key
```

## Run

```bash
uvicorn main:app --reload
```

API docs: [http://127.0.0.1:8000/docs](http://127.0.0.1:8000/docs)

## API

### 1. Ingest the PDF

```http
POST /ingest
```

Loads `Climate.pdf`, splits it into overlapping chunks, creates embeddings, and writes them to `./chroma_db`.

Example:

```bash
curl -X POST http://127.0.0.1:8000/ingest
```

### 2. Ask a question

```http
POST /ask
Content-Type: application/json
```

Body:

```json
{
  "prompt": "What are the main climate risks discussed?",
  "top_k": 4
}
```

- `prompt` (required): question to answer from the ingested PDF
- `top_k` (optional, 1–10, default `4`): number of chunks to retrieve

Example:

```bash
curl -X POST http://127.0.0.1:8000/ask ^
  -H "Content-Type: application/json" ^
  -d "{\"prompt\":\"Summarize the key points\",\"top_k\":4}"
```

Typical response shape:

```json
{
  "prompt": "Summarize the key points",
  "answer": "...",
  "sources": [{ "...": "chunk metadata" }]
}
```

Run `/ingest` at least once before calling `/ask`.

## Notes

- Keep secrets in `.env`; that file is gitignored.
- The sample workflow is fixed to `Climate.pdf`. To use another PDF, update the path in `ingest.py`.
- Re-running `/ingest` rebuilds/refreshes the local Chroma store under `./chroma_db`.
