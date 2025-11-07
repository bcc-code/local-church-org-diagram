<template>
    <div
        class="flex items-center p-3 border border-silver-tree-400 rounded-lg bg-neutral-50 hover:bg-neutral-100 transition-colors">
        <div
            class="w-8 h-8 rounded-full bg-silver-tree-600 text-neutral-0 flex items-center justify-center font-bold text-xs flex-shrink-0">
            {{ initials }}
        </div>
        <div class="ml-3 min-w-0 flex-1">
            <div v-if="member.title" class="text-sm text-neutral-400">
                {{ member.title }}
            </div>
            <div class="text-body-md font-medium text-neutral-900">
                {{ member.name }}
            </div>
        </div>
        <button v-if="adminMode" @click="handleRemove"
            class="ml-2 w-6 h-6 flex items-center justify-center rounded-md hover:bg-red-200 text-red-600 transition-colors flex-shrink-0"
            title="Fjern medlem">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
        </button>
    </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue';
import type { GroupMember } from '@/types';

interface Props {
    member: GroupMember;
    adminMode?: boolean;
}

interface Emits {
    (e: 'remove', personUid: string): void;
}

const props = withDefaults(defineProps<Props>(), {
    adminMode: false,
});

const emit = defineEmits<Emits>();

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
</script>