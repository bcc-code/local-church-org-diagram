<template>
  <div class="p-6 h-screen max-h-screen bg-brand-600">
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-title-lg text-neutral-0">{{ TEXTS.APP_TITLE }} - Admin</h1>
      <div class="text-neutral-100 text-body-sm">
        Tenant ID: {{ tenantId }}
      </div>
    </div>
    <OrgChart :admin-mode="true" />
  </div>
</template>

<script lang="ts" setup>
import OrgChart from '../components/OrgChart.vue';
import { TEXTS } from '../constants';

interface Props {
  tenantId: string;
}

defineProps<Props>();
</script>
