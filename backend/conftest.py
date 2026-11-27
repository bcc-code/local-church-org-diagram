import os
from collections.abc import Generator
from pathlib import Path

import psycopg
import pytest
from db import Connection, configure_connection
from psycopg.conninfo import conninfo_to_dict
from psycopg.rows import dict_row

os.environ.setdefault("DEMO_MODE", "1")

SCHEMA_SQL = Path(__file__).parent / "schema.sql"

_LOCAL_HOSTS = {"localhost", "127.0.0.1", "::1", ""}


def _assert_disposable(url: str) -> None:
    """Refuse to drop tables anywhere but a local or explicitly test database."""
    params = conninfo_to_dict(url)
    host = str(params.get("host") or "")
    dbname = str(params.get("dbname") or "")
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
    with psycopg.connect(url) as conn, conn.cursor() as cur:
        cur.execute(
            "DROP TABLE IF EXISTS public.group_membership, public.groups CASCADE"
        )
        cur.execute(SCHEMA_SQL.read_text().encode())
    return url


@pytest.fixture()
def db_conn(_db_schema: str) -> Generator[Connection]:
    """A dict_row connection to the test DB, rolled back after each test."""
    conn = Connection.connect(_db_schema, row_factory=dict_row)
    configure_connection(conn)
    try:
        yield conn
    finally:
        conn.rollback()
        conn.close()
