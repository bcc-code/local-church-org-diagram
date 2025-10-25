<template>
    <div ref="chartEl" class="rounded-xl h-auto bg-neutral-0 shadow-lg border border-neutral-200" />
</template>

<script lang="ts">
import { defineComponent, onMounted, ref, createApp } from 'vue';
import OrgNode from './OrgNode.vue';

type Group = {
    group_id: number;
    label: string;
    parent_group_id: number | null;
    member_count: number
};

type OrgNodeData = {
    id: number | string;
    parentId: number | string | null;
    name: string;
    title: string;
    raw: Group;
};

export default defineComponent({
    name: 'OrgChart',
    components: { OrgNode },
    setup() {
        const chartEl = ref<HTMLDivElement | null>(null);

        const toOrgNodes = (groups: Group[]): OrgNodeData[] => {
            return groups.map((g) => {

                const title = g.member_count > 0 ? `${g.member_count} medlemmer` : '';

                return {
                    id: g.group_id,
                    parentId: g.parent_group_id,
                    name: g.label,
                    title,
                    raw: g,
                };
            });
        };

        onMounted(async () => {
            try {
                const res = await fetch('api/tree');
                const groups: Group[] = await res.json();
                const data: OrgNodeData[] = toOrgNodes(groups);

                const d3 = (window as any).d3;
                if (!d3 || !d3.OrgChart) {
                    console.error('d3-org-chart not loaded');
                    return;
                }

                const chart: any = new d3.OrgChart()
                    .container(chartEl.value as HTMLElement)
                    .data(data)
                    .nodeId((d: OrgNodeData) => d.id)
                    .parentNodeId((d: OrgNodeData) => d.parentId)
                    .childrenMargin(() => 40)
                    .neighbourMargin(() => 20)
                    .nodeHeight(() => 120)
                    .nodeWidth(() => 250)
                    .compact(false)
                    .nodeContent((node: any, _i: number, _arr: any[], state: any) => {
                        const w = state.nodeWidth(node);
                        const h = state.nodeHeight(node);
                        const id = state.nodeId(node.data);
                        return `<div class="org-node-mount" data-node-id="${id}" style="width:${w}px;height:${h}px;"></div>`;
                    })
                    .render();

                // Mount Vue components into each node placeholder
                const mountedApps: Record<string, ReturnType<typeof createApp>> = {};

                const mountNodes = () => {
                    const container: HTMLElement | null = chartEl.value;
                    if (!container) return;
                    const state = chart.getChartState();
                    const mounts = container.querySelectorAll('.org-node-mount');

                    mounts.forEach((el) => {
                        const host = el as HTMLElement;
                        const nodeId = host.getAttribute('data-node-id') || '';
                        const node = (state.allNodes || []).find(
                            (n: any) => state.nodeId(n.data).toString() === nodeId.toString(),
                        );
                        if (!node) return;

                        const data: OrgNodeData = node.data;
                        const width = state.nodeWidth(node);
                        const height = state.nodeHeight(node);

                        // Avoid leaks on updates
                        mountedApps[nodeId]?.unmount?.();
                        host.innerHTML = '';

                        const app = createApp(OrgNode, {
                            name: data.name,
                            title: data.title,
                            width,
                            height,
                            groupId: data.id,
                            memberCount: data.raw.member_count,
                            parentGroupId: data.parentId,
                            raw: data.raw,
                        });
                        app.mount(host);
                        mountedApps[nodeId] = app;
                    });
                };

                mountNodes();

                // Re-mount after any chart restyle/update
                const origRestyle = chart.restyleForeignObjectElements.bind(chart);
                chart.restyleForeignObjectElements = (...args: any[]) => {
                    const res = origRestyle(...args);
                    mountNodes();
                    return res;
                };

                // Expose for debugging
                (window as any).orgChart = chart;
            } catch (e) {
                console.error('Failed to load org data', e);
            }
        });

        return { chartEl };
    },
});
</script>