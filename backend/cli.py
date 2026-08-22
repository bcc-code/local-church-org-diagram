import csv
import logging
import sys

import click

logging.getLogger().setLevel(logging.WARNING)
from flask import current_app  # noqa: E402
from flask.cli import with_appcontext  # noqa: E402
from reports import MembershipReport  # noqa: E402


@click.group()
def generate_report():
    """Generate various reports in CSV format."""
    pass


@generate_report.command("membership")
@click.option("--root", help="Only include groups under this root group (by name)")
@with_appcontext
def membership_report(root):
    """Generate a membership report showing persons and their groups."""
    if current_app.config.get("DEMO_MODE"):
        click.echo("Error: Reports not available in demo mode", err=True)
        sys.exit(1)

    supabase = current_app.config["SUPABASE"]
    persons_api = current_app.config["PERSONS_API"]
    bcc_auth = current_app.config["BCC_AUTH"]

    report = MembershipReport(supabase, persons_api, bcc_auth)
    try:
        data = report.generate_report(root_group_name=root)
    except ValueError as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)

    writer = csv.writer(sys.stdout)
    writer.writerow(["person_uid", "name", "groups", "group_count"])
    for row in data:
        writer.writerow(
            [row["person_uid"], row["name"], ";".join(row["groups"]), row["group_count"]]
        )
