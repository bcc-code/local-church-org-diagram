import os

from dotenv import load_dotenv
from flask import Flask
from requests_oauth2client import OAuth2Client, OAuth2ClientCredentialsAuth
from supabase import Client, create_client
import swagger_client as bcc_api_client

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


@app.route("/users", methods=["GET"])
def get_users():
    bcc_client_config.access_token = str(
        OAuth2ClientCredentialsAuth(oauth_client).token
    )
    api_client = bcc_api_client.ApiClient(configuration=bcc_client_config)
    persons_api = bcc_api_client.PersonsApi(api_client)
    users = persons_api.get_person(uid="1")
    return [{"user_id": user["id"], "name": user["name"]} for user in users]


if __name__ == "__main__":
    supabase: Client = create_client(
        os.environ["SUPABASE_URL"], os.environ["SUPABASE_KEY"]
    )

    oauth_client = OAuth2Client(
        token_endpoint="https://api.bcc.no/oauth2/token",
        scope="bcc-core-api",
        client_id=os.environ["BCC_OAUTH_CLIENT_ID"],
        client_secret=os.environ["BCC_OAUTH_CLIENT_SECRET"],
    )

    bcc_client_config = bcc_api_client.Configuration()
    bcc_client_config.host = "https://api.bcc.no"

    app.run(debug=True)
