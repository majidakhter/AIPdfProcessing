import os
from fastapi import APIRouter
from langchain_community.document_loaders import PyPDFLoader
from langchain_chroma import Chroma
from langchain_openai import OpenAIEmbeddings
from langchain_text_splitters import RecursiveCharacterTextSplitter

router = APIRouter()
CHROMA_DIRECTORY = "./chroma_db"

@router.post("/ingest")
def ingest():
    """Load, split, embed, and persist Climate.pdf for later retrieval."""
    try:
        document = load_document()
        chunks = split_document(document)
        embeddings = create_embeddings()
        store_embeddings_in_chroma(chunks, embeddings)
        return {"message": "Successfully ingested document chunks into vector store"}
    except Exception as exc:
        return {"error": str(exc)}

def load_document():
    """Load the source PDF and return its pages as LangChain documents."""
    return PyPDFLoader("Climate.pdf").load()

def split_document(document):
    """Split pages into searchable chunks with overlap for context continuity."""
    text_splitter = RecursiveCharacterTextSplitter(chunk_size=1000, chunk_overlap=200)
    return text_splitter.split_documents(document)

def create_embeddings():
    """Create the OpenAI embedding function used by Chroma."""
    return OpenAIEmbeddings(api_key=os.getenv("OPENAI_API_KEY"))

def store_embeddings_in_chroma(chunks, embeddings):
    """Write document chunks and their vectors to the local Chroma database."""
    vector_store = Chroma.from_documents(
        documents=chunks,
        embedding=embeddings,
        persist_directory=CHROMA_DIRECTORY,
    )
    vector_store.persist()