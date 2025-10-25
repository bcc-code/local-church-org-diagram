import { ref, type Ref } from "vue";
import { API_CONFIG } from "@/constants";
import type { AsyncState, ApiError } from "@/types";

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
    const response = await fetch(
      `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.TREE}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch groups: ${response.statusText}`);
    }
    return response.json();
  };

  const fetchGroupMembers = async (groupId: number | string) => {
    const response = await fetch(
      `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.PERSONS}?group_id=${groupId}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch group members: ${response.statusText}`);
    }
    return response.json();
  };

  return {
    fetchGroups,
    fetchGroupMembers,
  };
}
