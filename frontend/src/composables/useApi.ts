import { ref, type Ref } from "vue";
import { API_CONFIG } from "@/constants";
import type { AsyncState, ApiError } from "@/types";

// Fetch wrapper that handles 401 responses by redirecting to login
async function fetchWithAuth(
  input: RequestInfo | URL,
  init?: RequestInit
): Promise<Response> {
  const response = await fetch(input, init);

  // Redirect to login if unauthorized
  if (response.status === 401) {
    window.location.href = "/login";
    throw new Error("Unauthorized - redirecting to login");
  }

  return response;
}

export function useAsyncData<T>() {
  const state = ref<AsyncState<T>>({
    data: null,
    loading: false,
    error: null,
  }) as Ref<AsyncState<T>>;

  const execute = async (
    asyncFn: () => Promise<T>,
    onSuccess?: (data: T) => void,
    onError?: (error: ApiError) => void
  ) => {
    state.value.loading = true;
    state.value.error = null;

    try {
      const result = await asyncFn();
      state.value.data = result;
      onSuccess?.(result);
    } catch (error) {
      const apiError: ApiError = {
        message:
          error instanceof Error ? error.message : "An unknown error occurred",
        code: "UNKNOWN_ERROR",
      };
      state.value.error = apiError.message;
      onError?.(apiError);
      console.error("Async operation failed:", error);
    } finally {
      state.value.loading = false;
    }
  };

  const reset = () => {
    state.value = {
      data: null,
      loading: false,
      error: null,
    };
  };

  return {
    state,
    execute,
    reset,
    // Computed helpers
    isLoading: () => state.value.loading,
    hasError: () => !!state.value.error,
    hasData: () => !!state.value.data,
  };
}

// Specific API fetchers
export function useApiClient() {
  const fetchGroups = async () => {
    const response = await fetchWithAuth(
      `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.TREE}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch groups: ${response.statusText}`);
    }
    return response.json();
  };

  const fetchGroupMembers = async (groupId: number | string) => {
    const response = await fetchWithAuth(
      `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.PERSONS}?group_id=${groupId}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch group members: ${response.statusText}`);
    }
    return response.json();
  };

  const searchPersons = async (query: string) => {
    const response = await fetchWithAuth(
      `${API_CONFIG.BASE_URL}/persons/search?q=${encodeURIComponent(query)}`
    );
    if (!response.ok) {
      throw new Error(`Failed to search persons: ${response.statusText}`);
    }
    return response.json();
  };

  const addGroupMember = async (
    groupId: number | string,
    personUid: string
  ) => {
    const response = await fetchWithAuth(
      `${API_CONFIG.BASE_URL}/group-membership`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          group_id: groupId,
          person_uid: personUid,
        }),
      }
    );
    if (!response.ok) {
      throw new Error(`Failed to add group member: ${response.statusText}`);
    }
    return response.json();
  };

  const removeGroupMember = async (
    groupId: number | string,
    personUid: string
  ) => {
    const response = await fetchWithAuth(
      `${API_CONFIG.BASE_URL}/group-membership`,
      {
        method: "DELETE",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          group_id: groupId,
          person_uid: personUid,
        }),
      }
    );
    if (!response.ok) {
      throw new Error(`Failed to remove group member: ${response.statusText}`);
    }
    return response.json();
  };

  const updateMemberTitle = async (
    groupId: number | string,
    personUid: string,
    title: string
  ) => {
    const response = await fetchWithAuth(
      `${API_CONFIG.BASE_URL}/group-membership`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          group_id: groupId,
          person_uid: personUid,
          title: title,
        }),
      }
    );
    if (!response.ok) {
      throw new Error(`Failed to update member title: ${response.statusText}`);
    }
    return response.json();
  };

  return {
    fetchGroups,
    fetchGroupMembers,
    searchPersons,
    addGroupMember,
    removeGroupMember,
    updateMemberTitle,
  };
}
