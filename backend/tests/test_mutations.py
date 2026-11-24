from contextlib import contextmanager

import db
import mutations
import pytest
from admin import admin_bp
from factories import OTHER_TENANT, TENANT, get_member, make_group, make_member
from flask import Flask
from psycopg2.errors import UniqueViolation


def _sort_orders(conn, ids):
    with conn.cursor() as cur:
        cur.execute("SELECT id, sort_order FROM groups WHERE id = ANY(%s)", [ids])
        return {r["id"]: r["sort_order"] for r in cur.fetchall()}


# --- add_membership ---------------------------------------------------------


def test_add_membership_returns_row(db_conn):
    gid = make_group(db_conn, "A")
    row = mutations.add_membership(db_conn, gid, "uid-1", TENANT)
    assert row is not None
    assert row["group_id"] == gid
    assert row["bcc_person_uid"] == "uid-1"
    assert row["tenant_id"] == TENANT
    assert row["title"] is None
    assert row["id"] is not None


def test_add_membership_with_title(db_conn):
    gid = make_group(db_conn, "A")
    row = mutations.add_membership(db_conn, gid, "uid-1", TENANT, title="Leader")
    assert row is not None
    assert row["title"] == "Leader"


def test_add_membership_takes_tenant_from_group(db_conn):
    gid = make_group(db_conn, "A")
    row = mutations.add_membership(db_conn, gid, "uid-1", TENANT)
    assert row is not None
    assert row["tenant_id"] == TENANT


def test_add_membership_other_tenants_group_is_rejected(db_conn):
    theirs = make_group(db_conn, "Theirs", tenant_id=OTHER_TENANT)
    assert mutations.add_membership(db_conn, theirs, "uid-1", TENANT) is None
    assert get_member(db_conn, theirs, "uid-1") is None


def test_add_membership_null_tenant_cannot_use_tenant_group(db_conn):
    gid = make_group(db_conn, "A")
    assert mutations.add_membership(db_conn, gid, "uid-1", None) is None


def test_add_membership_unknown_group_returns_none(db_conn):
    gid = make_group(db_conn, "A")
    assert mutations.add_membership(db_conn, gid + 10_000, "uid-1", TENANT) is None


def test_add_membership_duplicate_raises_unique_violation(db_conn):
    gid = make_group(db_conn, "A")
    mutations.add_membership(db_conn, gid, "uid-1", TENANT)
    with pytest.raises(UniqueViolation):
        mutations.add_membership(db_conn, gid, "uid-1", TENANT)


# --- remove_membership ------------------------------------------------------


