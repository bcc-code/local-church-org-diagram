<template>
    <div class="relative">
        <div class="relative">
            <!-- Search icon -->
            <div class="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-400 pointer-events-none">
                <Icon name="Search" :size="16" />
            </div>

            <input v-model="searchQuery" @input="handleInput" @focus="handleFocus" @blur="handleBlur" type="text"
                placeholder="Søk etter person..."
                class="w-full pl-9 pr-10 py-2.5 text-sm border border-neutral-300 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-brand-500 focus:border-brand-500 transition-shadow placeholder:text-neutral-400"
                :disabled="loading" />

            <!-- Loading spinner -->
            <div v-if="loading"
                class="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 border-2 border-brand-200 border-t-brand-600 rounded-full animate-spin">
            </div>

            <!-- Clear button -->
            <button v-else-if="searchQuery.length > 0" @click="clearSearch"
                class="absolute right-3 top-1/2 -translate-y-1/2 text-neutral-400 hover:text-neutral-600 transition-colors">
                <Icon name="X" :size="16" />
            </button>
        </div>

        <!-- Dropdown results -->
        <div v-if="showDropdown && (results.length > 0 || error || (searchQuery.length > 0 && searchQuery.length < 3))"
            class="absolute z-50 w-full mt-2 bg-white border border-neutral-200 rounded-lg shadow-lg overflow-hidden">

            <!-- Minimum characters hint -->
            <div v-if="searchQuery.length > 0 && searchQuery.length < 3"
                class="px-3 py-2.5 text-xs text-neutral-500 bg-neutral-50">
                Skriv minst 3 tegn for å søke
            </div>

            <!-- Error message -->
            <div v-else-if="error" class="px-3 py-2.5 text-sm text-red-600 bg-red-50">
                <div class="flex items-center gap-2">
                    <Icon name="AlertCircle" :size="16" class="flex-shrink-0" />
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
                            <div class="text-xs text-neutral-500 truncate font-mono mt-0.5">{{ person.person_uid }}
                            </div>
                        </div>
                        <Icon name="Plus" :size="16"
                            class="text-neutral-400 group-hover:text-brand-600 transition-colors flex-shrink-0" />
                    </div>
                </button>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { ref, onUnmounted } from 'vue';
import { useApiClient } from '@/composables/useApi';
import Icon from '@/components/ui/icon/Icon.vue';
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
