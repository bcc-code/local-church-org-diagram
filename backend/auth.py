import logging
from flask import Blueprint, current_app, session, redirect, url_for

auth_bp = Blueprint("auth", __name__, url_prefix="/")

logger = logging.getLogger("auth")


@auth_bp.route("/login", methods=["GET"])
def login():
    """Initiate OIDC login flow"""
    oidc = current_app.config["oidc"]
    if current_app.config["DEMO_MODE"]:
        session["user"] = {"email": "demo@example.com", "name": "Demo User"}
        return redirect("/")

    redirect_uri = url_for("auth.authorize", _scheme="https", _external=True)
    logger.debug(f"OIDC redirect URI: {redirect_uri}")
    return oidc.bcc.authorize_redirect(redirect_uri)


@auth_bp.route("/authorize")
def authorize():
    """OIDC callback endpoint"""
    oidc = current_app.config["oidc"]
    token = oidc.bcc.authorize_access_token()
    userinfo = token.get("userinfo")

    if userinfo:
        userinfo["churchId"] = userinfo.get("https://login.bcc.no/claims/churchId")
        session["user"] = userinfo
        logger.info(f"User logged in: {userinfo.get('email')}")

    return redirect("/")


@auth_bp.route("/logout")
def logout():
    """Clear user session"""
    session.pop("user", None)
    return redirect("/")


@auth_bp.route("/user")
def get_current_user():
    """Get current authenticated user information"""
    user = session.get("user")
    if user:
        return user
    return {"authenticated": False}, 401