def test_remove_membership_returns_deleted_row(db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1")
    row = mutations.remove_membership(db_conn, gid, "uid-1", TENANT)
    assert row is not None
    assert row["bcc_person_uid"] == "uid-1"
    assert get_member(db_conn, gid, "uid-1") is None


def test_remove_membership_not_found(db_conn):
    gid = make_group(db_conn, "A")
    assert mutations.remove_membership(db_conn, gid, "nobody", TENANT) is None


def test_remove_membership_other_tenant_is_untouched(db_conn):
    gid = make_group(db_conn, "A", tenant_id=OTHER_TENANT)
    make_member(db_conn, gid, "uid-1", tenant_id=OTHER_TENANT)
    assert mutations.remove_membership(db_conn, gid, "uid-1", TENANT) is None
    assert mutations.remove_membership(db_conn, gid, "uid-1", None) is None
    assert get_member(db_conn, gid, "uid-1") is not None


def test_remove_membership_null_tenant_matches_null(db_conn):
    gid = make_group(db_conn, "A", tenant_id=None)
    make_member(db_conn, gid, "uid-1", tenant_id=None)
    assert mutations.remove_membership(db_conn, gid, "uid-1", None) is not None


# --- update_membership ------------------------------------------------------


def test_update_membership_sets_fields(db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1", title="Old")
    row = mutations.update_membership(
        db_conn, gid, "uid-1", TENANT, {"title": "New", "link": "https://x"}
    )
    assert row is not None
    assert row["title"] == "New"
    assert row["link"] == "https://x"


def test_update_membership_partial_keeps_other_fields(db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1", title="Keep")
    row = mutations.update_membership(
        db_conn, gid, "uid-1", TENANT, {"link": "https://x"}
    )
    assert row is not None
    assert row["title"] == "Keep"


def test_update_membership_not_found(db_conn):
    gid = make_group(db_conn, "A")
    assert (
        mutations.update_membership(db_conn, gid, "nobody", TENANT, {"title": "x"})
        is None
    )


def test_update_membership_other_tenant_is_untouched(db_conn):
    gid = make_group(db_conn, "A", tenant_id=OTHER_TENANT)
    make_member(db_conn, gid, "uid-1", tenant_id=OTHER_TENANT, title="Orig")
    assert (
        mutations.update_membership(db_conn, gid, "uid-1", TENANT, {"title": "Hack"})
        is None
    )
    member = get_member(db_conn, gid, "uid-1")
    assert member is not None
    assert member["title"] == "Orig"


def test_update_membership_rejects_unknown_column(db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1")
    with pytest.raises(ValueError):
        mutations.update_membership(db_conn, gid, "uid-1", TENANT, {"tenant_id": 1})
    with pytest.raises(ValueError):
        mutations.update_membership(db_conn, gid, "uid-1", TENANT, {})


# --- update_sort_order ------------------------------------------------------


def test_update_sort_order_success(db_conn):
    a = make_group(db_conn, "A", sort_order=0)
    b = make_group(db_conn, "B", sort_order=1)
    mutations.update_sort_order(
        db_conn,
        [(a, 1), (b, 0)],
        TENANT,
    )
    assert _sort_orders(db_conn, [a, b]) == {a: 1, b: 0}


def test_update_sort_order_missing_id_changes_nothing(db_conn):
    a = make_group(db_conn, "A", sort_order=0)
    b = make_group(db_conn, "B", sort_order=1)
    missing = b + 10_000
    with db_conn.cursor() as cur:
        cur.execute("SAVEPOINT before_sort")
    with pytest.raises(mutations.GroupsNotFound) as exc:
        mutations.update_sort_order(
            db_conn,
            [
                (a, 5),
                (missing, 6),
                (b, 7),
            ],
            TENANT,
        )
    assert exc.value.missing_ids == [missing]
    assert str(missing) in str(exc.value)
    with db_conn.cursor() as cur:
        cur.execute("ROLLBACK TO SAVEPOINT before_sort")
    assert _sort_orders(db_conn, [a, b]) == {a: 0, b: 1}


def test_update_sort_order_does_not_touch_other_tenant(db_conn):
    mine = make_group(db_conn, "Mine", sort_order=0)
    theirs = make_group(db_conn, "Theirs", tenant_id=OTHER_TENANT, sort_order=0)
    with pytest.raises(mutations.GroupsNotFound) as exc:
        mutations.update_sort_order(
            db_conn,
            [
                (mine, 3),
                (theirs, 3),
            ],
            TENANT,
        )
    assert exc.value.missing_ids == [theirs]
    assert _sort_orders(db_conn, [theirs]) == {theirs: 0}


def test_update_sort_order_null_tenant_scoped_to_null(db_conn):
    null_group = make_group(db_conn, "Null", tenant_id=None, sort_order=0)
    tenant_group = make_group(db_conn, "T", sort_order=0)
    mutations.update_sort_order(db_conn, [(null_group, 4)], None)
    assert _sort_orders(db_conn, [null_group]) == {null_group: 4}
    with pytest.raises(mutations.GroupsNotFound):
        mutations.update_sort_order(db_conn, [(tenant_group, 4)], None)


# --- Flask routes (live mode, against db_conn) ------------------------------


@pytest.fixture()
def admin_client(db_conn, monkeypatch):
    @contextmanager
    def fake_transaction():
        # Savepoint instead of commit so the test's outer rollback still cleans up.
        with db_conn.cursor() as cur:
            cur.execute("SAVEPOINT route_tx")
        try:
            yield db_conn
        except Exception:
            with db_conn.cursor() as cur:
                cur.execute("ROLLBACK TO SAVEPOINT route_tx")
            raise
        with db_conn.cursor() as cur:
            cur.execute("RELEASE SAVEPOINT route_tx")

    monkeypatch.setattr(db, "transaction", fake_transaction)

    app = Flask("admin-test")
    app.secret_key = "test"
    app.config["DEMO_MODE"] = False
    app.config["TESTING"] = True
    app.register_blueprint(admin_bp)
    with app.test_client() as client:
        with client.session_transaction() as sess:
            sess["user"] = {"churchId": TENANT}
        yield client


def test_route_add_member(admin_client, db_conn):
    gid = make_group(db_conn, "A")
    resp = admin_client.post(
        "/api/group-membership", json={"group_id": gid, "person_uid": "uid-1"}
    )
    assert resp.status_code == 201
    body = resp.get_json()
    assert body["success"] is True
    assert body["data"]["bcc_person_uid"] == "uid-1"
    assert isinstance(body["data"]["created_at"], str)


def test_route_add_member_duplicate_is_409(admin_client, db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1")
    resp = admin_client.post(
        "/api/group-membership", json={"group_id": gid, "person_uid": "uid-1"}
    )
    assert resp.status_code == 409
    assert resp.get_json() == {"error": "Member already in group"}


def test_route_remove_member(admin_client, db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1")
    resp = admin_client.delete(
        "/api/group-membership", json={"group_id": gid, "person_uid": "uid-1"}
    )
    assert resp.status_code == 200
    assert resp.get_json()["data"]["bcc_person_uid"] == "uid-1"
    resp = admin_client.delete(
        "/api/group-membership", json={"group_id": gid, "person_uid": "uid-1"}
    )
    assert resp.status_code == 404


def test_route_update_member(admin_client, db_conn):
    gid = make_group(db_conn, "A")
    make_member(db_conn, gid, "uid-1")
    resp = admin_client.put(
        "/api/group-membership",
        json={"group_id": gid, "person_uid": "uid-1", "title": "Boss"},
    )
    assert resp.status_code == 200
    assert resp.get_json()["data"]["title"] == "Boss"


def test_route_sort_order_missing_is_404_and_atomic(admin_client, db_conn):
    a = make_group(db_conn, "A", sort_order=0)
    missing = a + 10_000
    resp = admin_client.put(
        "/api/groups/sort-order",
        json={
            "updates": [
                {"group_id": a, "sort_order": 9},
                {"group_id": missing, "sort_order": 1},
            ]
        },
    )
    assert resp.status_code == 404
    assert str(missing) in resp.get_json()["error"]
    assert _sort_orders(db_conn, [a]) == {a: 0}


def test_route_sort_order_success(admin_client, db_conn):
    a = make_group(db_conn, "A", sort_order=0)
    b = make_group(db_conn, "B", sort_order=1)
    resp = admin_client.put(
        "/api/groups/sort-order",
        json={
            "updates": [
                {"group_id": a, "sort_order": 1},
                {"group_id": b, "sort_order": 0},
            ]
        },
    )
    assert resp.status_code == 200
    assert resp.get_json() == {"success": True}
    assert _sort_orders(db_conn, [a, b]) == {a: 1, b: 0}


def test_route_add_member_other_tenants_group_is_404(admin_client, db_conn):
    theirs = make_group(db_conn, "Theirs", tenant_id=OTHER_TENANT)
    resp = admin_client.post(
        "/api/group-membership", json={"group_id": theirs, "person_uid": "uid-1"}
    )
    assert resp.status_code == 404
    assert get_member(db_conn, theirs, "uid-1") is None


@pytest.mark.parametrize(
    "payload",
    [
        {"group_id": "abc", "person_uid": "uid-1"},
        {"group_id": True, "person_uid": "uid-1"},
        {"group_id": 1.5, "person_uid": "uid-1"},
        {"group_id": [1], "person_uid": "uid-1"},
        {"group_id": 2**63, "person_uid": "uid-1"},
        {"group_id": 1, "person_uid": {"x": 1}},
        {"group_id": 1, "person_uid": "uid-1", "title": 5},
    ],
)
def test_route_add_member_invalid_input_is_400(admin_client, payload):
    resp = admin_client.post("/api/group-membership", json=payload)
    assert resp.status_code == 400
    assert "error" in resp.get_json()


@pytest.mark.parametrize(
    "updates",
    [
        [{"group_id": "x", "sort_order": 1}],
        [{"group_id": 1, "sort_order": 2**31}],
        [{"group_id": 1, "sort_order": None}],
        [{"group_id": 1, "sort_order": 1}, {"group_id": 1, "sort_order": 2}],
        ["not-a-dict"],
    ],
)
def test_route_sort_order_invalid_input_is_400(admin_client, updates):
    resp = admin_client.put("/api/groups/sort-order", json={"updates": updates})
    assert resp.status_code == 400
