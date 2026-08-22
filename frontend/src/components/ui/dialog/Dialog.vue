<script setup lang="ts">
import { DialogRoot } from 'radix-vue';
</script>

<template>
  <DialogRoot v-bind="$attrs">
    <slot />
  </DialogRoot>
</template>
