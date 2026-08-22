# Report Generation CLI Plan

## Overview

Add a Flask CLI command for generating reports that outputs CSV data to the terminal.

## Usage

```bash
flask generate-report membership
```

Future report types can be added as subcommands (e.g., `flask generate-report attendance`).

## Implementation

### 1. Create CLI module: `backend/cli.py`

```python
import csv
import sys

import click
from flask import current_app
from flask.cli import with_appcontext

from reports import MembershipReport


@click.group()
def generate_report():
    """Generate various reports in CSV format."""
    pass


@generate_report.command("membership")
@with_appcontext
def membership_report():
    """Generate a membership report showing persons and their groups."""
    if current_app.config.get("DEMO_MODE"):
        click.echo("Error: Reports not available in demo mode", err=True)
        sys.exit(1)

    supabase = current_app.config["SUPABASE"]
    report = MembershipReport(supabase)
    data = report.generate_report()

    writer = csv.writer(sys.stdout)
    writer.writerow(["person_uid", "groups"])
    for person_uid, groups in data.items():
        writer.writerow([person_uid, ";".join(groups)])
```

### 2. Register CLI command in `backend/app.py`

Add the following after blueprint registrations:

```python
from cli import generate_report
app.cli.add_command(generate_report)
```

### 3. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Use `@click.group()` | Allows future report types as subcommands |
| Use `@with_appcontext` | Access Flask app config (Supabase client) |
| Output to `sys.stdout` | Standard CSV output, can be piped/redirected |
| Semicolon delimiter for groups | Groups column contains multiple values |
| Error on demo mode | No Supabase client available in demo mode |

### 4. CSV Output Format

```csv
person_uid,groups
12345,Group A;Group B (Leader)
67890,Group C
```

### 5. Future Extensibility

To add new report types:

1. Create a new report class in `reports.py` (e.g., `AttendanceReport`)
2. Add a new subcommand in `cli.py`:

```python
@generate_report.command("attendance")
@with_appcontext
def attendance_report():
    """Generate an attendance report."""
    # Implementation
```

## Files to Create/Modify

| File | Action |
|------|--------|
| `backend/cli.py` | Create new file |
| `backend/app.py` | Add CLI command registration |

## Testing

```bash
# Run from backend directory with .env configured
cd backend
flask generate-report membership > membership.csv

# Or pipe to other tools
flask generate-report membership | head -10
```
