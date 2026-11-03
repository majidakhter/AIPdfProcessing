from dotenv import load_dotenv

load_dotenv()
from fastapi import FastAPI

from chat import router as chat_router
from ingest import router as ingest_router
from split import router as split_router

app = FastAPI()
app.include_router(chat_router)
app.include_router(ingest_router)
app.include_router(split_router)
