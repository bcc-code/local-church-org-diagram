"""
Reports module for generating various types of reports.
"""

import re
from typing import Any, cast

from supabase import Client
from swagger_client.api.persons_api import PersonsApi


class MembershipReport:
    def __init__(self, supabase, persons_api: PersonsApi, bcc_auth):
        self.supabase: Client = supabase
        self.persons_api = persons_api
        self.bcc_auth = bcc_auth

    def _get_descendant_group_ids(self, root_name):
        groups_data = cast(
            "list[dict[str, Any]]",
            self.supabase.table("groups").select("id, name, parent_id").execute().data,
        )
        children_by_parent = {}
        name_to_id = {}
        for g in groups_data:
            name_to_id[g["name"]] = g["id"]
            children_by_parent.setdefault(g["parent_id"], []).append(g["id"])

        root_id = name_to_id.get(root_name)
        if root_id is None:
            raise ValueError(f'Group "{root_name}" not found')

        descendant_ids = {root_id}
        stack = [root_id]
        while stack:
            pid = stack.pop()
            for cid in children_by_parent.get(pid, []):
                if cid not in descendant_ids:
                    descendant_ids.add(cid)
                    stack.append(cid)

        return list(descendant_ids)

    def generate_report(self, root_group_name=None):
        """Generate a report of group memberships with person names.

        Optionally filter to only include groups under a root group by name.
        Each group membership shows the full ancestor path, e.g.
        "Root, Child 1, Subchild 2".

        Returns a list of dicts with person details and their group memberships:
        [
            {
                "person_uid": 12345,
                "name": "John Doe",
                "team_no": "1;3",
                "groups": ["Root, Child 1, Subchild 2"]
            },
            ...
        ]
        """
        all_groups = cast(
            "list[dict[str, Any]]",
            self.supabase.table("groups").select("id, name, parent_id").execute().data,
        )
        group_names = {g["id"]: g["name"] for g in all_groups}
        group_parents = {g["id"]: g["parent_id"] for g in all_groups}

        root_id = None
        name_to_id = {g["name"]: g["id"] for g in all_groups}
        if root_group_name:
            root_id = name_to_id.get(root_group_name)

        def get_path(group_id):
            ids = []
            current = group_id
            while current is not None:
                ids.append(current)
                current = group_parents.get(current)
            ids.reverse()
            if root_id is not None:
                try:
                    idx = ids.index(root_id)
                    ids = ids[idx:]
                except ValueError:
                    pass
            return ", ".join(group_names[i] for i in ids)

        def extract_team_no(group_name):
            m = re.search(r"Hold (\d+)", group_name)
            return m.group(1) if m else None

        # Collect memberships grouped by person_uid
        memberships_by_uid = {}
        teams_by_uid = {}
        query = self.supabase.table("group_membership").select(
            "bcc_person_uid, title, group_id, groups!inner(name)"
        )
        if root_group_name:
            group_ids = self._get_descendant_group_ids(root_group_name)
            query = query.in_("group_id", group_ids)
        for member in cast("list[dict[str, Any]]", query.execute().data):
            person_uid = member["bcc_person_uid"]
            group_path = get_path(member["group_id"])
            title = member.get("title") or ""
            if len(title) > 0:
                group_path = f"{group_path} ({title})"

            if person_uid not in memberships_by_uid:
                memberships_by_uid[person_uid] = []
            if group_path not in memberships_by_uid[person_uid]:
                memberships_by_uid[person_uid].append(group_path)

            team_no = extract_team_no(group_path)
            if team_no is not None:
                teams_by_uid.setdefault(person_uid, set()).add(team_no)

        if not memberships_by_uid:
            return []

        # Lookup person names from BCC API
        if self.bcc_auth.token is None or self.bcc_auth.token.is_expired():
            self.bcc_auth.renew_token()
        self.persons_api.api_client.configuration.access_token = str(
            self.bcc_auth.token
        )

        persons_by_uid = {}
        for uid in memberships_by_uid.keys():
            try:
                result = cast(Any, self.persons_api.get_person(str(uid), fields="*"))
                persons_by_uid[uid] = result.data
            except Exception:
                # Person not found or API error - skip
                pass

        # Build report with person names
        report = []
        for uid, groups in memberships_by_uid.items():
            person = persons_by_uid.get(uid)
            team_nos = teams_by_uid.get(uid, set())
            report.append(
                {
                    "person_uid": uid,
                    "name": person.display_name if person else "?",
                    "team_no": ";".join(sorted(team_nos)) if team_nos else "",
                    "groups": groups,
                }
            )

        # Sort by name
        report.sort(key=lambda x: x["name"].lower())

        return report
