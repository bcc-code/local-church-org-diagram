<template>
    <div class="box-border w-full h-full p-3 border border-[#E4E2E9] rounded-lg bg-white shadow-sm flex items-center">
        <div
            class=" w-11 h-11 rounded-full bg-[#4c6ef5] text-white flex items-center justify-center font-bold flex-shrink-0">
            {{ computedInitials }}
        </div>
        <div class="ml-3 min-w-0">
            <div class="text-sm font-bold text-[#111] whitespace-nowrap overflow-hidden text-ellipsis" :title="name">
                {{ name }}
            </div>
            <div class="text-xs text-[#666] whitespace-nowrap overflow-hidden text-ellipsis" :title="title">
                {{ title }}
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue';

interface Props {
    name: string;
    title?: string;
    width?: number;
    height?: number;
}

const props = defineProps<Props>();

const computedInitials = computed(() => {
    const n = props.name || '';
    return n
        .split(' ')
        .filter(Boolean)
        .slice(0, 2)
        .map((s) => s[0]?.toUpperCase())
        .join('');
});
</script>
