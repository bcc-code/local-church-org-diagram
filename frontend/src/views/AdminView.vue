<template>
  <div class="p-6 h-screen max-h-screen bg-brand-600">
    <h2 class="text-neutral-100 absolute top-1 text-body-sm">
      Admin
    </h2>
    <OrgChart :admin-mode="true" />
  </div>
</template>

<script lang="ts" setup>
import OrgChart from '../components/OrgChart.vue';

interface Props {
  tenantId: string;
}

defineProps<Props>();
</script>
