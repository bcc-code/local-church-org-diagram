import os

from dotenv import load_dotenv
from flask import Flask
from supabase import Client, create_client

load_dotenv()

app = Flask(__name__)
supabase: Client = create_client(os.environ["SUPABASE_URL"], os.environ["SUPABASE_KEY"])

TENANT_ID = os.environ.get("TENANT_ID")


@app.route("/")
def hello():
    return "OK!"


@app.route("/tree", methods=["GET"])
def get_tree():
    q = supabase.table("groups").select("*")
    if TENANT_ID:
        q = q.eq("tenant_id", TENANT_ID)
    else:
        q = q.is_("tenant_id", None)

    groups = q.execute()
    return [
        {
            "group_id": group["id"],
            "label": group["name"],
            "parent_group_id": group["parent_id"],
        }
        for group in groups.data
    ]


if __name__ == "__main__":
    app.run(debug=True)
