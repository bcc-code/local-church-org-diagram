import os
from collections.abc import Iterator
from pathlib import Path

import psycopg2
import psycopg2.extensions
import pytest
from psycopg2.extras import RealDictCursor

os.environ.setdefault("DEMO_MODE", "1")

SCHEMA_SQL = Path(__file__).parent / "schema.sql"


@pytest.fixture(scope="session")
def _db_schema() -> str:
    """Drop and recreate the tables in TEST_DATABASE_URL once per session."""
    url = os.environ.get("TEST_DATABASE_URL")
    if not url:
        pytest.skip("TEST_DATABASE_URL not set")
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
