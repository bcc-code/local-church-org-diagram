<template>
  <div class="p-6 h-screen max-h-screen bg-brand-600">
    <h1 class="text-title-lg mb-6 text-neutral-0">Organisasjonskart</h1>
    <OrgChart />
  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import OrgChart from './components/OrgChart.vue';

export default defineComponent({
  name: 'App',
  components: { OrgChart },
});
</script>
