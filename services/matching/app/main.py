"""FastAPI entry point.

Development (reachable from a Quest on the same LAN):
    uv run uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
"""

from fastapi import FastAPI

from app import __version__
from app.api.routes import router


def create_app() -> FastAPI:
    application = FastAPI(title="Suolingo matching service", version=__version__)
    application.include_router(router)
    return application


app = create_app()
