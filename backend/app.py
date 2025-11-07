import logging
import os

from authlib.integrations.flask_client import OAuth
from dotenv import load_dotenv
from flask import (
    Flask,
    current_app,
    send_from_directory,
    session,
    redirect,
    url_for,
)
from requests_oauth2client import OAuth2Client, OAuth2ClientCredentialsAuth
from supabase import create_client
import swagger_client as bcc_api_client

from admin import admin_bp
from api import api_bp

load_dotenv()

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("app")

app = Flask("org-diagram", static_folder="public", static_url_path="")
app.secret_key = os.environ.get("FLASK_SECRET_KEY", os.urandom(24))
oauth = OAuth(app)

# Store configuration values
app.config["TENANT_ID"] = os.environ.get("TENANT_ID")
app.config["DEMO_MODE"] = os.environ.get("DEMO_MODE", "0") == "1"
FLASK_DEBUG = os.environ.get("FLASK_DEBUG", "0") == "1"

if not app.config["DEMO_MODE"]:
    logger.info("Configuring app in PRODUCTION mode")
    oauth.register(
        name="bcc",
        client_id=os.environ.get("BCC_OIDC_CLIENT_ID"),
        client_secret=os.environ.get("BCC_OIDC_CLIENT_SECRET"),
        server_metadata_url="https://login.bcc.no/.well-known/openid-configuration",
        client_kwargs={"scope": "openid profile email"},
    )

    # Store dependencies in app.config
    app.config["SUPABASE"] = create_client(
        os.environ["SUPABASE_URL"], os.environ["SUPABASE_KEY"]
    )

    bcc_oauth_client = OAuth2Client(
        token_endpoint="https://login.bcc.no/oauth/token",
        client_id=os.environ["BCC_OAUTH_CLIENT_ID"],
        client_secret=os.environ["BCC_OAUTH_CLIENT_SECRET"],
    )

    app.config["BCC_AUTH"] = OAuth2ClientCredentialsAuth(
        bcc_oauth_client,
        scope="persons.name#read",
        audience="api.bcc.no",
    )

    bcc_client_config = bcc_api_client.Configuration()
    bcc_client_config.host = "https://core.api.bcc.no"
    api_client = bcc_api_client.ApiClient(configuration=bcc_client_config)
    app.config["PERSONS_API"] = bcc_api_client.PersonsApi(api_client)
else:
    logger.info("Running in DEMO mode")
    # Load demo data into memory for interactive demo mode
    import json
    with open("demo_requests/tree.json") as f:
        app.config["DEMO_TREE"] = json.load(f)
    with open("demo_requests/members.json") as f:
        app.config["DEMO_MEMBERS"] = json.load(f)
    with open("demo_requests/persons.json") as f:
        app.config["DEMO_PERSONS"] = json.load(f)
    # Create a mapping of group_id -> list of person_uids for membership
    app.config["DEMO_MEMBERSHIPS"] = {}  # {group_id: [person_uid, ...]}

# Register blueprints
app.register_blueprint(api_bp)
app.register_blueprint(admin_bp)


@app.route("/")
def index():
    """Serve the Vue frontend"""
    return send_from_directory(app.static_folder, "index.html")  # type: ignore


@app.route("/login")
def login():
    """Initiate OIDC login flow"""
    if current_app.config["DEMO_MODE"]:
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
