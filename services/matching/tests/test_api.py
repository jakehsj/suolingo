from fastapi.testclient import TestClient

from app.main import app
from tests.factory import make_recording

client = TestClient(app)


def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ok"
    assert body["contracts"]["hand_recording"] == "0.1.0"
    assert body["evaluation"] == "not-implemented"


def test_inspect_returns_quality_report_not_evaluation():
    response = client.post("/v1/recordings/inspect", json=make_recording())
    assert response.status_code == 200
    body = response.json()
    assert body["kind"] == "input-quality-report"
    assert body["report"]["left"]["interruptions"] == 1
    assert body["report"]["left"]["tracked_ratio"] == 0.8


def test_inspect_rejects_invalid_recording():
    data = make_recording()
    data["joints"] = data["joints"][:-1]
    response = client.post("/v1/recordings/inspect", json=data)
    assert response.status_code == 422
