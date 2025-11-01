<template>
    <div class="relative">
        <div class="relative">
            <!-- Search icon -->
            <div class="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-400 pointer-events-none">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
            </div>

            <input v-model="searchQuery" @input="handleInput" @focus="handleFocus" @blur="handleBlur"
                type="text" placeholder="Søk etter person..."
                class="w-full pl-9 pr-10 py-2.5 text-sm border border-neutral-300 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-brand-500 focus:border-brand-500 transition-shadow placeholder:text-neutral-400"
                :disabled="loading" />

            <!-- Loading spinner -->
            <div v-if="loading"
                class="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 border-2 border-brand-200 border-t-brand-600 rounded-full animate-spin">
            </div>

            <!-- Clear button -->
            <button v-else-if="searchQuery.length > 0" @click="clearSearch"
                class="absolute right-3 top-1/2 -translate-y-1/2 text-neutral-400 hover:text-neutral-600 transition-colors">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
            </button>
        </div>

        <!-- Dropdown results -->
        <div v-if="showDropdown && (results.length > 0 || error || (searchQuery.length > 0 && searchQuery.length < 3))"
            class="absolute z-50 w-full mt-2 bg-white border border-neutral-200 rounded-lg shadow-lg overflow-hidden">

            <!-- Minimum characters hint -->
            <div v-if="searchQuery.length > 0 && searchQuery.length < 3" class="px-3 py-2.5 text-xs text-neutral-500 bg-neutral-50">
                Skriv minst 3 tegn for å søke
            </div>

            <!-- Error message -->
            <div v-else-if="error" class="px-3 py-2.5 text-sm text-red-600 bg-red-50">
                <div class="flex items-center gap-2">
                    <svg class="w-4 h-4 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                        <path fill-rule="evenodd"
                            d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                            clip-rule="evenodd" />
                    </svg>
                    <span>{{ error }}</span>
                </div>
            </div>

            <!-- Results list -->
            <div v-else-if="results.length > 0" class="max-h-60 overflow-y-auto">
                <button v-for="person in results" :key="person.person_uid" @mousedown.prevent="selectPerson(person)"
                    class="w-full px-3 py-2.5 text-left hover:bg-brand-50 focus:bg-brand-50 focus:outline-none transition-colors border-b border-neutral-100 last:border-b-0 group">
                    <div class="flex items-center justify-between gap-2">
                        <div class="min-w-0 flex-1">
                            <div class="text-sm font-medium text-neutral-900 truncate">{{ person.name }}</div>
                            <div class="text-xs text-neutral-500 truncate font-mono mt-0.5">{{ person.person_uid }}</div>
                        </div>
                        <svg class="w-4 h-4 text-neutral-400 group-hover:text-brand-600 transition-colors flex-shrink-0"
                            fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                        </svg>
                    </div>
                </button>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { ref, onUnmounted } from 'vue';
import { useApiClient } from '@/composables/useApi';
import type { GroupMember } from '@/types';

interface Emits {
    (e: 'select', person: GroupMember): void;
}

const emit = defineEmits<Emits>();

const { searchPersons } = useApiClient();

const searchQuery = ref('');
const results = ref<GroupMember[]>([]);
const showDropdown = ref(false);
const loading = ref(false);
const error = ref<string | null>(null);
let searchTimeout: ReturnType<typeof setTimeout> | null = null;

const handleInput = () => {
    error.value = null;

    // Clear previous timeout
    if (searchTimeout) {
        clearTimeout(searchTimeout);
    }

    const query = searchQuery.value.trim();

    // Don't search if less than 3 characters
    if (query.length < 3) {
        results.value = [];
        return;
    }

    // Debounce search by 300ms
    searchTimeout = setTimeout(async () => {
        loading.value = true;
        try {
            const data = await searchPersons(query);
            results.value = data;
            showDropdown.value = true;
        } catch (err) {
            error.value = err instanceof Error ? err.message : 'Failed to search';
            results.value = [];
        } finally {
            loading.value = false;
        }
    }, 300);
};

const handleFocus = () => {
    if (searchQuery.value.length >= 3 || results.value.length > 0) {
        showDropdown.value = true;
    }
};

const handleBlur = () => {
    // Delay hiding dropdown to allow click events to fire
    setTimeout(() => {
        showDropdown.value = false;
    }, 200);
};

const clearSearch = () => {
    searchQuery.value = '';
    results.value = [];
    error.value = null;
    showDropdown.value = false;
};

const selectPerson = (person: GroupMember) => {
    emit('select', person);
    searchQuery.value = '';
    results.value = [];
    showDropdown.value = false;
};

// Clean up timeout on unmount
onUnmounted(() => {
    if (searchTimeout) {
        clearTimeout(searchTimeout);
    }
});
</script>
