<template>
    <div class="relative">
        <div class="relative">
            <input ref="searchInput" v-model="searchQuery" @input="handleInput" @focus="handleFocus" @keydown="handleKeydown" type="text"
                placeholder="Legg til person..."
                class="w-full px-2 py-1.5 text-sm border-2 border-brand-500 bg-brand-50 rounded-lg focus:outline-none focus:bg-white placeholder:text-neutral-400"
                :disabled="loading" />

            <!-- Loading indicator -->
            <div v-if="loading" class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-neutral-400">
                ...
            </div>
        </div>

        <!-- Dropdown results -->
        <div v-if="showDropdown && (results.length > 0 || error || (searchQuery.length > 0 && searchQuery.length < 3))"
            ref="dropdown"
            class="absolute z-50 w-full mt-1 bg-neutral-100 border border-neutral-300 rounded-md shadow-sm overflow-hidden">

            <!-- Minimum characters hint -->
            <div v-if="searchQuery.length > 0 && searchQuery.length < 3" class="px-2 py-1 text-xs text-neutral-500">
                Skriv minst 3 tegn
            </div>

            <!-- Error message -->
            <div v-else-if="error" class="px-2 py-1 text-xs text-red-600">
                {{ error }}
            </div>

            <!-- Results list -->
            <div v-else-if="results.length > 0" class="max-h-40 overflow-y-auto">
                <button v-for="(person, index) in results" :key="person.person_uid"
                    @mousedown.prevent="selectPerson(person)"
                    ref="resultButtons"
                    :class="['w-full px-2 py-1 text-left text-xs hover:bg-brand-50 focus:bg-brand-50 focus:outline-none border-b border-neutral-200 last:border-b-0 flex items-center gap-1.5', selectedIndex === index ? 'bg-brand-100' : '']">
                    <svg class="w-3 h-3 text-brand-600 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                    </svg>
                    <span>{{ person.name }}</span>
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

const searchInput = ref<HTMLInputElement | null>(null);
const searchQuery = ref('');
const results = ref<GroupMember[]>([]);
const showDropdown = ref(false);
const loading = ref(false);
const error = ref<string | null>(null);
const selectedIndex = ref<number>(-1);
const dropdown = ref<HTMLDivElement | null>(null);
const resultButtons = ref<HTMLButtonElement[]>([]);
let searchTimeout: ReturnType<typeof setTimeout> | null = null;

const handleInput = () => {
    error.value = null;
    selectedIndex.value = -1;

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

const handleKeydown = (event: KeyboardEvent) => {
    const totalResults = results.value.length;

    if (totalResults === 0) return;

    if (event.key === 'ArrowDown') {
        event.preventDefault();
        selectedIndex.value = Math.min(selectedIndex.value + 1, totalResults - 1);
        scrollToSelected();
    } else if (event.key === 'ArrowUp') {
        event.preventDefault();
        selectedIndex.value = Math.max(selectedIndex.value - 1, -1);
        scrollToSelected();
    } else if (event.key === 'Enter') {
        event.preventDefault();
        if (selectedIndex.value >= 0 && selectedIndex.value < totalResults) {
            selectPerson(results.value[selectedIndex.value]);
        }
    } else if (event.key === 'Escape') {
        event.preventDefault();
        searchQuery.value = '';
        results.value = [];
        showDropdown.value = false;
        selectedIndex.value = -1;
    }
};

const scrollToSelected = () => {
    if (selectedIndex.value >= 0 && resultButtons.value[selectedIndex.value]) {
        resultButtons.value[selectedIndex.value].scrollIntoView({
            block: 'nearest',
            behavior: 'smooth'
        });
    }
};

const selectPerson = (person: GroupMember) => {
    emit('select', person);
    searchQuery.value = '';
    results.value = [];
    selectedIndex.value = -1;
    // Keep focus on the input field
    if (searchInput.value) {
        searchInput.value.focus();
    }
};

// Clean up timeout on unmount
onUnmounted(() => {
    if (searchTimeout) {
        clearTimeout(searchTimeout);
    }
});
</script>
