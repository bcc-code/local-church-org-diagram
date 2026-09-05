import { ref, computed, type Ref } from "vue";
import { AUTH_CONFIG } from "@/constants";
import { fetchWithAuth } from "./useApi";
import type { CurrentUser, Role } from "@/types";

// Module-level state: shared across every component that calls useAuth(),
// so the user is only fetched once per page load.
const user: Ref<CurrentUser | null> = ref(null);
const loading = ref(false);
const loaded = ref(false);

interface RoleScope {
  tenantId?: string | number;
  groupId?: number;
}

function hasRole(role: Role, scope: RoleScope = {}): boolean {
  if (!user.value) return false;

  return user.value.roles.some((assignment) => {
    if (assignment.role !== role) return false;
    if (
      scope.tenantId !== undefined &&
      assignment.tenant_id !== null &&
      String(assignment.tenant_id) !== String(scope.tenantId)
    ) {
      return false;
    }
    if (
      scope.groupId !== undefined &&
      assignment.group_id !== null &&
      assignment.group_id !== scope.groupId
    ) {
      return false;
    }
    return true;
  });
}

export function useAuth() {
  const fetchCurrentUser = async (): Promise<CurrentUser | null> => {
    loading.value = true;
    try {
      const response = await fetchWithAuth(AUTH_CONFIG.ENDPOINTS.USER);
      user.value = response.ok ? await response.json() : null;
      return user.value;
    } finally {
      loading.value = false;
      loaded.value = true;
    }
  };

  const isGlobalAdmin = computed(() => hasRole("global_admin"));

  // A user is an admin for a tenant if they're a global admin, or a local
  // admin scoped to that specific tenant.
  const isAdminForTenant = (tenantId: string | number) =>
    isGlobalAdmin.value || hasRole("local_admin", { tenantId });

  // A user is an admin for a group if they're an admin for its tenant, or a
  // group admin scoped to that specific group.
  const isAdminForGroup = (groupId: number, tenantId?: string | number) =>
    (tenantId !== undefined && isAdminForTenant(tenantId)) ||
    hasRole("group_admin", { groupId });

  return {
    user,
    loading,
    loaded,
    fetchCurrentUser,
    hasRole,
    isGlobalAdmin,
    isAdminForTenant,
    isAdminForGroup,
  };
}
