<script lang="ts" setup>
import { computed, type Component } from 'vue';
import * as LucideIcons from 'lucide-vue-next';
import { cn } from '@/lib/utils';

interface IconProps {
    name: keyof typeof LucideIcons;
    size?: number | string;
    strokeWidth?: number | string;
    class?: string;
    color?: string;
}

const props = withDefaults(defineProps<IconProps>(), {
    size: 24,
    strokeWidth: 2,
});

const iconComponent = computed(() => {
    const icon = LucideIcons[props.name];
    return icon as Component;
});
</script>

<template>
    <component :is="iconComponent" :size="props.size" :stroke-width="props.strokeWidth" :color="props.color"
        :class="cn('inline-block', props.class)" />
</template>
