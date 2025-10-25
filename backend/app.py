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
logger = logging.getLogger(__name__)

app = Flask("org-diagram", static_folder="public", static_url_path="")
app.secret_key = os.environ.get("FLASK_SECRET_KEY", os.urandom(24))
oauth = OAuth(app)

TENANT_ID = os.environ.get("TENANT_ID")


@app.route("/")
def index():
    """Serve the Vue frontend"""
    return send_from_directory(app.static_folder, "index.html")  # type: ignore


@app.route("/login")
def login():
    """Initiate OIDC login flow"""
    if os.environ.get("DEMO_MODE") == "1":
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
            "person_uids": [
                membership["bcc_person_uid"] for membership in group["group_membership"]
            ],
        }
        for group in groups.data
    ]


@app.route("/api/persons", methods=["GET"])
def get_persons():
    uids = request.args.getlist("uids")
    if not uids:
        return {"error": "No uids provided"}, 400

    if bcc_auth.token is None or bcc_auth.token.is_expired():
        bcc_auth.renew_token()
    persons_api.api_client.configuration.access_token = str(bcc_auth.token)

    persons: list[Person] = persons_api.find_persons(
        fields="*",
        filter=json.dumps({"uid": {"_in": uids}}),
    ).data  # type: ignore
    return [{"person_uid": p.uid, "name": p.display_name} for p in persons]


@app.before_request
def demo_mode():
    """Serve demo JSON files in demo mode for API requests"""
    if os.environ.get("DEMO_MODE") == "1" and request.path.startswith("/api/"):
        # Remove /api prefix for demo file lookup
        demo_path = request.path.replace("/api", "", 1)
        return send_file(f"demo_requests{demo_path}.json")


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
    if not os.environ.get("DEMO_MODE"):
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

    app.run(debug=True)
