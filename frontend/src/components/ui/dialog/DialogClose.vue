<script setup lang="ts">
import { DialogClose } from 'radix-vue';
</script>

<template>
    <DialogClose v-bind="$attrs">
        <slot />
    </DialogClose>
</template>
