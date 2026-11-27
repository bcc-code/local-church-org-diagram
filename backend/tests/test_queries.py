import queries
from factories import OTHER_TENANT as T2
from factories import TENANT as T1
from factories import U1, U2, U3, U4, U5
from factories import make_group as _group
from factories import make_member as _member


def _by_id(rows):
    return {r["id"]: r for r in rows}


def test_get_tree_counts_and_empty_groups(db_conn):
    root = _group(db_conn, "Root", T1, sort_order=1)
    child = _group(db_conn, "Child", T1, parent_id=root, sort_order=2)
    empty = _group(db_conn, "Empty", T1, parent_id=root, sort_order=3)
    _member(db_conn, root, U1, T1)
    _member(db_conn, root, U2, T1)
    _member(db_conn, child, U1, T1)

    rows = _by_id(queries.get_tree(db_conn, T1))

    assert set(rows) == {root, child, empty}
    assert rows[root]["member_count"] == 2
    assert rows[child]["member_count"] == 1
    assert rows[empty]["member_count"] == 0
    assert rows[child]["parent_id"] == root
    assert rows[child]["name"] == "Child"
    assert rows[child]["sort_order"] == 2


def test_get_tree_membership_count_is_tenant_scoped(db_conn):
    g = _group(db_conn, "G", T1)
    _member(db_conn, g, U1, T1)
    _member(db_conn, g, U2, T2)  # mismatched tenant on membership
    _member(db_conn, g, U3, None)

    rows = _by_id(queries.get_tree(db_conn, T1))
    assert rows[g]["member_count"] == 1


def test_get_tree_cross_tenant_isolation(db_conn):
    g1 = _group(db_conn, "T1", T1)
    g2 = _group(db_conn, "T2", T2)
    _group(db_conn, "Null", None)

    assert set(_by_id(queries.get_tree(db_conn, T1))) == {g1}
    assert set(_by_id(queries.get_tree(db_conn, T2))) == {g2}


def test_get_tree_null_tenant(db_conn):
    g_null = _group(db_conn, "Null", None)
    _group(db_conn, "T1", T1)
    _member(db_conn, g_null, U1, None)
    _member(db_conn, g_null, U2, T1)

    rows = queries.get_tree(db_conn, None)
    assert [r["id"] for r in rows] == [g_null]
    assert rows[0]["member_count"] == 1


def test_get_tree_ordering(db_conn):
    c = _group(db_conn, "c", T1, sort_order=2)
    a = _group(db_conn, "a", T1, sort_order=1)
    n1 = _group(db_conn, "n1", T1, sort_order=None)
    b = _group(db_conn, "b", T1, sort_order=1)
    n2 = _group(db_conn, "n2", T1, sort_order=None)

    ids = [r["id"] for r in queries.get_tree(db_conn, T1)]
    # sort_order asc (nulls last), ties broken by id
    assert ids == [a, b, c, n1, n2]


def test_get_group_memberships(db_conn):
    g = _group(db_conn, "G", T1)
    other = _group(db_conn, "Other", T1)
    _member(db_conn, g, U1, T1, title="Leder", link="https://x")
    _member(db_conn, g, U2, T1)
    _member(db_conn, g, U3, T2)
    _member(db_conn, other, U4, T1)

    rows = queries.get_group_memberships(db_conn, g, T1)
    assert rows == [
        {"bcc_person_uid": U1, "title": "Leder", "link": "https://x"},
        {"bcc_person_uid": U2, "title": None, "link": None},
    ]
    assert queries.get_group_memberships(db_conn, g, None) == []


def test_get_titles(db_conn):
    g1 = _group(db_conn, "G1", T1)
    g2 = _group(db_conn, "G2", T1)
    _member(db_conn, g1, U1, T1, title="Leder")
    _member(db_conn, g2, U1, T1, title="Leder")
    _member(db_conn, g1, U2, T1, title="Medlem")
    _member(db_conn, g1, U3, T1, title=None)
    _member(db_conn, g1, U4, T2, title="Other")
    _member(db_conn, g1, U5, None, title="Nulltenant")

    assert sorted(queries.get_titles(db_conn, T1)) == ["Leder", "Medlem"]
    assert queries.get_titles(db_conn, None) == ["Nulltenant"]


def test_get_person_uids_by_title_distinct(db_conn):
    g1 = _group(db_conn, "G1", T1)
    g2 = _group(db_conn, "G2", T1)
    _member(db_conn, g1, U1, T1, title="Leder")
    _member(db_conn, g2, U1, T1, title="Leder")
    _member(db_conn, g2, U2, T1, title="Leder")
    _member(db_conn, g1, U3, T1, title="Medlem")
    _member(db_conn, g1, U4, T2, title="Leder")

    uids = queries.get_person_uids_by_title(db_conn, "Leder", T1)
    assert sorted(uids) == [U1, U2]
    assert queries.get_person_uids_by_title(db_conn, "Nope", T1) == []


def test_get_person_groups(db_conn):
    g1 = _group(db_conn, "G1", T1)
    g2 = _group(db_conn, "G2", T1)
    g3 = _group(db_conn, "G3", T1)
    g_t2 = _group(db_conn, "GT2", T2)
    _member(db_conn, g1, U1, T1)
    _member(db_conn, g1, U2, T1)
    _member(db_conn, g1, U3, T2)  # not counted: member_count is tenant-scoped
    _member(db_conn, g2, U1, T1)
    _member(db_conn, g3, U1, T2)  # membership in other tenant -> excluded
    _member(db_conn, g_t2, U1, T1)  # group in other tenant -> excluded

    rows = queries.get_person_groups(db_conn, U1, T1)
    assert rows == [
        {"id": g1, "name": "G1", "member_count": 2},
        {"id": g2, "name": "G2", "member_count": 1},
    ]
    assert queries.get_person_groups(db_conn, U5, T1) == []


def test_get_person_groups_null_tenant(db_conn):
    g = _group(db_conn, "G", None)
    _member(db_conn, g, U1, None)
    _member(db_conn, g, U2, T1)

    rows = queries.get_person_groups(db_conn, U1, None)
    assert rows == [{"id": g, "name": "G", "member_count": 1}]
    assert queries.get_person_groups(db_conn, U1, T1) == []


def test_get_membership_report_rows(db_conn):
    g1 = _group(db_conn, "G1", T1)
    g2 = _group(db_conn, "G2", T2)
    _member(db_conn, g1, U1, T1, title="Leder")
    _member(db_conn, g2, U1, T2)
    _member(db_conn, g2, U2, None)

    rows = queries.get_membership_report_rows(db_conn)
    assert rows == [
        {"bcc_person_uid": U1, "title": "Leder", "group_name": "G1"},
        {"bcc_person_uid": U1, "title": None, "group_name": "G2"},
        {"bcc_person_uid": U2, "title": None, "group_name": "G2"},
    ]
