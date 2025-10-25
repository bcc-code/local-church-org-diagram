import os

from dotenv import load_dotenv
from flask import Flask, json, request, send_file
from requests_oauth2client import OAuth2Client, OAuth2ClientCredentialsAuth
from supabase import Client, create_client
import swagger_client as bcc_api_client
from swagger_client.models.person import Person

load_dotenv()

app = Flask("org-diagram")


TENANT_ID = os.environ.get("TENANT_ID")


@app.route("/")
def up():
    return "OK!"


@app.route("/tree", methods=["GET"])
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


@app.route("/persons", methods=["GET"])
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
    if os.environ.get("DEMO_MODE") == "1" and request.path != "/":
        return send_file(f"demo_requests{request.path}.json")


if __name__ == "__main__":
    if not os.environ.get("DEMO_MODE"):
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
