from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field
from langchain_text_splitters import RecursiveCharacterTextSplitter

router = APIRouter()


class SplitRequest(BaseModel):
    text: str = Field(..., min_length=1, description="Full document text to split")
    chunk_size: int = Field(default=1000, ge=100, le=8000)
    chunk_overlap: int = Field(default=200, ge=0, le=2000)


class SplitResponse(BaseModel):
    chunks: list[str]
    chunk_count: int


@router.post("/split", response_model=SplitResponse)
def split_text(request: SplitRequest) -> SplitResponse:
    """Split plain text into overlapping chunks for embedding/retrieval."""
    try:
        splitter = RecursiveCharacterTextSplitter(
            chunk_size=request.chunk_size,
            chunk_overlap=request.chunk_overlap,
        )
        chunks = splitter.split_text(request.text)
        return SplitResponse(chunks=chunks, chunk_count=len(chunks))
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Unable to split text: {exc}") from exc
