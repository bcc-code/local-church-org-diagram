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

# In-memory storage for DEMO mode
demo_storage = {"tree": [], "group_memberships": {}}


def init_demo_storage():
    """Initialize in-memory storage from demo JSON files"""
    global demo_storage

    # Load tree structure
    with open("demo_requests/tree.json") as f:
        demo_storage["tree"] = json.load(f)

    # Load persons data and build group memberships
    with open("demo_requests/members.json") as f:
        members = json.load(f)

    # Get all person UIDs
    person_uids = [m["person_uid"] for m in members]

    # Distribute members across different groups
    demo_storage["group_memberships"] = {
        "2": [person_uids[0]] if len(person_uids) > 0 else [],  # Forstander - John Doe
        "3": person_uids[1:6]
        if len(person_uids) > 5
        else person_uids[
            1:
        ],  # Bestyrelse - Jane Smith, Bob Johnson, Alice Williams, Charlie Brown, Eva Martinez
        "7": [person_uids[6]] if len(person_uids) > 6 else [],  # TASK - David Anderson
        "9": [person_uids[7]] if len(person_uids) > 7 else [],  # VK - Sarah Thompson
        "13": person_uids[1:3]
        if len(person_uids) > 2
        else [],  # Ungdom - Jane Smith, Bob Johnson
        "52": person_uids[4:6]
        if len(person_uids) > 5
        else [],  # Musik - Charlie Brown, Eva Martinez
    }

    logger.info("Demo storage initialized with in-memory data")


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
    init_demo_storage()


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
        # Return tree with updated member counts from in-memory storage
        tree_copy = []
        for group in demo_storage["tree"]:
            group_copy = group.copy()
            group_id = str(group["group_id"])
            # Update member count from in-memory storage
            group_copy["member_count"] = len(
                demo_storage["group_memberships"].get(group_id, [])
            )
            tree_copy.append(group_copy)
        return tree_copy

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
        # Get UIDs for this group from in-memory storage
        uids = demo_storage["group_memberships"].get(str(group_id), [])

        if not uids:
            return []

        # Load all available persons from members.json
        with open("demo_requests/members.json") as f:
            all_members = json.load(f)

        # Filter to only persons in this group
        results = [
            {"person_uid": member["person_uid"], "name": member["name"]}
            for member in all_members
            if member["person_uid"] in uids
        ]

        # Add any UIDs not found in members.json with placeholder names
        found_uids = {r["person_uid"] for r in results}
        for uid in uids:
            if uid not in found_uids:
                results.append({"person_uid": uid, "name": "?"})

        return results

    uids = []
    q = (
        supabase.table("group_membership")
        .select("bcc_person_uid")
        .eq("group_id", group_id)
    )
    if TENANT_ID:
        q = q.eq("tenant_id", TENANT_ID)
    else:
        q = q.is_("tenant_id", None)
    memberships = q.execute()
    for membership in memberships.data:
        uids.append(membership["bcc_person_uid"])

    if not uids:
        return []

    # Lookup person names from BCC API
    if bcc_auth.token is None or bcc_auth.token.is_expired():
        bcc_auth.renew_token()
    persons_api.api_client.configuration.access_token = str(bcc_auth.token)

    persons: list[Person] = persons_api.find_persons(
        fields="*",
        filter=json.dumps({"uid": {"_in": uids}}),
    ).data  # type: ignore

    results = [
        {
            "person_uid": p.uid,
            "name": p.display_name,
        }
        for p in persons
    ]

    not_found_uids = [uid for uid in uids if uid not in {p.uid for p in persons}]
    for uid in not_found_uids:
        results.append({"person_uid": uid, "name": "?"})

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
    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if DEMO_MODE:
        # Add to in-memory storage
        group_id_str = str(group_id)

        # Initialize group if it doesn't exist
        if group_id_str not in demo_storage["group_memberships"]:
            demo_storage["group_memberships"][group_id_str] = []

        # Check if already a member
        if person_uid in demo_storage["group_memberships"][group_id_str]:
            return {"error": "Person is already a member of this group"}, 400

        # Add the member
        demo_storage["group_memberships"][group_id_str].append(person_uid)

        logger.info(f"Added {person_uid} to group {group_id_str} (DEMO mode)")

        return {
            "success": True,
            "data": {"group_id": group_id, "bcc_person_uid": person_uid},
        }, 201

    membership_data = {
        "group_id": group_id,
        "bcc_person_uid": person_uid,
    }

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
    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if DEMO_MODE:
        # Remove from in-memory storage
        group_id_str = str(group_id)

        # Check if group exists
        if group_id_str not in demo_storage["group_memberships"]:
            return {"error": "Member not found in group"}, 404

        # Check if person is a member
        if person_uid not in demo_storage["group_memberships"][group_id_str]:
            return {"error": "Member not found in group"}, 404

        # Remove the member
        demo_storage["group_memberships"][group_id_str].remove(person_uid)

        logger.info(f"Removed {person_uid} from group {group_id_str} (DEMO mode)")

        return {
            "success": True,
            "data": {"group_id": group_id, "bcc_person_uid": person_uid},
        }, 200

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
