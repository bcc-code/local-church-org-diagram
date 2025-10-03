<template>
  <div class="page">
    <h1>Org Chart Demo</h1>
    <div ref="chartEl" class="chart-container"></div>
  </div>

</template>

<script lang="ts">
import { defineComponent, onMounted, ref } from 'vue';

export default defineComponent({
  name: 'App',
  setup() {
    const chartEl = ref<HTMLDivElement | null>(null);

    onMounted(async () => {
      try {
        const res = await fetch('/org-data.json');
        const data = await res.json();
        const d3 = (window as any).d3;
        if (!d3 || !d3.OrgChart) {
          console.error('d3-org-chart not loaded');
          return;
        }
        const chart = new d3.OrgChart()
          .container(chartEl.value as HTMLElement)
          .data(data)
          .nodeHeight(() => 120)
          .nodeWidth(() => 250)
          .childrenMargin(() => 40)
          .neighbourMargin(() => 20)
          .nodeContent((node: any, i: number, arr: any[], state: any) => {
            const w = state.nodeWidth(node);
            const h = state.nodeHeight(node);
            const name: string = node?.data?.name || '';
            const title: string = node?.data?.title || '';
            const initials = name
              .split(' ')
              .filter(Boolean)
              .slice(0, 2)
              .map((s) => s[0]?.toUpperCase())
              .join('');
            return `
              <div style="box-sizing:border-box;width:${w}px;height:${h}px;padding:12px;border:1px solid #E4E2E9;border-radius:8px;background:#ffffff;box-shadow:0 1px 2px rgba(0,0,0,0.05);display:flex;align-items:center;">
                <div style="width:44px;height:44px;border-radius:999px;background:#4c6ef5;color:#fff;display:flex;align-items:center;justify-content:center;font-weight:700;flex:0 0 auto;">${initials}</div>
                <div style="margin-left:12px;min-width:0;">
                  <div style="font-size:14px;font-weight:700;color:#111;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${name}</div>
                  <div style="font-size:12px;color:#666;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${title}</div>
                </div>
              </div>
            `;
          })
          .compact(true)
          .render();

        // Expose for quick console tweaking in dev
        (window as any).orgChart = chart;
      } catch (e) {
        console.error('Failed to load org data', e);
      }
    });

    return { chartEl };
  },
});
</script>

<style>
.page {
  padding: 16px;
}

.chart-container {
  width: 100%;
  height: 100%;
  border: 1px solid #e4e2e9;
  background: #fff;
}
</style>
