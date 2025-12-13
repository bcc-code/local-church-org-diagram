import logging
import os

import swagger_client as bcc_api_client
from admin import admin_bp
from api import api_bp
from auth import auth_bp
from authlib.integrations.flask_client import OAuth
from dotenv import load_dotenv
from flask import (
    Flask,
    redirect,
    send_from_directory,
    session,
    url_for,
)
from requests_oauth2client import OAuth2Client, OAuth2ClientCredentialsAuth
from supabase import create_client
from swagger_client.rest import ApiException

load_dotenv()

FLASK_DEBUG = os.environ.get("FLASK_DEBUG", "0") == "1"

logging.basicConfig(level=logging.DEBUG if FLASK_DEBUG else logging.INFO)
logger = logging.getLogger("app")

app = Flask("org-diagram", static_folder="public", static_url_path="")
app.secret_key = os.environ.get("FLASK_SECRET_KEY", os.urandom(24))
app.config["oidc"] = OAuth(app)

app.config["TENANT_ID"] = os.environ.get("TENANT_ID")
app.config["DEMO_MODE"] = os.environ.get("DEMO_MODE", "0") == "1"

if not app.config["DEMO_MODE"]:
    logger.info("Configuring app in PRODUCTION mode")
    app.config["oidc"].register(
        name="bcc",
        client_id=os.environ.get("BCC_OIDC_CLIENT_ID"),
        client_secret=os.environ.get("BCC_OIDC_CLIENT_SECRET"),
        server_metadata_url="https://login.bcc.no/.well-known/openid-configuration",
        client_kwargs={"scope": "openid profile email church"},
    )

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
        scope="persons.name#read persons.profile_picture#read",
        audience="api.bcc.no",
    )

    bcc_client_config = bcc_api_client.Configuration()
    bcc_client_config.host = "https://core.api.bcc.no"
    bcc_client_config.debug = app.debug
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
    # Load group memberships mapping
    with open("demo_requests/memberships.json") as f:
        memberships_data = json.load(f)
        # Convert string keys to integers
        app.config["DEMO_MEMBERSHIPS"] = {
            int(k): v for k, v in memberships_data.items()
        }


app.register_blueprint(auth_bp)
app.register_blueprint(api_bp)
app.register_blueprint(admin_bp)


@app.route("/")
@app.route("/<int:tenant_id>/admin")
def index(tenant_id=None):
    """Serves the Vue rontend i"""
    user = session.get("user")
    if not user:
        return redirect(url_for("auth.login"))
    if tenant_id is not None and int(tenant_id) != user.get("churchId"):
        return "Unauthorized", 403

    return send_from_directory(app.static_folder, "index.html")  # type: ignore


@app.route("/<path:path>")
def serve_spa(path):
    """Catch-all route for SPA client-side routing"""
    if app.static_folder is None:
        raise RuntimeError("Static folder is not set")

    file_path = os.path.join(app.static_folder, path)
    if not (os.path.exists(file_path) and os.path.isfile(file_path)):
        return "Not found", 404

    return send_from_directory(app.static_folder, path)


@app.route("/health")
def health():
    """Health check endpoint"""
    bcc_auth = app.config["BCC_AUTH"]
    persons_api: bcc_api_client.PersonsApi = app.config["PERSONS_API"]
    if bcc_auth.token is None or bcc_auth.token.is_expired():
        bcc_auth.renew_token()
    persons_api.api_client.configuration.access_token = str(bcc_auth.token)
    try:
        persons_api.get_person(1234)  # Dummy call to check connectivity
    except ApiException as api_error:
        if api_error.status != 404:
            logger.error("Health check failed: %s", api_error)
            return "BCC API connectivity issue", 500
        # We expect a 404

    return "OK", 200


if __name__ == "__main__":
    app.run(debug=FLASK_DEBUG)
