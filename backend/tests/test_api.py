import pytest
from app import app as flask_app


@pytest.fixture()
def client():
    flask_app.config["TESTING"] = True
    with flask_app.test_client() as test_client:
        yield test_client


def test_index_redirects_to_login_when_unauthenticated(client):
    response = client.get("/")
    assert response.status_code == 302
    assert "/login" in response.headers["Location"]


def test_tree_endpoint_returns_demo_data_once_logged_in(client):
    client.get("/login")
    response = client.get("/api/tree")
    assert response.status_code == 200
    assert isinstance(response.get_json(), list)


def test_api_requires_authentication(client):
    response = client.get("/api/tree")
    assert response.status_code == 401
