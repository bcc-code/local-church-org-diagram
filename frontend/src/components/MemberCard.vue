<template>
    <div
        class="flex items-center p-3 border border-silver-tree-400 rounded-lg bg-neutral-50 hover:bg-neutral-100 transition-colors">
        <div
            class="w-8 h-8 rounded-full bg-silver-tree-600 text-neutral-0 flex items-center justify-center font-bold text-xs flex-shrink-0">
            {{ initials }}
        </div>
        <div class="ml-3 min-w-0">
            <div class="text-body-sm font-medium text-neutral-900">
                {{ member.name }}
            </div>
            <!-- <div class="text-caption text-neutral-600">
                ID: {{ member.person_uid }}
            </div> -->
        </div>
    </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue';
import type { GroupMember } from '@/types';

interface Props {
    member: GroupMember;
}

const props = defineProps<Props>();

const initials = computed(() => {
    return props.member.name
        .split(' ')
        .filter(Boolean)
        .slice(0, 2)
        .map((s) => s[0]?.toUpperCase())
        .join('');
});
</script>