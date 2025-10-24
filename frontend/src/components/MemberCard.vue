<template>
    <div class="flex items-center p-2 border border-gray-200 rounded-lg bg-gray-50">
        <div
            class="w-8 h-8 rounded-full bg-[#4c6ef5] text-white flex items-center justify-center font-bold text-xs flex-shrink-0">
            {{ initials }}
        </div>
        <div class="ml-2 min-w-0">
            <div class="text-sm font-medium text-gray-900">
                {{ member.name }}
            </div>
            <div class="text-xs text-gray-500">
                ID: {{ member.person_uid }}
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue';

interface GroupMember {
    name: string;
    person_uid: string;
    group_id: number;
}

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