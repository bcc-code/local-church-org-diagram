<template>
    <div class="relative">
        <div class="relative">
            <input v-model="searchQuery" @input="handleInput" @focus="handleFocus" type="text"
                placeholder="Søk etter person..."
                class="w-full px-1.5 py-1 sm:px-2 sm:py-1.5 text-xs sm:text-sm border border-neutral-300 rounded bg-white focus:outline-none focus:border-brand-500 placeholder:text-neutral-400"
                :disabled="loading" />

            <!-- Loading indicator -->
            <div v-if="loading" class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-neutral-400">
                ...
            </div>
        </div>

        <!-- Dropdown results -->
        <div v-if="showDropdown && (results.length > 0 || error || (searchQuery.length > 0 && searchQuery.length < 3))"
            @mousedown.prevent
            class="absolute z-50 w-full mt-1 bg-white border border-neutral-200 rounded shadow-sm overflow-hidden">

            <!-- Minimum characters hint -->
            <div v-if="searchQuery.length > 0 && searchQuery.length < 3" class="px-2 py-1.5 text-xs text-neutral-500">
                Skriv minst 3 tegn
            </div>

            <!-- Error message -->
            <div v-else-if="error" class="px-2 py-1.5 text-xs text-red-600">
                {{ error }}
            </div>

            <!-- Results list -->
            <div v-else-if="results.length > 0" class="max-h-40 overflow-y-auto">
                <button v-for="person in results" :key="person.person_uid" @click="selectPerson(person)"
                    class="w-full px-1.5 py-1 sm:px-2 sm:py-1.5 text-left text-xs sm:text-sm hover:bg-neutral-50 focus:bg-neutral-50 focus:outline-none border-b border-neutral-100 last:border-b-0">
                    {{ person.name }}
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
    showDropdown.value = true;
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
