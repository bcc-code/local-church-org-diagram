<template>
  <div class="p-4 h-screen max-h-screen bg-cyan-700">
    <h1 class="text-2xl font-semibold mb-4 text-white">Org Chart Demo</h1>
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
