from flask import Blueprint, current_app, json, request
from swagger_client.models.person import Person

api_bp = Blueprint("api", __name__, url_prefix="/api")


@api_bp.route("/tree", methods=["GET"])
def get_tree():
    if current_app.config["DEMO_MODE"]:
        with open("demo_requests/tree.json") as f:
            demo_members = json.load(f)
        return demo_members

    supabase = current_app.config["SUPABASE"]
    tenant_id = current_app.config["TENANT_ID"]

    q = supabase.table("groups").select(
        "id, name, parent_id, group_membership(bcc_person_uid)"
    )
    if tenant_id:
        q = q.eq("tenant_id", tenant_id).eq("group_membership.tenant_id", tenant_id)
    else:
        q = q.is_("tenant_id", None).is_("group_membership.tenant_id", None)

    groups = q.execute()
    return [
        {
            "group_id": group["id"],
            "label": group["name"],
            "parent_group_id": group["parent_id"],
            "member_count": len(group["group_membership"]),
        }
        for group in groups.data
    ]


@api_bp.route("/persons", methods=["GET"])
def get_persons():
    group_id = request.args.get("group_id")
    if not group_id:
        return {"error": "No group_id provided"}, 400

    if current_app.config["DEMO_MODE"]:
        with open("demo_requests/persons.json") as f:
            demo_members = json.load(f)
        return demo_members

    supabase = current_app.config["SUPABASE"]
    tenant_id = current_app.config["TENANT_ID"]
    bcc_auth = current_app.config["BCC_AUTH"]
    persons_api = current_app.config["PERSONS_API"]

    q = (
        supabase.table("group_membership")
        .select("bcc_person_uid, title")
        .eq("group_id", group_id)
    )
    if tenant_id:
        q = q.eq("tenant_id", tenant_id)
    else:
        q = q.is_("tenant_id", None)
    memberships = q.execute()

    uids = {m["bcc_person_uid"]: m for m in memberships.data}
    if not uids:
        return []

    # Lookup person names from BCC API
    if bcc_auth.token is None or bcc_auth.token.is_expired():
        bcc_auth.renew_token()
    persons_api.api_client.configuration.access_token = str(bcc_auth.token)

    persons: list[Person] = persons_api.find_persons(
        fields="*",
        filter=json.dumps({"uid": {"_in": list(uids.keys())}}),
    ).data  # type: ignore

    results = [
        {
            "person_uid": p.uid,
            "name": p.display_name,
            "title": uids.get(p.uid, {}).get("title"),
        }
        for p in persons
    ]

    not_found_uids = [uid for uid in uids if uid not in {p.uid for p in persons}]
    for uid in not_found_uids:
        results.append(
            {"person_uid": uid, "name": "?", "title": uids.get(uid, {}).get("title")}
        )

    return results


@api_bp.route("/persons/search", methods=["GET"])
def search_persons():
    """Search for persons by name (for autocomplete)"""
    search_query = request.args.get("q", "").strip()

    if not search_query:
        return {"error": "Query parameter 'q' is required"}, 400

    if len(search_query) < 3:
        return {"error": "Search query must be at least 3 characters"}, 400

    if current_app.config["DEMO_MODE"]:
        with open("demo_requests/members.json") as f:
            demo_members = json.load(f)
        return _score_and_rank_persons(demo_members, search_query)

    # Production mode - use BCC API
    bcc_auth = current_app.config["BCC_AUTH"]
    persons_api = current_app.config["PERSONS_API"]

    if bcc_auth.token is None or bcc_auth.token.is_expired():
        bcc_auth.renew_token()
    persons_api.api_client.configuration.access_token = str(bcc_auth.token)

    persons: list[Person] = persons_api.find_persons(
        fields="*",
        filter=json.dumps({"displayName": {"_contains": search_query}}),
        limit=50,  # Get more results for better sorting
    ).data  # type: ignore

    # Convert API response to common format
    persons_data = [
        {"person_uid": p.uid, "name": p.display_name or ""} for p in persons
    ]

    return _score_and_rank_persons(persons_data, search_query)


def _score_and_rank_persons(persons_data, search_query, limit=5):
    """
    Score and rank persons by match quality.

    Args:
        persons_data: List of dicts with 'person_uid' and 'name' keys
        search_query: The search string to match against
        limit: Maximum number of results to return

    Returns:
        List of top matches with 'person_uid' and 'name' keys
    """
    scored_results = []
    query_lower = search_query.lower()

    for person in persons_data:
        name = person["name"]
        name_lower = name.lower()

        # Skip if name doesn't contain the query
        if query_lower not in name_lower:
            continue

        # Calculate match score (higher is better)
        if name_lower == query_lower:
            score = 1000  # Exact match
        elif name_lower.startswith(query_lower):
            score = 500  # Starts with
        else:
            score = 100 + (len(name_lower) - name_lower.find(query_lower))  # Contains

        scored_results.append(
            {
                "score": score,
                "person_uid": person["person_uid"],
                "name": person["name"],
            }
        )

    # Sort by score (descending) and return top results
    scored_results.sort(key=lambda x: x["score"], reverse=True)
    top_results = scored_results[:limit]

    # Remove score from response
    return [{"person_uid": r["person_uid"], "name": r["name"]} for r in top_results]
