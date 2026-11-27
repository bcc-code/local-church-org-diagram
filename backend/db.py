"""Postgres access via psycopg 3 (replaces supabase-py).

Connects to Supabase Postgres through the Supavisor transaction pooler, so no
session state (SET, prepared statements, temp tables, advisory locks) may be
relied upon across transactions. ``prepare_threshold=None`` is therefore
required: psycopg 3 would otherwise prepare statements server-side.

One pooled connection is checked out per Flask app context (stored on
``flask.g``) and returned to the pool on teardown. The pool is created lazily
per process so gunicorn forked workers never share sockets. Gunicorn sync
workers serve one request at a time, so ``DB_POOL_MAX`` defaults to 1
connection per worker; raise it only for threaded workers.

Connections are checked on checkout (``ConnectionPool.check_connection``) so a
connection killed by a Supavisor restart or idle timeout is replaced instead
of failing the request.

``uuid`` columns are loaded as ``str`` (see ``configure_connection``), matching
psycopg2/PostgREST and the BCC API, which all represent person UIDs as strings.
"""

import logging
import os
import threading
from collections.abc import Generator, Mapping
from contextlib import contextmanager
from datetime import date, datetime
from decimal import Decimal
from typing import Any, LiteralString, cast
from uuid import UUID

import psycopg
from flask import Flask, current_app, g
from psycopg.rows import DictRow, dict_row
from psycopg.types.string import TextLoader
from psycopg_pool import ConnectionPool

logger = logging.getLogger("db")

Connection = psycopg.Connection[DictRow]
Row = dict[str, Any]
TenantId = int | None

_G_CONN = "_db_conn"

_pool: ConnectionPool[Connection] | None = None
_pool_pid: int | None = None
_pool_lock = threading.Lock()


def init_app(app: Flask) -> None:
    """Validate config and register per-request connection teardown."""
    if not app.config.get("DATABASE_URL"):
        raise RuntimeError("DATABASE_URL is not configured")
    app.teardown_appcontext(_teardown)


def _check_connection(conn: Connection) -> None:
    """Pool checkout probe: raises on a dead connection so the pool replaces it."""
    # check_connection is typed for tuple-row connections only.
    ConnectionPool.check_connection(cast(psycopg.Connection[Any], conn))


def configure_connection(conn: Connection) -> None:
    """Load ``uuid`` as ``str`` so UIDs compare equal to BCC API strings."""
    conn.adapters.register_loader("uuid", TextLoader)


def _get_pool() -> ConnectionPool[Connection]:
    global _pool, _pool_pid
    pid = os.getpid()
    if _pool is not None and _pool_pid == pid:
        return _pool
    with _pool_lock:
        if _pool is None or _pool_pid != pid:
            # A pool inherited from a parent process is dropped, not closed,
            # since closing would tear down the parent's sockets.
            _pool = ConnectionPool(
                current_app.config["DATABASE_URL"],
                connection_class=Connection,
                kwargs={
                    "row_factory": dict_row,
                    "prepare_threshold": None,
                    "autocommit": False,
                    "application_name": "org-diagram",
                    "connect_timeout": 5,
                    "keepalives": 1,
                    "keepalives_idle": 30,
                },
                min_size=1,
                max_size=int(os.environ.get("DB_POOL_MAX", 1)),
                timeout=10,
                configure=configure_connection,
                check=_check_connection,
                name="org-diagram",
                open=True,
            )
            _pool_pid = pid
        return _pool


def get_conn() -> Connection:
    """Return this app context's pooled connection (not autocommit)."""
    conn: Connection | None = g.get(_G_CONN)
    if conn is None:
        conn = _get_pool().getconn()
        setattr(g, _G_CONN, conn)
    return conn


@contextmanager
def transaction() -> Generator[Connection]:
    """Yield the app-context connection; commit on success, roll back on error.

    Use this for reads as well as writes, and keep the block free of slow
    external calls (e.g. the BCC API) so the pooler connection is released
    as soon as the SQL is done.
    """
    conn = get_conn()
    try:
        yield conn
    except BaseException:
        if not conn.closed:
            try:
                conn.rollback()
            except psycopg.Error:
                logger.warning("Rollback failed", exc_info=True)
        raise
    else:
        conn.commit()


def _teardown(exc: BaseException | None) -> None:
    conn: Connection | None = g.pop(_G_CONN, None)
    if conn is None:
        return
    pool = _pool
    if pool is None or _pool_pid != os.getpid():
        conn.close()
        return
    # Roll back leftover work ourselves (the pool would too, but warns);
    # closed/broken connections are discarded by putconn.
    if not conn.closed and conn.info.transaction_status != (
        psycopg.pq.TransactionStatus.IDLE
    ):
        try:
            conn.rollback()
        except psycopg.Error:
            logger.warning("Rollback on teardown failed", exc_info=True)
    try:
        pool.putconn(conn)
    except Exception:
        logger.warning("Failed to return connection to pool", exc_info=True)
        conn.close()


def tenant_clause(
    col: LiteralString, tenant_id: TenantId
) -> tuple[LiteralString, list[Any]]:
    """SQL filter on a tenant column. ``col`` must be a trusted literal."""
    if tenant_id is not None:
        return f"{col} = %s", [tenant_id]
    return f"{col} IS NULL", []


def to_json(row: Mapping[str, Any] | None) -> Row | None:
    """Copy a row into a JSON-serialisable dict."""
    if row is None:
        return None
    out: dict[str, Any] = {}
    for key, value in row.items():
        if isinstance(value, (datetime, date)):
            out[key] = value.isoformat()
        elif isinstance(value, (UUID, Decimal)):
            out[key] = str(value)
        else:
            out[key] = value
    return out
