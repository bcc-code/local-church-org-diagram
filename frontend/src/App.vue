<template>
  <div class="p-6 h-screen max-h-screen bg-brand-600">
    <h1 class="text-title-lg mb-6 text-neutral-0">{{ TEXTS.APP_TITLE }}</h1>
    <OrgChart />
  </div>
</template>

<script lang="ts" setup>
import OrgChart from './components/OrgChart.vue';
import { TEXTS } from './constants';
</script>
