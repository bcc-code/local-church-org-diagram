"""Postgres access via psycopg2 (replaces supabase-py).

Connects to Supabase Postgres through the Supavisor transaction pooler, so no
session state (SET, prepared statements, temp tables, advisory locks) may be
relied upon across transactions.

One pooled connection is checked out per Flask app context (stored on
``flask.g``) and returned to the pool on teardown. The pool is created lazily
per process so gunicorn forked workers never share sockets.
"""

import logging
import os
import threading
from collections.abc import Iterator, Mapping
from contextlib import contextmanager
from datetime import date, datetime
from decimal import Decimal
from typing import Any, cast
from uuid import UUID

import psycopg2
import psycopg2.extensions
from flask import Flask, current_app, g
from psycopg2.extras import RealDictCursor
from psycopg2.pool import PoolError, ThreadedConnectionPool

logger = logging.getLogger("db")

Connection = psycopg2.extensions.connection
Cursor = psycopg2.extensions.cursor
Row = dict[str, Any]
TenantId = int | None

_G_CONN = "_db_conn"
_G_BROKEN = "_db_conn_broken"

_pool: ThreadedConnectionPool | None = None
_pool_pid: int | None = None
_pool_lock = threading.Lock()

_DISCONNECT_ERRORS = (psycopg2.OperationalError, psycopg2.InterfaceError)


def init_app(app: Flask) -> None:
    """Validate config and register per-request connection teardown."""
    if not app.config.get("DATABASE_URL"):
        raise RuntimeError("DATABASE_URL is not configured")
    app.teardown_appcontext(_teardown)


def _get_pool() -> ThreadedConnectionPool:
    global _pool, _pool_pid
    pid = os.getpid()
    if _pool is not None and _pool_pid == pid:
        return _pool
    with _pool_lock:
        if _pool is None or _pool_pid != pid:
            # minconn=1: psycopg2 closes (rather than keeps) returned conns
            # once the pool holds >= minconn idle ones, so 0 would disable reuse.
            # A pool inherited from a parent process is dropped, not closed,
            # since closing would tear down the parent's sockets.
            _pool = ThreadedConnectionPool(
                minconn=1,
                maxconn=int(os.environ.get("DB_POOL_MAX", 2)),
                dsn=current_app.config["DATABASE_URL"],
                connect_timeout=5,
                keepalives=1,
                keepalives_idle=30,
                application_name="org-diagram",
                cursor_factory=RealDictCursor,
            )
            _pool_pid = pid
        return _pool


def get_conn() -> psycopg2.extensions.connection:
    """Return this app context's pooled connection (not autocommit)."""
    conn: psycopg2.extensions.connection | None = g.get(_G_CONN)
    if conn is None:
        pool = _get_pool()
        conn = pool.getconn()
        if conn.closed:
            pool.putconn(conn, close=True)
            conn = pool.getconn()
        setattr(g, _G_CONN, conn)
        setattr(g, _G_BROKEN, False)
    return conn


@contextmanager
def transaction() -> Iterator[psycopg2.extensions.connection]:
    """Yield the app-context connection; commit on success, roll back on error.

    Use this for reads as well as writes, and keep the block free of slow
    external calls (e.g. the BCC API) so the pooler connection is released
    as soon as the SQL is done.
    """
    conn = get_conn()
    try:
        yield conn
    except BaseException as exc:
        if isinstance(exc, _DISCONNECT_ERRORS):
            setattr(g, _G_BROKEN, True)
        if not conn.closed:
            try:
                conn.rollback()
            except _DISCONNECT_ERRORS:
                setattr(g, _G_BROKEN, True)
        raise
    else:
        try:
            conn.commit()
        except _DISCONNECT_ERRORS:
            setattr(g, _G_BROKEN, True)
            raise


def _teardown(exc: BaseException | None) -> None:
    conn: psycopg2.extensions.connection | None = g.pop(_G_CONN, None)
    broken = bool(g.pop(_G_BROKEN, False))
    if conn is None:
        return
    if isinstance(exc, _DISCONNECT_ERRORS):
        broken = True
    if not conn.closed and not broken:
        try:
            status = conn.get_transaction_status()
            if status != psycopg2.extensions.TRANSACTION_STATUS_IDLE:
                conn.rollback()
        except _DISCONNECT_ERRORS:
            broken = True
    close = broken or bool(conn.closed)
    pool = _pool
    if pool is None or _pool_pid != os.getpid():
        conn.close()
        return
    try:
        pool.putconn(conn, close=close)
    except PoolError:
        logger.warning("Failed to return connection to pool", exc_info=True)
        conn.close()


def tenant_clause(col: str, tenant_id: TenantId) -> tuple[str, list[Any]]:
    """SQL filter on a tenant column. ``col`` must be a trusted literal."""
    if tenant_id is not None:
        return f"{col} = %s", [tenant_id]
    return f"{col} IS NULL", []


def fetch_all(cur: Cursor) -> list[Row]:
    """``cur.fetchall()`` typed for RealDictCursor (the stubs say tuples)."""
    return cast(list[Row], cur.fetchall())


def fetch_one(cur: Cursor) -> Row | None:
    """``cur.fetchone()`` typed for RealDictCursor (the stubs say tuples)."""
    return cast(Row | None, cur.fetchone())


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
