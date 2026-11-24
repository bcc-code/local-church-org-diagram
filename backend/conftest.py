import os
from collections.abc import Iterator
from pathlib import Path

import psycopg2
import psycopg2.extensions
import pytest
from psycopg2.extras import RealDictCursor

os.environ.setdefault("DEMO_MODE", "1")

SCHEMA_SQL = Path(__file__).parent / "schema.sql"

_LOCAL_HOSTS = {"localhost", "127.0.0.1", "::1", ""}


def _assert_disposable(url: str) -> None:
    """Refuse to drop tables anywhere but a local or explicitly test database."""
    params = psycopg2.extensions.parse_dsn(url)
    host = params.get("host", "")
    dbname = params.get("dbname", "")
    if host in _LOCAL_HOSTS or "test" in dbname:
        return
    pytest.exit(
        f"Refusing to run DB tests against host={host!r} dbname={dbname!r}: "
        "TEST_DATABASE_URL must point at localhost or a database named *test*, "
        "because the test setup drops tables.",
        returncode=2,
    )


@pytest.fixture(scope="session")
def _db_schema() -> str:
    """Drop and recreate the tables in TEST_DATABASE_URL once per session."""
    url = os.environ.get("TEST_DATABASE_URL")
    if not url:
        pytest.skip("TEST_DATABASE_URL not set")
    _assert_disposable(url)
    conn = psycopg2.connect(url)
    try:
        with conn, conn.cursor() as cur:
            cur.execute(
                "DROP TABLE IF EXISTS public.group_membership, public.groups CASCADE"
            )
            cur.execute(SCHEMA_SQL.read_text())
    finally:
        conn.close()
    return url


@pytest.fixture()
def db_conn(_db_schema: str) -> Iterator[psycopg2.extensions.connection]:
    """A RealDictCursor connection to the test DB, rolled back after each test."""
    conn = psycopg2.connect(_db_schema, cursor_factory=RealDictCursor)
    try:
        yield conn
    finally:
        conn.rollback()
        conn.close()
