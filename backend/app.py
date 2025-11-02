import logging
import os

from authlib.integrations.flask_client import OAuth
from dotenv import load_dotenv
from flask import (
    Flask,
    json,
    request,
    send_file,
    send_from_directory,
    session,
    redirect,
    url_for,
)
from requests_oauth2client import OAuth2Client, OAuth2ClientCredentialsAuth
from supabase import Client, create_client
import swagger_client as bcc_api_client
from swagger_client.models.person import Person

load_dotenv()

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("app")

app = Flask("org-diagram", static_folder="public", static_url_path="")
app.secret_key = os.environ.get("FLASK_SECRET_KEY", os.urandom(24))
oauth = OAuth(app)

TENANT_ID = os.environ.get("TENANT_ID")
DEMO_MODE = os.environ.get("DEMO_MODE", "0") == "1"
FLASK_DEBUG = os.environ.get("FLASK_DEBUG", "0") == "1"

if not DEMO_MODE:
    logger.info("Configuring app in PRODUCTION mode")
    oauth.register(
        name="bcc",
        client_id=os.environ.get("BCC_OIDC_CLIENT_ID"),
        client_secret=os.environ.get("BCC_OIDC_CLIENT_SECRET"),
        server_metadata_url="https://login.bcc.no/.well-known/openid-configuration",
        client_kwargs={"scope": "openid profile email"},
    )

    supabase: Client = create_client(
        os.environ["SUPABASE_URL"], os.environ["SUPABASE_KEY"]
    )

    bcc_oauth_client = OAuth2Client(
        token_endpoint="https://login.bcc.no/oauth/token",
        client_id=os.environ["BCC_OAUTH_CLIENT_ID"],
        client_secret=os.environ["BCC_OAUTH_CLIENT_SECRET"],
    )

    bcc_auth = OAuth2ClientCredentialsAuth(
        bcc_oauth_client,
        scope="persons.name#read",
        audience="api.bcc.no",
    )

    bcc_client_config = bcc_api_client.Configuration()
    bcc_client_config.host = "https://core.api.bcc.no"
    api_client = bcc_api_client.ApiClient(configuration=bcc_client_config)
    persons_api = bcc_api_client.PersonsApi(api_client)
else:
    logger.info("Running in DEMO mode")


@app.route("/")
def index():
    """Serve the Vue frontend"""
    return send_from_directory(app.static_folder, "index.html")  # type: ignore


@app.route("/login")
def login():
    """Initiate OIDC login flow"""
    if DEMO_MODE:
        # In demo mode, just set a fake user session
        session["user"] = {"email": "demo@example.com", "name": "Demo User"}
        return redirect("/")

    redirect_uri = url_for("authorize", _external=True)
    return oauth.bcc.authorize_redirect(redirect_uri)  # type: ignore


@app.route("/authorize")
def authorize():
    """OIDC callback endpoint"""
    token = oauth.bcc.authorize_access_token()  # type: ignore
    userinfo = token.get("userinfo")

    if userinfo:
        session["user"] = {
            "email": userinfo.get("email"),
            "name": userinfo.get("name"),
            "sub": userinfo.get("sub"),
        }
        logger.info(f"User logged in: {userinfo.get('email')}")

    return redirect("/")


@app.route("/logout")
def logout():
    """Clear user session"""
    session.pop("user", None)
    return redirect("/")


@app.route("/api/user")
def get_current_user():
    """Get current authenticated user information"""
    user = session.get("user")
    if user:
        return user
    return {"authenticated": False}, 401


@app.route("/api/tree", methods=["GET"])
def get_tree():
    if DEMO_MODE:
        with open("demo_requests/tree.json") as f:
            demo_members = json.load(f)
        return demo_members

    q = supabase.table("groups").select(
        "id, name, parent_id, group_membership(bcc_person_uid)"
    )
    if TENANT_ID:
        q = q.eq("tenant_id", TENANT_ID).eq("group_membership.tenant_id", TENANT_ID)
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


@app.route("/api/persons", methods=["GET"])
def get_persons():
    group_id = request.args.get("group_id")
    if not group_id:
        return {"error": "No group_id provided"}, 400

    if DEMO_MODE:
        with open("demo_requests/persons.json") as f:
            demo_members = json.load(f)
        return demo_members

    q = (
        supabase.table("group_membership")
        .select("bcc_person_uid, title")
        .eq("group_id", group_id)
    )
    if TENANT_ID:
        q = q.eq("tenant_id", TENANT_ID)
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


@app.route("/api/persons/search", methods=["GET"])
def search_persons():
    """Search for persons by name (for autocomplete)"""
    search_query = request.args.get("q", "").strip()

    if not search_query:
        return {"error": "Query parameter 'q' is required"}, 400

    if len(search_query) < 3:
        return {"error": "Search query must be at least 3 characters"}, 400

    if DEMO_MODE:
        with open("demo_requests/members.json") as f:
            demo_members = json.load(f)
        return _score_and_rank_persons(demo_members, search_query)

    # Production mode - use BCC API
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


@app.route("/api/group-membership", methods=["POST"])
def add_group_member():
    """Add a member to a group"""
    if DEMO_MODE:
        return {"success": True}, 200

    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")
    title = data.get("title")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    membership_data = {
        "group_id": group_id,
        "bcc_person_uid": person_uid,
    }

    if title is not None:
        membership_data["title"] = title

    if TENANT_ID:
        membership_data["tenant_id"] = TENANT_ID

    result = supabase.table("group_membership").insert(membership_data).execute()

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 201


@app.route("/api/group-membership", methods=["DELETE"])
def remove_group_member():
    """Remove a member from a group"""
    if DEMO_MODE:
        return {"success": True}, 200

    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    q = (
        supabase.table("group_membership")
        .delete()
        .eq("group_id", group_id)
        .eq("bcc_person_uid", person_uid)
    )

    if TENANT_ID:
        q = q.eq("tenant_id", TENANT_ID)
    else:
        q = q.is_("tenant_id", None)

    result = q.execute()

    if not result.data:
        return {"error": "Member not found in group"}, 404

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 200


@app.route("/api/group-membership", methods=["PUT"])
def update_group_member():
    """Update a group member's properties (currently only title)"""
    if DEMO_MODE:
        return {"success": True}, 200

    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")
    title = data.get("title")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if title is None:
        return {"success": True}, 304  # Not modified

    q = (
        supabase.table("group_membership")
        .update({"title": title})
        .eq("group_id", group_id)
        .eq("bcc_person_uid", person_uid)
    )

    if TENANT_ID:
        q = q.eq("tenant_id", TENANT_ID)
    else:
        q = q.is_("tenant_id", None)

    result = q.execute()
    if not result.data:
        return {"error": "Member not found in group"}, 404

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 200


@app.route("/<path:path>")
def serve_spa(path):
    """Catch-all route for SPA client-side routing"""
    if app.static_folder is None:
        return "Static folder not configured", 500

    file_path = os.path.join(app.static_folder, path)
    if os.path.exists(file_path) and os.path.isfile(file_path):
        return send_from_directory(app.static_folder, path)
    # For any other path, serve index.html (Vue Router will handle it)
    return send_from_directory(app.static_folder, "index.html")


if __name__ == "__main__":
    app.run(debug=FLASK_DEBUG)
