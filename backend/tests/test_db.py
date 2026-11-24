from collections.abc import Iterator
from datetime import UTC, date, datetime
from decimal import Decimal
from typing import Any, cast
from uuid import UUID

import db
import psycopg2
import psycopg2.extensions
import pytest
from flask import Flask

TENANT = 987654321


def test_tenant_clause_with_tenant():
    assert db.tenant_clause("g.tenant_id", 42) == ("g.tenant_id = %s", [42])


def test_tenant_clause_without_tenant():
    assert db.tenant_clause("tenant_id", None) == ("tenant_id IS NULL", [])


def test_tenant_clause_zero_is_a_tenant():
    assert db.tenant_clause("tenant_id", 0) == ("tenant_id = %s", [0])


def test_to_json_none():
    assert db.to_json(None) is None


def test_to_json_converts_types():
    row = {
        "id": 1,
        "created_at": datetime(2026, 1, 2, 3, 4, 5, tzinfo=UTC),
        "day": date(2026, 1, 2),
        "uid": UUID("12345678-1234-5678-1234-567812345678"),
        "amount": Decimal("1.50"),
        "name": "x",
        "parent_id": None,
    }
    out = db.to_json(row)
    assert out == {
        "id": 1,
        "created_at": "2026-01-02T03:04:05+00:00",
        "day": "2026-01-02",
        "uid": "12345678-1234-5678-1234-567812345678",
        "amount": "1.50",
        "name": "x",
        "parent_id": None,
    }
    assert out is not row


def test_init_app_requires_database_url():
    with pytest.raises(RuntimeError):
        db.init_app(Flask("no-db"))


@pytest.fixture()
def db_app(_db_schema: str) -> Iterator[Flask]:
    app = Flask("test-db")
    app.config["DATABASE_URL"] = _db_schema
    db.init_app(app)
    yield app
    conn = psycopg2.connect(_db_schema)
    try:
        with conn, conn.cursor() as cur:
            cur.execute("DELETE FROM groups WHERE tenant_id = %s", [TENANT])
    finally:
        conn.close()
    if db._pool is not None:
        db._pool.closeall()
    db._pool = None
    db._pool_pid = None


def _count(app: Flask, name: str) -> int:
    with app.app_context():
        with db.get_conn().cursor() as cur:
            cur.execute(
                "SELECT count(*) AS n FROM groups WHERE tenant_id = %s AND name = %s",
                [TENANT, name],
            )
            row = cur.fetchone()
            assert row is not None
            return cast(dict[str, Any], row)["n"]


def test_transaction_commits(db_app: Flask):
    with db_app.app_context():
        with db.transaction() as conn, conn.cursor() as cur:
            cur.execute(
                "INSERT INTO groups (name, tenant_id) VALUES (%s, %s)",
                ["committed", TENANT],
            )
    assert _count(db_app, "committed") == 1


def test_transaction_rolls_back_and_reraises(db_app: Flask):
    with db_app.app_context():
        with pytest.raises(ValueError), db.transaction() as conn:
            with conn.cursor() as cur:
                cur.execute(
                    "INSERT INTO groups (name, tenant_id) VALUES (%s, %s)",
                    ["rolled-back", TENANT],
                )
            raise ValueError("boom")
        status = db.get_conn().get_transaction_status()
        assert status == psycopg2.extensions.TRANSACTION_STATUS_IDLE
    assert _count(db_app, "rolled-back") == 0


def test_get_conn_reused_within_app_context(db_app: Flask):
    with db_app.app_context():
        conn = db.get_conn()
        assert db.get_conn() is conn
        with db.transaction() as tx_conn:
            assert tx_conn is conn
        assert not conn.autocommit
        with conn.cursor() as cur:
            cur.execute("SELECT 1 AS one")
            assert cur.fetchone() == {"one": 1}


def test_teardown_rolls_back_uncommitted_work(db_app: Flask):
    with db_app.app_context():
        with db.get_conn().cursor() as cur:
            cur.execute(
                "INSERT INTO groups (name, tenant_id) VALUES (%s, %s)",
                ["uncommitted", TENANT],
            )
    assert _count(db_app, "uncommitted") == 0


def test_teardown_discards_closed_connection(db_app: Flask):
    with db_app.app_context():
        db.get_conn().close()
    with db_app.app_context():
        conn = db.get_conn()
        assert not conn.closed
        with conn.cursor() as cur:
            cur.execute("SELECT 1 AS one")
            assert cur.fetchone() == {"one": 1}
