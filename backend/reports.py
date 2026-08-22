"""
Reports module for generating various types of reports.
"""

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

        Returns a list of dicts with person details and their group memberships:
        [
            {
                "person_uid": 12345,
                "name": "John Doe",
                "groups": ["Subchild 2"],
                "group_count": 1
            },
            ...
        ]
        """
        # Collect group names grouped by person_uid
        groups_by_uid = {}
        query = self.supabase.table("group_membership").select(
            "bcc_person_uid, title, group_id, groups!inner(name)"
        )
        if root_group_name:
            group_ids = self._get_descendant_group_ids(root_group_name)
            query = query.in_("group_id", group_ids)
        for member in cast("list[dict[str, Any]]", query.execute().data):
            person_uid = member["bcc_person_uid"]
            group_name = member["groups"]["name"]
            title = member.get("title") or ""
            if len(title) > 0:
                group_name = f"{group_name} ({title})"

            if person_uid not in groups_by_uid:
                groups_by_uid[person_uid] = []
            if group_name not in groups_by_uid[person_uid]:
                groups_by_uid[person_uid].append(group_name)

        if not groups_by_uid:
            return []

        # Lookup person names from BCC API
        if self.bcc_auth.token is None or self.bcc_auth.token.is_expired():
            self.bcc_auth.renew_token()
        self.persons_api.api_client.configuration.access_token = str(
            self.bcc_auth.token
        )

        persons_by_uid = {}
        for uid in groups_by_uid.keys():
            try:
                result = cast(Any, self.persons_api.get_person(str(uid), fields="*"))
                persons_by_uid[uid] = result.data
            except Exception:
                # Person not found or API error - skip
                pass

        # Build report with person names
        report = []
        for uid, groups in groups_by_uid.items():
            person = persons_by_uid.get(uid)
            report.append(
                {
                    "person_uid": uid,
                    "name": person.display_name if person else "?",
                    "groups": groups,
                    "group_count": len(groups),
                }
            )

        # Sort by name
        report.sort(key=lambda x: x["name"].lower())

        return report
