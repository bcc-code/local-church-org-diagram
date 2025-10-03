import os

from dotenv import load_dotenv
from flask import Flask
from supabase import Client, create_client

load_dotenv()

app = Flask(__name__)
supabase: Client = create_client(os.environ["SUPABASE_URL"], os.environ["SUPABASE_KEY"])
supabase.options.schema = "public"


@app.route("/")
def hello():
    return "OK!"


@app.route("/tree", methods=["GET"])
def get_tree():
    groups = supabase.table("groups").select("*").execute().data
    return [
        {
            "group_id": group["id"],
            "label": group["name"],
            "parent_group_id": group["parent_id"],
        }
        for group in groups
    ]


if __name__ == "__main__":
    app.run(debug=True)
