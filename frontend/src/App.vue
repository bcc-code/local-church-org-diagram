<template>
  <div class="p-4 bg-cyan-700">
    <h1 class="text-2xl font-semibold mb-4">Org Chart Demo</h1>
    <div class="mb-4 flex gap-2">
      <Button>Primary</Button>
      <Dialog>
        <DialogTrigger as-child>
          <Button variant="outline">Open Dialog</Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Invite user</DialogTitle>
            <DialogDescription>Send an invitation link to this user.</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose as-child>
              <Button variant="secondary">Close</Button>
            </DialogClose>
            <Button>Send</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
    <div ref="chartEl" class="rounded-xl bg-white overflow-hidden"></div>
  </div>

</template>

<script lang="ts">
import { defineComponent, onMounted, ref, createApp } from 'vue';
import OrgNodeCard from './components/OrgNodeCard.vue';
import Button from '@/components/ui/button/Button.vue';
import Dialog from '@/components/ui/dialog/Dialog.vue';
import DialogTrigger from '@/components/ui/dialog/DialogTrigger.vue';
import DialogContent from '@/components/ui/dialog/DialogContent.vue';
import DialogHeader from '@/components/ui/dialog/DialogHeader.vue';
import DialogTitle from '@/components/ui/dialog/DialogTitle.vue';
import DialogDescription from '@/components/ui/dialog/DialogDescription.vue';
import DialogFooter from '@/components/ui/dialog/DialogFooter.vue';
import DialogClose from '@/components/ui/dialog/DialogClose.vue';

export default defineComponent({
  name: 'App',
  components: { Button, Dialog, DialogTrigger, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter, DialogClose },
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
          .nodeContent((node: any, _i: number, _arr: any[], state: any) => {
            const w = state.nodeWidth(node);
            const h = state.nodeHeight(node);
            const id = state.nodeId(node.data);
            return `<div class="org-node-mount" data-node-id="${id}" style="width:${w}px;height:${h}px;"></div>`;
          })
          .compact(true)
          .render();

        // Mount Vue components into each node placeholder
        const mountedApps: Record<string, any> = {};
        const mountNodes = () => {
          const container: HTMLElement | null = chartEl.value;
          if (!container) return;
          const state = chart.getChartState();
          const mounts = container.querySelectorAll('.org-node-mount');
          mounts.forEach((el) => {
            const host = el as HTMLElement;
            const nodeId = host.getAttribute('data-node-id') || '';
            const node = (state.allNodes || []).find((n: any) => state.nodeId(n.data).toString() === nodeId.toString());
            if (!node) return;
            const name: string = node?.data?.name || '';
            const title: string = node?.data?.title || '';
            const width = state.nodeWidth(node);
            const height = state.nodeHeight(node);

            // Unmount previous app if exists to avoid leaks
            mountedApps[nodeId]?.unmount?.();
            host.innerHTML = '';
            const app = createApp(OrgNodeCard, { name, title, width, height });
            app.mount(host);
            mountedApps[nodeId] = app;
          });
        };

        mountNodes();

        // Hook into chart's foreignObject restyling to re-mount after updates
        const origRestyle = chart.restyleForeignObjectElements.bind(chart);
        chart.restyleForeignObjectElements = (...args: any[]) => {
          const res = origRestyle(...args);
          mountNodes();
          return res;
        };

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
