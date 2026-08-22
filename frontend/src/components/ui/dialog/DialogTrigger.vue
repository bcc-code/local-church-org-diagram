<script setup lang="ts">
import { DialogTrigger } from 'radix-vue';
</script>

<template>
  <DialogTrigger v-bind="$attrs">
    <slot />
  </DialogTrigger>
</template>
