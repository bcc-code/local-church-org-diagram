<template>
  <div class="p-6 h-screen max-h-screen bg-brand-600">
    <OrgChart :admin-mode="isGlobalAdmin" />
  </div>
</template>

<script lang="ts" setup>
import OrgChart from '../components/OrgChart.vue';
import { useAuth } from '../composables/useAuth';

const { isGlobalAdmin } = useAuth();

console.log('isGlobalAdmin:', isGlobalAdmin.value);
</script>
