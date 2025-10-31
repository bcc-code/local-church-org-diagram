<template>
    <div class="relative">
        <div class="relative">
            <input v-model="searchQuery" @input="handleInput" @focus="showDropdown = true" @blur="handleBlur"
                type="text" placeholder="Søk etter person (min 3 tegn)..."
                class="w-full px-3 py-2 border border-neutral-300 rounded-md focus:outline-none focus:ring-2 focus:ring-brand-500 focus:border-transparent"
                :disabled="loading" />
            <div v-if="loading"
                class="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 border-2 border-brand-200 border-t-brand-600 rounded-full animate-spin">
            </div>
        </div>

        <div v-if="showDropdown && (results.length > 0 || error)"
            class="absolute z-50 w-full mt-1 bg-neutral-0 border border-neutral-200 rounded-md shadow-lg max-h-48 overflow-y-auto">
            <div v-if="error" class="p-3 text-sm text-red-600">
                {{ error }}
            </div>
            <div v-else-if="results.length > 0">
                <button v-for="person in results" :key="person.person_uid" @mousedown.prevent="selectPerson(person)"
                    class="w-full px-3 py-2 text-left hover:bg-neutral-100 focus:bg-neutral-100 focus:outline-none border-b border-neutral-100 last:border-b-0">
                    <div class="text-body-sm font-medium text-neutral-900">{{ person.name }}</div>
                    <div class="text-caption text-neutral-600">ID: {{ person.person_uid }}</div>
                </button>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { ref, watch } from 'vue';
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

const handleBlur = () => {
    // Delay hiding dropdown to allow click events to fire
    setTimeout(() => {
        showDropdown.value = false;
    }, 200);
};

const selectPerson = (person: GroupMember) => {
    emit('select', person);
    searchQuery.value = '';
    results.value = [];
    showDropdown.value = false;
};

// Clean up timeout on unmount
watch(() => {
    return () => {
        if (searchTimeout) {
            clearTimeout(searchTimeout);
        }
    };
});
</script>
