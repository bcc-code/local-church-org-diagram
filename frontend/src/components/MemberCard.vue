<template>
    <div
        class="flex items-center p-3 border border-silver-tree-400 rounded-lg bg-neutral-50 hover:bg-neutral-100 transition-colors">
        <img v-if="member.profile_picture" :src="member.profile_picture" :alt="member.name"
            class="w-8 h-8 rounded-full object-cover flex-shrink-0" />
        <div v-else
            class="w-8 h-8 rounded-full bg-silver-tree-600 text-neutral-0 flex items-center justify-center font-bold text-xs flex-shrink-0">
            {{ initials }}
        </div>
        <div class="ml-3 min-w-0 flex-1">
            <!-- View Mode -->
            <div v-if="!isEditing">
                <!-- Title (above name) -->
                <div v-if="member.title" class="text-sm text-neutral-400 mb-0.5">
                    {{ member.title }}
                </div>

                <!-- Name (with optional link) -->
                <a v-if="member.link" :href="member.link" target="_blank" rel="noopener noreferrer"
                    class="flex items-center gap-1.5 text-body-md font-medium text-brand-600 hover:text-brand-700 underline decoration-1 underline-offset-2 cursor-pointer group">
                    {{ member.name }}
                    <svg class="w-4 h-4 flex-shrink-0 opacity-70 group-hover:opacity-100 transition-opacity" fill="none"
                        stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                            d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                    </svg>
                </a>
                <div v-else class="text-body-md font-medium text-neutral-900">
                    {{ member.name }}
                </div>
            </div>

            <!-- Edit Mode -->
            <div v-else>
                <div class="space-y-1.5 pb-2 mb-2 border-b border-neutral-200">
                    <!-- Title input -->
                    <input v-model="editedTitle" type="text"
                        class="w-full text-xs text-neutral-500 bg-white border border-neutral-300 rounded px-2 py-1 focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500"
                        placeholder="Tittel" @keyup.enter="handleSave" @keyup.escape="handleCancelEdit" />

                    <!-- Name (read-only in edit mode) -->
                    <div class="text-sm font-medium text-neutral-900">
                        {{ member.name }}
                    </div>

                    <!-- Link input -->
                    <input v-model="editedLink" type="url"
                        class="w-full text-xs text-neutral-600 bg-white border border-neutral-300 rounded px-2 py-1 focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500"
                        placeholder="https://..." @keyup.enter="handleSave" @keyup.escape="handleCancelEdit" />
                </div>

                <!-- Action buttons (below all fields) -->
                <div class="flex items-center gap-1.5">
                    <button @click="handleSave"
                        class="flex-1 flex items-center justify-center gap-1.5 py-1.5 rounded bg-green-600 hover:bg-green-700 text-white text-xs font-medium transition-colors"
                        title="Lagre">
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"
                            stroke-width="2.5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                        </svg>
                        <span>Lagre</span>
                    </button>
                    <button @click="handleCancelEdit"
                        class="flex-1 flex items-center justify-center gap-1.5 py-1.5 rounded bg-neutral-200 hover:bg-neutral-300 text-neutral-700 text-xs font-medium transition-colors"
                        title="Avbryt">
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"
                            stroke-width="2.5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                        <span>Avbryt</span>
                    </button>
                </div>
            </div>
        </div>

        <!-- Action buttons (only in admin mode and when not editing) -->
        <div v-if="adminMode && !isEditing" class="flex items-center gap-1 flex-shrink-0 ml-2">
            <button @click="handleStartEdit"
                class="w-6 h-6 flex items-center justify-center rounded-md hover:bg-brand-200 text-brand-600 transition-colors"
                title="Rediger">
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
    (e: 'update-link', personUid: string, link: string): void;
}

const props = withDefaults(defineProps<Props>(), {
    adminMode: false,
});

const emit = defineEmits<Emits>();

const isEditing = ref(false);
const editedTitle = ref(props.member.title || '');
const editedLink = ref(props.member.link || '');

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
    editedLink.value = props.member.link || '';
    isEditing.value = true;
};

const handleSave = () => {
    // Only emit if values have changed
    if (editedTitle.value !== (props.member.title || '')) {
        emit('update-title', props.member.person_uid, editedTitle.value);
    }
    if (editedLink.value !== (props.member.link || '')) {
        emit('update-link', props.member.person_uid, editedLink.value);
    }
    isEditing.value = false;
};

const handleCancelEdit = () => {
    editedTitle.value = props.member.title || '';
    editedLink.value = props.member.link || '';
    isEditing.value = false;
};
</script>
