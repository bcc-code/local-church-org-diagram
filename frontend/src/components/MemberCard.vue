<template>
    <div
        class="flex items-center p-3 border border-silver-tree-400 rounded-lg bg-neutral-50 hover:bg-neutral-100 transition-colors">
        <div
            class="w-8 h-8 rounded-full bg-silver-tree-600 text-neutral-0 flex items-center justify-center font-bold text-xs flex-shrink-0">
            {{ initials }}
        </div>
        <div class="ml-3 min-w-0 flex-1">
            <div v-if="!isEditingTitle" class="text-sm text-neutral-400">
                {{ member.title }}
            </div>
            <div v-else class="flex items-center gap-2">
                <input v-model="editedTitle" type="text"
                    class="text-sm text-neutral-400 bg-neutral-0 border border-brand-300 rounded px-2 py-1 flex-1 focus:outline-none focus:ring-2 focus:ring-brand-500"
                    placeholder="Legg til tittel" @keyup.enter="handleSaveTitle" @keyup.escape="handleCancelEdit" />
                <button @click="handleSaveTitle"
                    class="w-6 h-6 flex items-center justify-center rounded-md hover:bg-green-200 text-green-600 transition-colors flex-shrink-0"
                    title="Lagre">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                    </svg>
                </button>
                <button @click="handleCancelEdit"
                    class="w-6 h-6 flex items-center justify-center rounded-md hover:bg-neutral-200 text-neutral-600 transition-colors flex-shrink-0"
                    title="Avbryt">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                            d="M6 18L18 6M6 6l12 12" />
                    </svg>
                </button>
            </div>
            <div class="text-body-md font-medium text-neutral-900">
                {{ member.name }}
            </div>
        </div>
        <div v-if="adminMode && !isEditingTitle" class="flex items-center gap-1 flex-shrink-0">
            <button @click="handleStartEdit"
                class="ml-2 w-6 h-6 flex items-center justify-center rounded-md hover:bg-brand-200 text-brand-600 transition-colors"
                title="Rediger tittel">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                </svg>
            </button>
            <button @click="handleRemove"
                class="w-6 h-6 flex items-center justify-center rounded-md hover:bg-red-200 text-red-600 transition-colors"
                title="Fjern medlem">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
            </button>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';
import type { GroupMember } from '@/types';

interface Props {
    member: GroupMember;
    adminMode?: boolean;
}

interface Emits {
    (e: 'remove', personUid: string): void;
    (e: 'update-title', personUid: string, title: string): void;
}

const props = withDefaults(defineProps<Props>(), {
    adminMode: false,
});

const emit = defineEmits<Emits>();

const isEditingTitle = ref(false);
const editedTitle = ref(props.member.title || '');

const initials = computed(() => {
    return props.member.name
        .split(' ')
        .filter(Boolean)
        .slice(0, 2)
        .map((s) => s[0]?.toUpperCase())
        .join('');
});

const handleRemove = () => {
    emit('remove', props.member.person_uid);
};

const handleStartEdit = () => {
    editedTitle.value = props.member.title || '';
    isEditingTitle.value = true;
};

const handleSaveTitle = () => {
    emit('update-title', props.member.person_uid, editedTitle.value);
    isEditingTitle.value = false;
};

const handleCancelEdit = () => {
    editedTitle.value = props.member.title || '';
    isEditingTitle.value = false;
};
</script>