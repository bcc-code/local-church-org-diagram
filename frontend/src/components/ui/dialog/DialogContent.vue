<script setup lang="ts">
import { DialogPortal, DialogOverlay, DialogContent } from 'radix-vue';
import { cn } from '@/lib/utils';
defineProps<{ class?: string }>();
</script>

<template>
    <DialogPortal>
        <DialogOverlay
            class="fixed inset-0 z-50 bg-black/80 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <DialogContent
            :class="cn('fixed z-50 grid w-full gap-4 rounded-b-lg border bg-background p-6 shadow-lg sm:max-w-lg sm:rounded-lg left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[state=closed]:slide-out-to-left-1/2 data-[state=closed]:slide-out-to-top-[48%] data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%]', $props.class)">
            <slot />
        </DialogContent>
    </DialogPortal>
</template>
